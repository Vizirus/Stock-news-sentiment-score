using Application.Tests.TestInfrastructure;
using Application.UseCases;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases;

public class SummaryDataCleanUpUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_DeletesOnlyOldSummaries()
    {
        await using var db = new TestAppDbContext();
        var now = DateTime.UtcNow;
        db.Ticker.Add(new Ticker { Id = 1, Symbol = "AAPL", CompanyName = "Apple" });

        db.TickerDailySummaries.AddRange(
            new TickerDailySummary { Id = 1, TickerId = 1, SummaryDate = now.AddDays(-400), AverageScore = 0.2m, ArticleCount = 10, UpdatedAt = now },
            new TickerDailySummary { Id = 2, TickerId = 1, SummaryDate = now.AddDays(-10), AverageScore = 0.3m, ArticleCount = 8, UpdatedAt = now });
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<SummaryDataCleanUpUseCase>>();
        var sut = new SummaryDataCleanUpUseCase(db, logger.Object);

        await sut.ExecuteAsync(retentionDays: 365);

        Assert.Single(db.TickerDailySummaries);
        Assert.Equal(2, db.TickerDailySummaries.Single().Id);
    }
}
