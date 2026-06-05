using Application.Interfaces;
using Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Worker.BackgroundServices;

public class ArticleFetchingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    public ArticleFetchingService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int fetchIntervalHours = 6;

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IAppDBContext>();
                var settings = await dbContext.SystemSettings.FirstOrDefaultAsync(stoppingToken);
                if (settings != null)
                {
                    fetchIntervalHours = Math.Clamp(settings.FetchIntervalHours, 1, 24);
                }

                var fetchArticlesUseCase = scope.ServiceProvider.GetRequiredService<FetchArticlesUseCase>();
                await fetchArticlesUseCase.ExecuteAsync(stoppingToken);
            }

            await Task.Delay(TimeSpan.FromHours(fetchIntervalHours), stoppingToken);
        }
    }
}
