using Application.Tests.TestInfrastructure;
using Application.UseCases;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases;

public class CreateDailyAggregationUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenNoScoresForTargetDate_DoesNotCreateSummary()
    {
        await using var db = new TestAppDbContext();
        var logger = new Mock<ILogger<CreateDailyAggregationUseCase>>();
        var sut = new CreateDailyAggregationUseCase(db, logger.Object);

        await sut.ExecuteAsync();

        Assert.Empty(db.TickerDailySummaries);
    }

    [Fact]
    public async Task ExecuteAsync_UpsertsSummariesForYesterday()
    {
        await using var db = new TestAppDbContext();
        var targetDate = DateTime.UtcNow.Date.AddDays(-1);

        db.Ticker.Add(new Ticker { Id = 1, Symbol = "AAPL", CompanyName = "Apple" });
        db.Article.AddRange(
            new Article { Id = 1, Title = "A1", Description = "d", Url = "https://a1", SourceName = "s", PublishedAt = targetDate, CreatedAt = targetDate },
            new Article { Id = 2, Title = "A2", Description = "d", Url = "https://a2", SourceName = "s", PublishedAt = targetDate, CreatedAt = targetDate });
        db.ArticleScores.AddRange(
            new ArticleScore { TickerId = 1, ArticleId = 1, Score = 0.8m, Confidence = 0.9m, ScoredAt = targetDate.AddHours(2) },
            new ArticleScore { TickerId = 1, ArticleId = 2, Score = 0.2m, Confidence = 0.8m, ScoredAt = targetDate.AddHours(5) });
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<CreateDailyAggregationUseCase>>();
        var sut = new CreateDailyAggregationUseCase(db, logger.Object);
        await sut.ExecuteAsync();

        Assert.Single(db.TickerDailySummaries);
        var summary = db.TickerDailySummaries.Single();
        Assert.Equal(1, summary.TickerId);
        Assert.Equal(targetDate, summary.SummaryDate);
        Assert.Equal(0.5m, summary.AverageScore);
        Assert.Equal(0.5m, summary.AverageScore);
        Assert.Equal(2, summary.ArticleCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleTickers_CreatesSeparateSummaries()
    {
        await using var db = new TestAppDbContext();
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        
        db.Ticker.AddRange(
            new Ticker { Id = 1, Symbol = "A" },
            new Ticker { Id = 2, Symbol = "B" });
        db.Article.Add(new Article { Id = 1, Title = "A", Url = "U" });
        db.ArticleScores.AddRange(
            new ArticleScore { TickerId = 1, ArticleId = 1, Score = 0.5m, ScoredAt = yesterday.AddHours(1) },
            new ArticleScore { TickerId = 2, ArticleId = 1, Score = 1.0m, ScoredAt = yesterday.AddHours(2) });
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<CreateDailyAggregationUseCase>>();
        var sut = new CreateDailyAggregationUseCase(db, logger.Object);
        await sut.ExecuteAsync();

        Assert.Equal(2, db.TickerDailySummaries.Count());
        Assert.Contains(db.TickerDailySummaries, s => s.TickerId == 1 && s.AverageScore == 0.5m);
        Assert.Contains(db.TickerDailySummaries, s => s.TickerId == 2 && s.AverageScore == 1.0m);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRunTwice_UpdatesExistingRecord()
    {
        await using var db = new TestAppDbContext();
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        db.Ticker.Add(new Ticker { Id = 1, Symbol = "A" });
        db.Article.Add(new Article { Id = 1, Title = "A1", Url = "U1" });
        db.Article.Add(new Article { Id = 2, Title = "A2", Url = "U2" });
        await db.SaveChangesAsync();
        
        // Run 1: 1 article score
        db.ArticleScores.Add(new ArticleScore { TickerId = 1, ArticleId = 1, Score = 1.0m, ScoredAt = yesterday.AddHours(1) });
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<CreateDailyAggregationUseCase>>();
        var sut = new CreateDailyAggregationUseCase(db, logger.Object);
        await sut.ExecuteAsync();

        Assert.Equal(1, db.TickerDailySummaries.Single().ArticleCount);

        // Run 2: Another article score added
        db.ArticleScores.Add(new ArticleScore { TickerId = 1, ArticleId = 2, Score = 0.0m, ScoredAt = yesterday.AddHours(2) });
        await db.SaveChangesAsync();

        await sut.ExecuteAsync();

        var summary = db.TickerDailySummaries.Single();
        Assert.Equal(2, summary.ArticleCount);
        Assert.Equal(0.5m, summary.AverageScore);
    }
}
