namespace Worker.BackgroundServices;

public class DailyAggregationService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
