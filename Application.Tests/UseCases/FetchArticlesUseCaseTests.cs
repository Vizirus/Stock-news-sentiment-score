using Application.DTOs;
using Application.Interfaces;
using Application.Tests.TestInfrastructure;
using Application.UseCases;
using Domain.Entities;
using Domain.Enums;
using Moq;

namespace Application.Tests.UseCases;

public class FetchArticlesUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_AddsOnlyNonDuplicateArticles_AndPendingJobs()
    {
        await using var db = new TestAppDbContext();
        db.Ticker.Add(new Ticker { Id = 1, Symbol = "AAPL", CompanyName = "Apple" });
        db.Artice.Add(new Article
        {
            Id = 10,
            Title = "Existing title",
            Description = "desc",
            Url = "https://example.com/existing",
            SourceName = "src",
            PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var newsApi = new Mock<INewsAPI>(MockBehavior.Strict);
        newsApi.Setup(x => x.GetArticlesForCompany("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FetchedArticleDto>
            {
                new() { Title = "Existing title", Url = "https://example.com/new-1", SourceName = "src", CreatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow },
                new() { Title = "Unique title", Url = "https://example.com/new-2", SourceName = "src", CreatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow },
            });

        var sut = new FetchArticlesUseCase(db, newsApi.Object);

        await sut.ExecuteAsync();

        Assert.Equal(2, db.Artice.Count());
        Assert.Single(db.ScoringJobs);
        Assert.Equal(ScoringJobStatus.Pending, db.ScoringJobs.Single().StatusId);
        Assert.Equal(1, db.ScoringJobs.Single().TickerId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewsApiThrows_PropagatesException()
    {
        await using var db = new TestAppDbContext();
        db.Ticker.Add(new Ticker { Id = 1, Symbol = "AAPL", CompanyName = "Apple" });
        await db.SaveChangesAsync();

        var newsApi = new Mock<INewsAPI>(MockBehavior.Strict);
        newsApi.Setup(x => x.GetArticlesForCompany("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("api failed"));

        var sut = new FetchArticlesUseCase(db, newsApi.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync());
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateTitleInDbButNewUrl_ShouldBeSkipped()
    {
        await using var db = new TestAppDbContext();
        db.Ticker.Add(new Ticker { Id = 1, Symbol = "MSFT", CompanyName = "Microsoft" });
        db.Artice.Add(new Article { Title = "Duplicate Title", Url = "https://old.com" });
        await db.SaveChangesAsync();

        var newsApi = new Mock<INewsAPI>();
        newsApi.Setup(x => x.GetArticlesForCompany("MSFT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FetchedArticleDto> 
            { 
                new() { Title = "Duplicate Title", Url = "https://new.com", PublishedAt = DateTime.UtcNow } 
            });

        var sut = new FetchArticlesUseCase(db, newsApi.Object);
        await sut.ExecuteAsync();

        // Should still be only 1 article (the existing one)
        Assert.Single(db.Artice);
        Assert.Empty(db.ScoringJobs);
    }

    [Fact]
    public async Task ExecuteAsync_ApiReturnsEmptyList_DoesNothing()
    {
        await using var db = new TestAppDbContext();
        db.Ticker.Add(new Ticker { Id = 1, Symbol = "GOOGL", CompanyName = "Google" });
        await db.SaveChangesAsync();

        var newsApi = new Mock<INewsAPI>();
        newsApi.Setup(x => x.GetArticlesForCompany("GOOGL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FetchedArticleDto>());

        var sut = new FetchArticlesUseCase(db, newsApi.Object);
        await sut.ExecuteAsync();

        Assert.Empty(db.Artice);
        Assert.Empty(db.ScoringJobs);
    }
}
