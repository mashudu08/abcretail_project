using Azure.Storage.Files.Shares;
using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions
{
    public class UploadFileToAzureFiles
    {
        private readonly ILogger<UploadFileToAzureFiles> log;

        public UploadFileToAzureFiles(ILogger<UploadFileToAzureFiles> logger)
        {
            log = logger;
        }

        [Function("UploadFileToAzureFiles")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req)
        {
            log.LogInformation("UploadFileToAzureFiles function triggered.");

            // Get the directory from query parameters
            string directoryName = req.Query["directory"];
            if (string.IsNullOrEmpty(directoryName))
            {
                log.LogError("Directory parameter is missing.");
                return new BadRequestObjectResult("Directory parameter is missing.");
            }

            // Get the connection string from environment variables
            string connectionString = Environment.GetEnvironmentVariable("AzureFilesConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                log.LogError("Connection string is missing.");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            string shareName = "abcretailuploads"; // Common file share name for both directories

            // Parse the file from the request
            var formCollection = await req.ReadFormAsync();
            var file = formCollection.Files.GetFile("file");

            if (file == null || file.Length == 0)
            {
                log.LogError("File is missing or empty.");
                return new BadRequestObjectResult("File is missing or empty.");
            }

            try
            {
                // Create or connect to the Azure file share
                ShareClient share = new ShareClient(connectionString, shareName);
                await share.CreateIfNotExistsAsync();

                // Get the specific directory (logs or contracts)
                ShareDirectoryClient directory = share.GetDirectoryClient(directoryName);
                await directory.CreateIfNotExistsAsync();

                // Upload the file to the specific directory
                ShareFileClient fileClient = directory.GetFileClient(file.FileName);
                using (var stream = file.OpenReadStream())
                {
                    await fileClient.CreateAsync(stream.Length);
                    await fileClient.UploadRangeAsync(new HttpRange(0, stream.Length), stream);
                }

                log.LogInformation($"File {file.FileName} uploaded successfully to {directoryName}.");
                return new OkObjectResult($"File {file.FileName} uploaded successfully to {directoryName}.");
            }
            catch (Exception ex)
            {
                log.LogError($"An error occurred during file upload: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
