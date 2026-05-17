using Application.Interfaces;
using Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Worker.BackgroundServices;

public class ScoringWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRuntimeSettingsService _runtimeSettingsService;
    private readonly IJobTriggerService _jobTriggerService;

    public ScoringWorkerService(
        IServiceProvider serviceProvider, 
        IRuntimeSettingsService runtimeSettingsService,
        IJobTriggerService jobTriggerService)
    {
        _serviceProvider = serviceProvider;
        _runtimeSettingsService = runtimeSettingsService;
        _jobTriggerService = jobTriggerService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = _runtimeSettingsService.GetSettings();

            using (var scope = _serviceProvider.CreateScope())
            {
                var processScoringUseCase = scope.ServiceProvider.GetRequiredService<ProcessScoringUseCase>();
                
                await processScoringUseCase.ExecuteAsync(
                    settings.DailyLlmCallLimit, 
                    settings.BatchSize, 
                    stoppingToken);
            }

            // Wait for signal to wake up (e.g., when new jobs are added or Retry is clicked)
            await _jobTriggerService.WaitAsync(stoppingToken);
        }
    }
}
