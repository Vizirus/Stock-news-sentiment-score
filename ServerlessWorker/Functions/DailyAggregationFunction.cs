using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Application.UseCases;

namespace ServerlessWorker.Functions
{
    public class DailyAggregationFunction
    {
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;

        public DailyAggregationFunction(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        {
            _logger = loggerFactory.CreateLogger<DailyAggregationFunction>();
            _serviceProvider = serviceProvider;
        }

        // Run once a day at midnight
        [Function("DailyAggregationFunction")]
        public async Task RunAsync([TimerTrigger("0 0 0 * * *")] TimerInfo myTimer, FunctionContext context)
        {
            _logger.LogInformation($"DailyAggregationFunction started at: {DateTime.Now}");

            using (var scope = _serviceProvider.CreateScope())
            {
                var aggregationUseCase = scope.ServiceProvider.GetRequiredService<CreateDailyAggregationUseCase>();
                await aggregationUseCase.ExecuteAsync(context.CancellationToken);
            }
            
            _logger.LogInformation($"DailyAggregationFunction completed at: {DateTime.Now}");
        }
    }
}
