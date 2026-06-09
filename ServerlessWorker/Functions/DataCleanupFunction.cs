using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Application.UseCases;

namespace ServerlessWorker.Functions
{
    public class DataCleanupFunction
    {
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;

        public DataCleanupFunction(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        {
            _logger = loggerFactory.CreateLogger<DataCleanupFunction>();
            _serviceProvider = serviceProvider;
        }

        // Run once a week (Sunday at 2:00 AM)
        [Function("DataCleanupFunction")]
        public async Task RunAsync([TimerTrigger("0 0 2 * * 0")] TimerInfo myTimer, FunctionContext context)
        {
            _logger.LogInformation($"DataCleanupFunction started at: {DateTime.Now}");

            using (var scope = _serviceProvider.CreateScope())
            {
                var rawDataCleanUpUseCase = scope.ServiceProvider.GetRequiredService<RawDataCleanUpUseCase>();
                await rawDataCleanUpUseCase.ExecuteAsync(30, context.CancellationToken); // Keep raw data for 30 days

                var summaryDataCleanUpUseCase = scope.ServiceProvider.GetRequiredService<SummaryDataCleanUpUseCase>();
                await summaryDataCleanUpUseCase.ExecuteAsync(365, context.CancellationToken); // Keep summaries for 1 year
            }
            
            _logger.LogInformation($"DataCleanupFunction completed at: {DateTime.Now}");
        }
    }
}
