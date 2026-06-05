using Application.Interfaces;
using Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Worker.BackgroundServices;

public class ScoringWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IJobTriggerService _jobTriggerService;

    public ScoringWorkerService(
        IServiceProvider serviceProvider, 
        IJobTriggerService jobTriggerService)
    {
        _serviceProvider = serviceProvider;
        _jobTriggerService = jobTriggerService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int dailyLimit = 100;
            int batchSize = 20;

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IAppDBContext>();
                var settings = await dbContext.SystemSettings.FirstOrDefaultAsync(stoppingToken);
                if (settings != null)
                {
                    dailyLimit = Math.Clamp(settings.DailyLlmCallLimit, 1, 10000);
                    batchSize = Math.Clamp(settings.BatchSize, 1, 90);
                }

                var processScoringUseCase = scope.ServiceProvider.GetRequiredService<ProcessScoringUseCase>();
                
                await processScoringUseCase.ExecuteAsync(
                    dailyLimit, 
                    batchSize, 
                    stoppingToken);
            }

            // Wait for signal to wake up (e.g., when new jobs are added or Retry is clicked)
            await _jobTriggerService.WaitAsync(stoppingToken);
        }
    }
}
