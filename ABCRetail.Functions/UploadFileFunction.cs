using ABCRetail.AzureBlobService.Interface;
using ABCRetail.AzureBlobService.Service;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;


namespace ABCRetail.Functions
{
    public class UploadFileFunction
    {
        private readonly ILogger<UploadFileFunction> _logger;
        private readonly IBlobStorageService _blobStorageService;
        private readonly HttpClient _httpClient;

        public UploadFileFunction(ILogger<UploadFileFunction> logger, IBlobStorageService blobStorageService, HttpClient httpClient)
        {
            _logger = logger;
            _blobStorageService = blobStorageService;
            _httpClient=httpClient;
        }

        [Function("UploadFile")]
        public async Task <IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "uploadfile")] HttpRequest req)
        {
            _logger.LogInformation("Processing file upload");

           // var formdata = await req.ReadFormAsync();
            var file = req.Form.Files["file"];
            // Retrieve file from the request
            if (file == null || file.Length == 0)
            {
                return new BadRequestObjectResult("Please upload a file.");
            }

            var fileName = file.FileName;
            var fileStream = file.OpenReadStream();

            try
            {
                var blobUrl = await _blobStorageService.UploadFileAsync(fileName, fileStream);
                _logger.LogInformation("File uploaded successfully. Blob URL: {BlobUrl}", blobUrl);

                var response = await _httpClient.GetAsync(blobUrl);
                var responseBody = await response.Content.ReadAsStringAsync();
                return new OkObjectResult(new { Url = blobUrl, responseBody });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file to Blob Storage: {ex.ToString()}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

       
        }
    }
}
