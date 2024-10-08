using ABCRetail.AzureBlobService.Interface;
using ABCRetail.AzureBlobService.Service;
using ABCRetail.AzureQueueService.Interfaces;
using ABCRetail.AzureQueueService.Service;
using ABCRetail.AzureTableService.Interfaces;
using ABCRetail.AzureTableService.ServiceClasses;
using ABCRetail.Repositories.RepositorieInterfaces;
using ABCRetail.Repositories.ServiceClasses;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        //Retrieve the azure storage connection string
        string azureConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        services.AddSingleton(sp => new TableServiceClient(azureConnectionString));
        services.AddSingleton<IBlobStorageService>(provider => new BlobStorageService(azureConnectionString));
        services.AddTransient<IQueueStorageService>(provider => new QueueStorageService(azureConnectionString));

        // Register services
        services.AddTransient<IProductService, ProductService>();
        services.AddSingleton<IOrderService, OrderService>();
        services.AddLogging();
        services.AddHttpClient();
    })
    .Build();

host.Run();
