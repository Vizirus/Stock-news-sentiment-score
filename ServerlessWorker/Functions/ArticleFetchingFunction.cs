using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Application.UseCases;

namespace ServerlessWorker.Functions
{
    public class ArticleFetchingFunction
    {
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;

        public ArticleFetchingFunction(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        {
            _logger = loggerFactory.CreateLogger<ArticleFetchingFunction>();
            _serviceProvider = serviceProvider;
        }

        [Function("ArticleFetchingFunction")]
        public async Task RunAsync([TimerTrigger("0 0 * * * *")] TimerInfo myTimer, FunctionContext context)
        {
            _logger.LogInformation($"ArticleFetchingFunction started at: {DateTime.Now}");

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IAppDBContext>();
                var settings = await dbContext.SystemSettings.FirstOrDefaultAsync(context.CancellationToken);

                if (settings == null)
                {
                    _logger.LogWarning("SystemSettings not found. Skipping fetch.");
                    return;
                }

                int fetchIntervalHours = Math.Clamp(settings.FetchIntervalHours, 1, 24);
                
                if (settings.LastArticleFetchTime.HasValue && 
                    DateTime.UtcNow < settings.LastArticleFetchTime.Value.AddHours(fetchIntervalHours))
                {
                    _logger.LogInformation($"Fetch interval of {fetchIntervalHours} hours has not elapsed yet. Skipping.");
                    return;
                }

                _logger.LogInformation("Executing FetchArticlesUseCase...");
                var fetchArticlesUseCase = scope.ServiceProvider.GetRequiredService<FetchArticlesUseCase>();
                await fetchArticlesUseCase.ExecuteAsync(context.CancellationToken);

                settings.LastArticleFetchTime = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(context.CancellationToken);
            }
            
            _logger.LogInformation($"ArticleFetchingFunction completed at: {DateTime.Now}");
        }
    }
}
