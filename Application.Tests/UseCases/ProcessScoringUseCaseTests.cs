using Application.DTOs;
using Application.Interfaces;
using Application.Tests.TestInfrastructure;
using Application.UseCases;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases;

public class ProcessScoringUseCaseTests
{
    // Helper to create a mock LLM that returns a batch result matching the input articles by index
    private static Mock<ISentimentLLM> CreateSuccessfulLlm(string label = "Neutral", decimal score = 0.0m)
    {
        var llm = new Mock<ISentimentLLM>();
        llm.Setup(x => x.ScoreArticlesAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ArticleInputDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, IReadOnlyList<ArticleInputDto> articles, CancellationToken _) =>
                articles.Select(a => new SentimentResultDto
                {
                    Index = a.Index,
                    Score = score,
                    ScoreLabel = label,
                    Confidence = 0.9m
                }).ToList());
        return llm;
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidDailyLimit_Throws()
    {
        await using var db = new TestAppDbContext();
        var llm = new Mock<ISentimentLLM>(MockBehavior.Strict);
        var logger = new Mock<ILogger<ProcessScoringUseCase>>();
        var sut = new ProcessScoringUseCase(db, llm.Object, logger.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.ExecuteAsync(0));
    }

    [Fact]
    public async Task ExecuteAsync_WhenLlmSucceeds_CompletesJobAndWritesScore()
    {
        await using var db = new TestAppDbContext();

        var ticker = new Ticker { Id = 1, Symbol = "MSFT", CompanyName = "Microsoft" };
        var article = new Article
        {
            Id = 2, Title = "Title", Description = "Description",
            Url = "https://example.com/a", SourceName = "src",
            CreatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow
        };
        var job = new ScoringJob
        {
            Id = 3, TickerId = 1, ArticleId = 2, StatusId = ScoringJobStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5), Ticker = ticker, Article = article
        };

        db.Ticker.Add(ticker);
        db.Article.Add(article);
        db.ScoringJobs.Add(job);
        await db.SaveChangesAsync();

        var llm = CreateSuccessfulLlm("Positive", 0.7m);
        var logger = new Mock<ILogger<ProcessScoringUseCase>>();
        var sut = new ProcessScoringUseCase(db, llm.Object, logger.Object);

        await sut.ExecuteAsync(dailyLimit: 1, batchSize: 1);

        Assert.Single(db.ArticleScores);
        Assert.Equal(ScoringJobStatus.Completed, db.ScoringJobs.Single().StatusId);
        Assert.Equal(0.7m, db.ArticleScores.Single().Score);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLlmThrows_MarksJobFailed()
    {
        await using var db = new TestAppDbContext();
        var ticker = new Ticker { Id = 1, Symbol = "MSFT", CompanyName = "Microsoft" };
        var article = new Article
        {
            Id = 2, Title = "Title", Description = "Description",
            Url = "https://example.com/a", SourceName = "src",
            CreatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow
        };
        db.Ticker.Add(ticker);
        db.Article.Add(article);
        db.ScoringJobs.Add(new ScoringJob
        {
            Id = 3, TickerId = 1, ArticleId = 2, StatusId = ScoringJobStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5), Ticker = ticker, Article = article
        });
        await db.SaveChangesAsync();

        var llm = new Mock<ISentimentLLM>(MockBehavior.Strict);
        llm.Setup(x => x.ScoreArticlesAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ArticleInputDto>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM unavailable"));

        var logger = new Mock<ILogger<ProcessScoringUseCase>>();
        var sut = new ProcessScoringUseCase(db, llm.Object, logger.Object);

        await sut.ExecuteAsync(dailyLimit: 1, batchSize: 1);

        Assert.Equal(ScoringJobStatus.Failed, db.ScoringJobs.Single().StatusId);
        Assert.Empty(db.ArticleScores);
    }

    [Fact]
    public async Task ExecuteAsync_ReachesDailyLimit_StopsProcessing()
    {
        await using var db = new TestAppDbContext();
        var ticker = new Ticker { Id = 1, Symbol = "T", CompanyName = "Test" };
        db.Ticker.Add(ticker);

        for (int i = 1; i <= 2; i++)
        {
            var art = new Article { Id = i, Title = "T" + i, Url = "U" + i, CreatedAt = DateTime.UtcNow };
            db.Article.Add(art);
            db.ScoringJobs.Add(new ScoringJob
            {
                Id = i, TickerId = 1, ArticleId = i, StatusId = ScoringJobStatus.Pending,
                CreatedAt = DateTime.UtcNow, Ticker = ticker, Article = art
            });
        }
        await db.SaveChangesAsync();

        var llm = CreateSuccessfulLlm();
        var logger = new Mock<ILogger<ProcessScoringUseCase>>();
        var sut = new ProcessScoringUseCase(db, llm.Object, logger.Object);

        // Daily limit of 1 — should only process one job
        await sut.ExecuteAsync(dailyLimit: 1, batchSize: 10);

        Assert.Equal(1, db.ScoringJobs.Count(j => j.StatusId == ScoringJobStatus.Completed));
        Assert.Equal(1, db.ScoringJobs.Count(j => j.StatusId == ScoringJobStatus.Pending));
    }

    [Fact]
    public async Task ExecuteAsync_WhenBatchSizeIsSmallerThanQueue_ProcessesInMultipleSteps()
    {
        await using var db = new TestAppDbContext();
        var ticker = new Ticker { Id = 1, Symbol = "T", CompanyName = "Test" };
        db.Ticker.Add(ticker);

        for (int i = 1; i <= 3; i++)
        {
            var art = new Article { Id = i, Title = "T" + i, Url = "U" + i, CreatedAt = DateTime.UtcNow };
            db.Article.Add(art);
            db.ScoringJobs.Add(new ScoringJob
            {
                Id = i, TickerId = 1, ArticleId = i, StatusId = ScoringJobStatus.Pending,
                CreatedAt = DateTime.UtcNow, Ticker = ticker, Article = art
            });
        }
        await db.SaveChangesAsync();

        var llm = CreateSuccessfulLlm();
        var logger = new Mock<ILogger<ProcessScoringUseCase>>();
        var sut = new ProcessScoringUseCase(db, llm.Object, logger.Object);

        await sut.ExecuteAsync(dailyLimit: 10, batchSize: 2);

        Assert.Equal(3, db.ScoringJobs.Count(j => j.StatusId == ScoringJobStatus.Completed));
    }
}
