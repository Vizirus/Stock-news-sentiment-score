using Application.Tests.TestInfrastructure;
using Application.UseCases;
using Domain.Entities;
using Domain.Enums;

namespace Application.Tests.UseCases;

public class GetDashboardDataUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenNoData_ReturnsTickerWithDefaults()
    {
        await using var db = new TestAppDbContext();
        db.Ticker.Add(new Ticker { Id = 1, Symbol = "TSLA", CompanyName = "Tesla" });
        await db.SaveChangesAsync();

        var sut = new GetDashboardDataUseCase(db);
        var result = await sut.ExecuteAsync("TSLA", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        Assert.NotNull(result);
        Assert.Equal("TSLA", result.StockLabel);
        Assert.Equal(0m, result.AverageSentiment);
        Assert.Equal("Neutral", result.SentimentLabel);
        Assert.Equal(0, result.ArticlesForToday);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRecentArticlesAndJobCounts()
    {
        await using var db = new TestAppDbContext();
        var now = DateTime.UtcNow;
        db.Ticker.Add(new Ticker { Id = 1, Symbol = "TSLA", CompanyName = "Tesla" });
        db.Article.Add(new Article
        {
            Id = 2,
            Title = "Title",
            Description = "Desc",
            Url = "https://example.com/1",
            SourceName = "Source",
            PublishedAt = now,
            CreatedAt = now
        });
        db.ArticleScores.Add(new ArticleScore
        {
            Id = 3,
            TickerId = 1,
            ArticleId = 2,
            Score = 0.7m,
            ScoreLabel = "Positive",
            Confidence = 0.95m,
            ScoredAt = now
        });
        db.ScoringJobs.AddRange(
            new ScoringJob { Id = 4, TickerId = 1, ArticleId = 2, StatusId = ScoringJobStatus.Pending, CreatedAt = now },
            new ScoringJob { Id = 5, TickerId = 1, ArticleId = 2, StatusId = ScoringJobStatus.Failed, CreatedAt = now });
        await db.SaveChangesAsync();

        var sut = new GetDashboardDataUseCase(db);
        var result = await sut.ExecuteAsync("TSLA", now.AddDays(-1), now.AddDays(1));

        Assert.NotNull(result);
        Assert.Equal("Positive", result.SentimentLabel);
        Assert.Single(result.RecentArticles);
        Assert.Equal(1, result.PendingJobsCount);
        Assert.Equal(1, result.FailedJobsCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoTickersInDb_ReturnsNull()
    {
        await using var db = new TestAppDbContext();
        var sut = new GetDashboardDataUseCase(db);
        var result = await sut.ExecuteAsync("NONEXISTENT", DateTime.UtcNow, DateTime.UtcNow);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_Throws()
    {
        await using var db = new TestAppDbContext();
        db.Ticker.Add(new Ticker { Id = 1, Symbol = "AAPL" });
        await db.SaveChangesAsync();

        var sut = new GetDashboardDataUseCase(db);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ExecuteAsync("AAPL", DateTime.UtcNow, DateTime.UtcNow, cts.Token));
    }
}
