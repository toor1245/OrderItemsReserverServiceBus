using System.Text.Json;
using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace OrderItemsReserverServiceBus;

public static class ReserveOrderItem
{
    private const string OrderBlobConnection = "WEBSITE_CONTENTAZUREFILECONNECTIONSTRING";
    private const string OrderBlobContainer = "order-container";
    private const string AppLogicUrl = "TRIGGER_WHEN_HTTP_REQUEST_RECEIVED";
    
    [Function("ReserveOrderItem")]
    public static void Run(
        [ServiceBusTrigger("orderitemsqueue", Connection = "OrderItemsQueueConnection")] string orderItemReserve,
        FunctionContext context)
    {
        var logger = context.GetLogger("ReserveOrderItem");

        try
        {
            var binaryData = new BinaryData(orderItemReserve);
            logger.LogInformation($"C# ServiceBus queue trigger function processed message: {orderItemReserve}");
        
            var blobOptions = new BlobClientOptions
            {
                Retry = {
                    Delay = TimeSpan.FromSeconds(2),
                    MaxRetries = 3,
                    Mode = RetryMode.Exponential,
                    MaxDelay = TimeSpan.FromSeconds(10),
                    NetworkTimeout = TimeSpan.FromSeconds(100)
                },
            };
            BlobServiceClient blobServiceClient = new(Environment.GetEnvironmentVariable(OrderBlobConnection), blobOptions);
            var containerClient = blobServiceClient.GetBlobContainerClient(OrderBlobContainer);
            containerClient.CreateIfNotExists();
            containerClient.UploadBlob(Path.ChangeExtension(Guid.NewGuid().ToString(), "json"), binaryData);
        }
        catch (Exception ex)
        {
            logger.LogError("Sent order details to email.");
            var content = JsonSerializer.Serialize(new
            {
                To = "koliagogsadze@gmail.com",
                Body = $"Order details received data: {orderItemReserve}\nException Message: {ex.Message}"
            });
            using var httpClient = new HttpClient();
            httpClient.PostAsync(Environment.GetEnvironmentVariable(AppLogicUrl), new StringContent(content));
        }
    }
}