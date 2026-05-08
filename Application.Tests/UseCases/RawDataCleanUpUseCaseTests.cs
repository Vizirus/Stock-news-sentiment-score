using Application.Tests.TestInfrastructure;
using Application.UseCases;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases;

public class RawDataCleanUpUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_DeletesOnlyDataOlderThanRetention()
    {
        await using var db = new TestAppDbContext();
        var now = DateTime.UtcNow;
        db.Ticker.Add(new Ticker { Id = 1, Symbol = "AAPL", CompanyName = "Apple" });
        db.Artice.AddRange(
            new Article { Id = 1, Title = "Old", Url = "https://old", SourceName = "s", Description = "d", CreatedAt = now.AddDays(-31), PublishedAt = now.AddDays(-31) },
            new Article { Id = 2, Title = "New", Url = "https://new", SourceName = "s", Description = "d", CreatedAt = now.AddDays(-3), PublishedAt = now.AddDays(-3) });
        db.ScoringJobs.AddRange(
            new ScoringJob { Id = 10, ArticleId = 1, TickerId = 1, CreatedAt = now.AddDays(-31) },
            new ScoringJob { Id = 11, ArticleId = 2, TickerId = 1, CreatedAt = now.AddDays(-3) });
        db.ArticleScores.AddRange(
            new ArticleScore { Id = 20, ArticleId = 1, TickerId = 1, Score = 0.1m, Confidence = 0.5m, ScoredAt = now.AddDays(-31) },
            new ArticleScore { Id = 21, ArticleId = 2, TickerId = 1, Score = 0.2m, Confidence = 0.5m, ScoredAt = now.AddDays(-3) });
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<RawDataCleanUpUseCase>>();
        var sut = new RawDataCleanUpUseCase(db, logger.Object);

        await sut.ExecuteAsync(retentionDays: 30);

        Assert.Single(db.Artice);
        Assert.Single(db.ScoringJobs);
        Assert.Single(db.ArticleScores);
        Assert.Equal("New", db.Artice.Single().Title);
    }
}
