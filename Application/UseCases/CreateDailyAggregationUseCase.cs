using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.UseCases;

public class CreateDailyAggregationUseCase
{
    private readonly IAppDBContext _dbContext;
    private readonly ILogger<CreateDailyAggregationUseCase> _logger;

    public CreateDailyAggregationUseCase(
        IAppDBContext dbContext,
        ILogger<CreateDailyAggregationUseCase> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var targetDate = DateTime.UtcNow.Date.AddDays(-1);
        var startOfDay = targetDate;
        var endOfDay = targetDate.AddDays(1);

        _logger.LogInformation("Starting daily aggregation for {Date}", targetDate);

        var aggregations = await _dbContext.ArticleScores
            .Where(score => score.ScoredAt >= startOfDay && score.ScoredAt < endOfDay)
            .GroupBy(score => score.TickerId)
            .Select(group => new
            {
                TickerId = group.Key,
                AverageScore = group.Average(score => score.Score),
                ArticleCount = group.Count()
            })
            .ToListAsync(cancellationToken);

        if (aggregations.Count == 0)
        {
            _logger.LogInformation("No article scores found for {Date}. Aggregation skipped.", targetDate);
            return;
        }

        var tickerIds = aggregations
            .Select(a => a.TickerId)
            .ToList();

        var existingSummaries = await _dbContext.TickerDailySummaries
            .Where(summary => 
                summary.SummaryDate == targetDate &&
                tickerIds.Contains(summary.TickerId))
            .ToListAsync(cancellationToken);

        foreach (var aggregation in aggregations)
        {
            var existingSummary = existingSummaries
                .FirstOrDefault(summary => summary.TickerId == aggregation.TickerId);

            if (existingSummary != null)
            {
                existingSummary.AverageScore = aggregation.AverageScore;
                existingSummary.ArticleCount = aggregation.ArticleCount;
                existingSummary.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var newSummary = new TickerDailySummary
                {
                    TickerId = aggregation.TickerId,
                    SummaryDate = targetDate,
                    AverageScore = aggregation.AverageScore,
                    ArticleCount = aggregation.ArticleCount,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.TickerDailySummaries.Add(newSummary);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Daily aggregation completed for {Date}. Summaries processed: {Count}",
            targetDate,
            aggregations.Count);
    }
}