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

    public async Task<List<DashboardDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var sevenDaysAgo = today.AddDays(-7);
        var recentArticleCutoff = today.AddDays(-7);

        var tickers = await _dbContext.Ticker
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var todayStats = await _dbContext.ArticleScores
            .AsNoTracking()
            .Where(score => score.ScoredAt >= today && score.ScoredAt < tomorrow)
            .GroupBy(score => score.TickerId)
            .Select(group => new
            {
                TickerId = group.Key,
                AverageSentiment = group.Average(score => score.Score),
                ArticlesForToday = group.Count()
            })
            .ToListAsync(cancellationToken);

        var summaries = await _dbContext.TickerDailySummaries
            .AsNoTracking()
            .Where(summary => summary.SummaryDate >= sevenDaysAgo && summary.SummaryDate < tomorrow)
            .OrderBy(summary => summary.SummaryDate)
            .Select(summary => new
            {
                summary.TickerId,
                Trend = new TickerTrendDto
                {
                    Date = DateOnly.FromDateTime(summary.SummaryDate),
                    AverageScore = summary.AverageScore
                }
            })
            .ToListAsync(cancellationToken);

        var jobCounts = await _dbContext.ScoringJobs
            .AsNoTracking()
            .Where(job =>
                job.StatusId == ScoringJobStatus.Pending ||
                job.StatusId == ScoringJobStatus.Failed)
            .GroupBy(job => new
            {
                job.TickerId,
                job.StatusId
            })
            .Select(group => new
            {
                group.Key.TickerId,
                group.Key.StatusId,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var recentArticlesRaw = await _dbContext.ArticleScores
            .AsNoTracking()
            .Include(score => score.Article)
            .Where(score => score.ScoredAt >= recentArticleCutoff)
            .OrderByDescending(score => score.ScoredAt)
            .Select(score => new
            {
                score.TickerId,
                score.ScoredAt,
                Article = new RecentArticleDto
                {
                    Title = score.Article.Title,
                    SourceName = score.Article.SourceName,
                    PublishedAt = score.Article.PublishedAt,
                    Url = score.Article.Url,
                    Score = score.Score,
                    ScoreLabel = score.ScoreLabel ?? "Neutral",
                    Confidence = score.Confidence
                }
            })
            .ToListAsync(cancellationToken);

        var recentArticlesByTicker = recentArticlesRaw
            .GroupBy(x => x.TickerId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.ScoredAt)
                    .Take(5)
                    .Select(x => x.Article)
                    .ToList());

        var todayStatsByTicker = todayStats
            .ToDictionary(x => x.TickerId);

        var summariesByTicker = summaries
            .GroupBy(x => x.TickerId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.Trend).ToList());

        var pendingJobsByTicker = jobCounts
            .Where(x => x.StatusId == ScoringJobStatus.Pending)
            .ToDictionary(x => x.TickerId, x => x.Count);

        var failedJobsByTicker = jobCounts
            .Where(x => x.StatusId == ScoringJobStatus.Failed)
            .ToDictionary(x => x.TickerId, x => x.Count);

        var dashboardData = new List<DashboardDto>();

        foreach (var ticker in tickers)
        {
            var hasTodayStats = todayStatsByTicker.TryGetValue(ticker.Id, out var tickerTodayStats);

            var averageSentiment = hasTodayStats
                ? tickerTodayStats!.AverageSentiment
                : 0m;

            var articlesForToday = hasTodayStats
                ? tickerTodayStats!.ArticlesForToday
                : 0;

            var tickerTrend = summariesByTicker.TryGetValue(ticker.Id, out var trend)
                ? trend
                : new List<TickerTrendDto>();

            var recentArticles = recentArticlesByTicker.TryGetValue(ticker.Id, out var articles)
                ? articles
                : new List<RecentArticleDto>();

            var pendingJobs = pendingJobsByTicker.TryGetValue(ticker.Id, out var pending)
                ? pending
                : 0;

            var failedJobs = failedJobsByTicker.TryGetValue(ticker.Id, out var failed)
                ? failed
                : 0;

            var dto = new DashboardDto
            {
                StockLabel = ticker.Symbol,
                CompanyName = ticker.CompanyName,
                AverageSentiment = averageSentiment,
                SentimentLabel = GetSentimentLabel(averageSentiment),
                ArticlesForToday = articlesForToday,
                Trend = tickerTrend,
                DailySummaries = tickerTrend,
                RecentArticles = recentArticles,
                PendingJobsCount = pendingJobs,
                FailedJobsCount = failedJobs
            };

            dashboardData.Add(dto);
        }

        return dashboardData;
    }

    private string GetSentimentLabel(decimal averageScore)
    {
        if (averageScore >= 0.2m)
        {
            return "Positive";
        }

        if (averageScore <= -0.2m)
        {
            return "Negative";
        }

        return "Neutral";
    }
}