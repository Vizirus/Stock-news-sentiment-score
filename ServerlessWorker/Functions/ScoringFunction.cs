using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Application.UseCases;

namespace ServerlessWorker.Functions
{
    public class ScoringFunction
    {
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;

        public ScoringFunction(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        {
            _logger = loggerFactory.CreateLogger<ScoringFunction>();
            _serviceProvider = serviceProvider;
        }

        [Function("ScoringFunction_Timer")]
        public async Task RunTimerAsync([TimerTrigger("0 */15 * * * *")] TimerInfo myTimer, FunctionContext context)
        {
            _logger.LogInformation($"ScoringFunction_Timer started at: {DateTime.Now}");
            await ProcessScoringAsync(context.CancellationToken);
        }

        [Function("ScoringFunction_Queue")]
        public async Task RunQueueAsync([QueueTrigger("scoring-jobs")] string message, FunctionContext context)
        {
            _logger.LogInformation($"ScoringFunction_Queue triggered by message: {message}");
            await ProcessScoringAsync(context.CancellationToken);
        }

        private async Task ProcessScoringAsync(CancellationToken cancellationToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IAppDBContext>();
                var settings = await dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
                
                int dailyLimit = 100;
                int batchSize = 20;

                if (settings != null)
                {
                    dailyLimit = Math.Clamp(settings.DailyLlmCallLimit, 1, 10000);
                    batchSize = Math.Clamp(settings.BatchSize, 1, 90);
                }

                var processScoringUseCase = scope.ServiceProvider.GetRequiredService<ProcessScoringUseCase>();
                await processScoringUseCase.ExecuteAsync(dailyLimit, batchSize, cancellationToken);
            }
        }
    }
}
