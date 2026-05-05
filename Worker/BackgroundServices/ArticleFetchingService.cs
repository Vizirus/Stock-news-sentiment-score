namespace Worker.BackgroundServices;

public class ArticleFetchingService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
