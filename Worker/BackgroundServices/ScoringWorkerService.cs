namespace Worker.BackgroundServices;

public class ScoringWorkerService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
