using Azure.Storage.Queues;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace Infrastructure.Services;

public class QueueJobTriggerService : IJobTriggerService
{
    private readonly QueueClient _queueClient;

    public QueueJobTriggerService(IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("StorageAccount") ?? "UseDevelopmentStorage=true";
        _queueClient = new QueueClient(connectionString, "scoring-jobs");
    }

    public async Task TriggerScoringJobAsync()
    {
        await _queueClient.CreateIfNotExistsAsync();
        await _queueClient.SendMessageAsync("trigger-scoring");
    }
}
