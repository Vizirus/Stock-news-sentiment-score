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
        var result = await sut.ExecuteAsync();

        Assert.Single(result);
        Assert.Equal("TSLA", result[0].StockLabel);
        Assert.Equal(0m, result[0].AverageSentiment);
        Assert.Equal("Neutral", result[0].SentimentLabel);
        Assert.Equal(0, result[0].ArticlesForToday);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRecentArticlesAndJobCounts()
    {
        await using var db = new TestAppDbContext();
        var now = DateTime.UtcNow;
        db.Ticker.Add(new Ticker { Id = 1, Symbol = "TSLA", CompanyName = "Tesla" });
        db.Artice.Add(new Article
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
        var result = await sut.ExecuteAsync();

        Assert.Single(result);
        Assert.Equal("Positive", result[0].SentimentLabel);
        Assert.Single(result[0].RecentArticles);
        Assert.Equal(1, result[0].PendingJobsCount);
        Assert.Equal(1, result[0].PendingJobsCount);
        Assert.Equal(1, result[0].FailedJobsCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoTickersInDb_ReturnsEmptyList()
    {
        await using var db = new TestAppDbContext();
        var sut = new GetDashboardDataUseCase(db);
        var result = await sut.ExecuteAsync();

        Assert.Empty(result);
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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ExecuteAsync(cts.Token));
    }
}
