namespace Worker.BackgroundServices;

public class CleanUpService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
