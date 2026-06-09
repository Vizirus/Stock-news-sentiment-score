using Application.DTOs;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.UseCases;

public class GetDashboardDataUseCase
{
    private readonly IAppDBContext _dbContext;

    public GetDashboardDataUseCase(IAppDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardDto?> ExecuteAsync(string tickerSymbol, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        // Use exact times for the 24-hour pulse view
        var start = startDate;
        var end = endDate;

        var ticker = await _dbContext.Ticker
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Symbol == tickerSymbol, cancellationToken);

        if (ticker == null)
            return null;

        // Stats for the selected period
        var periodScores = await _dbContext.ArticleScores
            .AsNoTracking()
            .Include(score => score.Article)
            .Where(score => score.TickerId == ticker.Id && score.Article.PublishedAt >= start && score.Article.PublishedAt <= end)
            .ToListAsync(cancellationToken);

        var averageSentiment = periodScores.Count > 0 ? periodScores.Average(s => s.Score) : 0m;
        var articlesForPeriod = periodScores.Count;

        var positiveCount = periodScores.Count(s => s.ScoreLabel != null && s.ScoreLabel.Contains("Positive", StringComparison.OrdinalIgnoreCase));
        var negativeCount = periodScores.Count(s => s.ScoreLabel != null && s.ScoreLabel.Contains("Negative", StringComparison.OrdinalIgnoreCase));
        var neutralCount = periodScores.Count(s => s.ScoreLabel != null && s.ScoreLabel.Contains("Neutral", StringComparison.OrdinalIgnoreCase));

        // Daily summaries for the period (for the Daily Summaries table)
        var summaries = await _dbContext.TickerDailySummaries
            .AsNoTracking()
            .Where(summary => summary.TickerId == ticker.Id && summary.SummaryDate >= start.Date && summary.SummaryDate < end.Date.AddDays(1))
            .OrderBy(summary => summary.SummaryDate)
            .Select(summary => new TickerTrendDto
            {
                Date = DateOnly.FromDateTime(summary.SummaryDate),
                AverageScore = summary.AverageScore
            })
            .ToListAsync(cancellationToken);

        // Compute hourly trend using the exact publication time of the articles
        var hourlyTrend = periodScores
            .GroupBy(s => {
                var d = s.Article.PublishedAt;
                return new DateTime(d.Year, d.Month, d.Day, d.Hour, 0, 0, DateTimeKind.Utc);
            })
            .Select(g => new HourlyTrendDto
            {
                Hour = g.Key,
                AverageScore = g.Average(s => s.Score)
            })
            .OrderBy(h => h.Hour)
            .ToList();

        // Job counts
        var pendingJobs = await _dbContext.ScoringJobs
            .AsNoTracking()
            .CountAsync(job => job.TickerId == ticker.Id && job.StatusId == ScoringJobStatus.Pending, cancellationToken);

        var failedJobs = await _dbContext.ScoringJobs
            .AsNoTracking()
            .CountAsync(job => job.TickerId == ticker.Id && job.StatusId == ScoringJobStatus.Failed, cancellationToken);

        // Recent articles for the period
        var recentArticles = await _dbContext.ArticleScores
            .AsNoTracking()
            .Include(score => score.Article)
            .Where(score => score.TickerId == ticker.Id && score.Article.PublishedAt >= start && score.Article.PublishedAt <= end)
            .OrderByDescending(score => score.Article.PublishedAt)
            .Take(10) // Limit to top 10 recent
            .Select(score => new RecentArticleDto
            {
                Title = score.Article.Title,
                SourceName = score.Article.SourceName,
                PublishedAt = score.Article.PublishedAt,
                Url = score.Article.Url,
                Score = score.Score,
                ScoreLabel = score.ScoreLabel ?? "Neutral",
                Confidence = score.Confidence
            })
            .ToListAsync(cancellationToken);

        var dto = new DashboardDto
        {
            StockLabel = ticker.Symbol,
            CompanyName = ticker.CompanyName,
            AverageSentiment = averageSentiment,
            SentimentLabel = GetSentimentLabel(averageSentiment),
            ArticlesForToday = articlesForPeriod, 
            HourlyTrend = hourlyTrend,
            DailySummaries = summaries,
            RecentArticles = recentArticles,
            PendingJobsCount = pendingJobs,
            FailedJobsCount = failedJobs,
            PositiveArticlesCount = positiveCount,
            NegativeArticlesCount = negativeCount,
            NeutralArticlesCount = neutralCount
        };

        return dto;
    }

    private string GetSentimentLabel(decimal averageScore)
    {
        if (averageScore >= 0.8m)
            return "Very Positive";

        if (averageScore >= 0.2m)
            return "Positive";

        if (averageScore <= -0.8m)
            return "Very Negative";

        if (averageScore <= -0.2m)
            return "Negative";

        return "Neutral";
    }
}