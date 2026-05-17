using Application.Interfaces;
using Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Worker.BackgroundServices;

public class ArticleFetchingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRuntimeSettingsService _runtimeSettingsService;

    public ArticleFetchingService(
        IServiceProvider serviceProvider, 
        IRuntimeSettingsService runtimeSettingsService)
    {
        _serviceProvider = serviceProvider;
        _runtimeSettingsService = runtimeSettingsService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = _runtimeSettingsService.GetSettings();

            using (var scope = _serviceProvider.CreateScope())
            {
                var fetchArticlesUseCase = scope.ServiceProvider.GetRequiredService<FetchArticlesUseCase>();
                await fetchArticlesUseCase.ExecuteAsync(stoppingToken);
            }

            await Task.Delay(TimeSpan.FromHours(settings.FetchIntervalHours), stoppingToken);
        }
    }
}
