﻿using Azure.Storage.Files.Shares;
using ABCRetail.AzureFileService.Interfaces;
using Azure.Storage.Files.Shares.Models;
using ABCRetail.Models;

namespace ABCRetail.AzureFileService.Service
{
    public class ContractsFileService : IContractsFileService
    {
        private readonly ShareClient _shareClient;

        public ContractsFileService(string connectionString)
        {
            var shareName = "contracts"; // Ensure this matches your configuration
            _shareClient = new ShareClient(connectionString, shareName);
            _shareClient.CreateIfNotExists(); // Create the share if it doesn't exist
        }

        public async Task UploadFileAsync(IFormFile file)
        {
            var fileClient = _shareClient.GetDirectoryClient("").GetFileClient(file.FileName); // Use an empty directory name for the root directory
            using (var stream = file.OpenReadStream())
            {
                await fileClient.CreateAsync(stream.Length); // Create the file
                await fileClient.UploadAsync(stream, new ShareFileUploadOptions()); // Upload the file
            }
        }

        public async Task<Stream> DownloadFileAsync(string fileName)
        {
            var fileClient = _shareClient.GetDirectoryClient("").GetFileClient(fileName); // Use an empty directory name for the root directory
            var response = await fileClient.DownloadAsync();
            return response.Value.Content;
        }

        public async Task<IEnumerable<ContractsFileModel>> ListFilesAsync()
        {
            var directoryClient = _shareClient.GetDirectoryClient(""); // Use an empty directory name for the root directory
            var files = new List<ContractsFileModel>();

            await foreach (var item in directoryClient.GetFilesAndDirectoriesAsync())
            {
                if (!item.IsDirectory) // Check if the item is a file
                {
                    var fileClient = directoryClient.GetFileClient(item.Name);
                    var properties = await fileClient.GetPropertiesAsync();

                    files.Add(new ContractsFileModel
                    {
                        FileName = item.Name,
                        FileUrl = fileClient.Uri.ToString(),
                        FileSize = properties.Value.ContentLength,
                        UploadedDate = properties.Value.LastModified.DateTime
                    });
                }
            }
            return files;
        }

    }
}
