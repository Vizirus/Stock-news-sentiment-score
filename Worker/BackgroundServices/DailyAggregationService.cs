using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Worker.BackgroundServices;

public class DailyAggregationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyAggregationService> _logger;
    private DateTime? _lastAggregationDate;

    public DailyAggregationService(IServiceProvider serviceProvider, ILogger<DailyAggregationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyAggregationService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            // Run at exactly 00:00 UTC (or close to it)
            if (now.Hour == 0 && (_lastAggregationDate == null || _lastAggregationDate.Value.Date != now.Date))
            {
                // We want to aggregate data for the previous day
                var targetDate = now.Date.AddDays(-1);
                
                await PerformDailyAggregationAsync(targetDate, stoppingToken);
                
                _lastAggregationDate = now.Date;
            }

            // Check every 10 minutes so we catch the 00:xx hour reliably
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }

    private async Task PerformDailyAggregationAsync(DateTime targetDate, CancellationToken stoppingToken)
    {
        _logger.LogInformation($"Starting Daily Aggregation for {targetDate:yyyy-MM-dd}...");
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDBContext>();

        // Find all scores created on the target date
        var nextDay = targetDate.AddDays(1);
        
        var scoresForDay = await dbContext.ArticleScores
            .Where(s => s.ScoredAt >= targetDate && s.ScoredAt < nextDay)
            .ToListAsync(stoppingToken);

        if (!scoresForDay.Any())
        {
            _logger.LogInformation($"No scores found for {targetDate:yyyy-MM-dd}. Aggregation skipped.");
            return;
        }

        var groupedScores = scoresForDay.GroupBy(s => s.TickerId);

        foreach (var group in groupedScores)
        {
            var tickerId = group.Key;
            var averageScore = group.Average(s => s.Score);
            var articleCount = group.Select(s => s.ArticleId).Distinct().Count();

            // Check if summary already exists for this date and ticker
            var existingSummary = await dbContext.TickerDailySummaries
                .FirstOrDefaultAsync(s => s.TickerId == tickerId && s.SummaryDate == targetDate, stoppingToken);

            if (existingSummary != null)
            {
                existingSummary.AverageScore = averageScore;
                existingSummary.ArticleCount = articleCount;
                existingSummary.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation($"Updated daily summary for Ticker ID {tickerId} on {targetDate:yyyy-MM-dd}.");
            }
            else
            {
                var newSummary = new TickerDailySummary
                {
                    TickerId = tickerId,
                    SummaryDate = targetDate,
                    AverageScore = averageScore,
                    ArticleCount = articleCount,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.TickerDailySummaries.Add(newSummary);
                _logger.LogInformation($"Created new daily summary for Ticker ID {tickerId} on {targetDate:yyyy-MM-dd}.");
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        _logger.LogInformation($"Daily Aggregation for {targetDate:yyyy-MM-dd} completed successfully.");
    }
}
