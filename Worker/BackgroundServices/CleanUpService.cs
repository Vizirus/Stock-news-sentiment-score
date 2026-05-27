using Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Worker.BackgroundServices;

public class CleanUpService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CleanUpService> _logger;
    private DateTime? _lastWeeklyCleanup;
    private DateTime? _lastYearCleanup;

    public CleanUpService(IServiceProvider serviceProvider, ILogger<CleanUpService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CleanUpService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            // Weekly cleanup: Sunday at 02:00 UTC
            if (now.DayOfWeek == DayOfWeek.Sunday && now.Hour == 2 &&
                (_lastWeeklyCleanup == null || _lastWeeklyCleanup.Value.Date != now.Date))
            {
                await PerformWeeklyCleanupAsync(stoppingToken);
                _lastWeeklyCleanup = now;
            }

            // Year cleanup: Jan 1st at 03:00 UTC
            if (now.Month == 1 && now.Day == 1 && now.Hour == 3 &&
                (_lastYearCleanup == null || _lastYearCleanup.Value.Date != now.Date))
            {
                await PerformYearCleanupAsync(stoppingToken);
                _lastYearCleanup = now;
            }

            // Check every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task PerformWeeklyCleanupAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Weekly Cleanup...");
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDBContext>();

        var articlesToKeep = new HashSet<int>();
        var allTickers = await dbContext.Ticker.Select(t => t.Id).ToListAsync(stoppingToken);

        foreach (var tickerId in allTickers)
        {
            var latestForTicker = await dbContext.ArticleScores
                .Where(s => s.TickerId == tickerId)
                .OrderByDescending(s => s.Article.PublishedAt)
                .Select(s => s.ArticleId)
                .Take(2)
                .ToListAsync(stoppingToken);
            
            foreach (var id in latestForTicker)
            {
                articlesToKeep.Add(id);
            }
        }

        var cutoffDate = DateTime.UtcNow.AddDays(-7);
        var articlesToDelete = await dbContext.Article
            .Where(a => a.PublishedAt < cutoffDate && !articlesToKeep.Contains(a.Id))
            .ToListAsync(stoppingToken);

        if (articlesToDelete.Any())
        {
            dbContext.Article.RemoveRange(articlesToDelete);
            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation($"Weekly Cleanup deleted {articlesToDelete.Count} old articles.");
        }
        else
        {
            _logger.LogInformation("Weekly Cleanup found no articles to delete.");
        }
    }

    private async Task PerformYearCleanupAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Year Cleanup...");
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDBContext>();

        var summariesToKeep = new HashSet<int>();
        var allTickers = await dbContext.Ticker.Select(t => t.Id).ToListAsync(stoppingToken);

        foreach (var tickerId in allTickers)
        {
            var latestForTicker = await dbContext.TickerDailySummaries
                .Where(s => s.TickerId == tickerId)
                .OrderByDescending(s => s.SummaryDate)
                .Select(s => s.Id)
                .Take(10)
                .ToListAsync(stoppingToken);

            foreach (var id in latestForTicker)
            {
                summariesToKeep.Add(id);
            }
        }

        var cutoffDate = DateTime.UtcNow.AddMonths(-12);
        var summariesToDelete = await dbContext.TickerDailySummaries
            .Where(s => s.SummaryDate < cutoffDate && !summariesToKeep.Contains(s.Id))
            .ToListAsync(stoppingToken);

        if (summariesToDelete.Any())
        {
            dbContext.TickerDailySummaries.RemoveRange(summariesToDelete);
            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation($"Year Cleanup deleted {summariesToDelete.Count} old summaries.");
        }
        else
        {
            _logger.LogInformation("Year Cleanup found no summaries to delete.");
        }
    }
}
