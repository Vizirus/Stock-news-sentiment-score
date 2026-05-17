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
        // Normalize dates
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1); // Include the entire end date

        var ticker = await _dbContext.Ticker
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Symbol == tickerSymbol, cancellationToken);

        if (ticker == null)
            return null;

        // Stats for the selected period
        var periodScores = await _dbContext.ArticleScores
            .AsNoTracking()
            .Where(score => score.TickerId == ticker.Id && score.ScoredAt >= start && score.ScoredAt < end)
            .ToListAsync(cancellationToken);

        var averageSentiment = periodScores.Count > 0 ? periodScores.Average(s => s.Score) : 0m;
        var articlesForPeriod = periodScores.Count;

        // Daily summaries for the period
        var summaries = await _dbContext.TickerDailySummaries
            .AsNoTracking()
            .Where(summary => summary.TickerId == ticker.Id && summary.SummaryDate >= start && summary.SummaryDate < end)
            .OrderBy(summary => summary.SummaryDate)
            .Select(summary => new TickerTrendDto
            {
                Date = DateOnly.FromDateTime(summary.SummaryDate),
                AverageScore = summary.AverageScore
            })
            .ToListAsync(cancellationToken);

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
            .Where(score => score.TickerId == ticker.Id && score.ScoredAt >= start && score.ScoredAt < end)
            .OrderByDescending(score => score.ScoredAt)
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
            ArticlesForToday = articlesForPeriod, // Maps to the chosen period now
            Trend = summaries,
            DailySummaries = summaries,
            RecentArticles = recentArticles,
            PendingJobsCount = pendingJobs,
            FailedJobsCount = failedJobs
        };

        return dto;
    }

    private string GetSentimentLabel(decimal averageScore)
    {
        if (averageScore >= 0.2m)
            return "Positive";

        if (averageScore <= -0.2m)
            return "Negative";

        return "Neutral";
    }
}