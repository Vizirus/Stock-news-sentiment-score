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
            Id = 2,
            Title = "Title",
            Description = "Description",
            Url = "https://example.com/a",
            SourceName = "src",
            CreatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow
        };
        var job = new ScoringJob
        {
            Id = 3,
            TickerId = 1,
            ArticleId = 2,
            StatusId = ScoringJobStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            Ticker = ticker,
            Article = article
        };

        db.Ticker.Add(ticker);
        db.Artice.Add(article);
        db.ScoringJobs.Add(job);
        await db.SaveChangesAsync();

        var llm = new Mock<ISentimentLLM>(MockBehavior.Strict);
        llm.Setup(x => x.ScoreArticles("MSFT", "Microsoft", "Title", "Description", "https://example.com/a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SentimentResultDto { Score = 0.7m, ScoreLabel = "Positive", Confidence = 0.88m });

        var logger = new Mock<ILogger<ProcessScoringUseCase>>();
        var sut = new ProcessScoringUseCase(db, llm.Object, logger.Object);

        await sut.ExecuteAsync(dailyLimit: 1, batchSize: 1);

        Assert.Single(db.ArticleScores);
        Assert.Equal(ScoringJobStatus.Completed, db.ScoringJobs.Single().StatusId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLlmThrows_MarksJobFailed()
    {
        await using var db = new TestAppDbContext();
        var ticker = new Ticker { Id = 1, Symbol = "MSFT", CompanyName = "Microsoft" };
        var article = new Article
        {
            Id = 2,
            Title = "Title",
            Description = "Description",
            Url = "https://example.com/a",
            SourceName = "src",
            CreatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow
        };
        db.Ticker.Add(ticker);
        db.Artice.Add(article);
        db.ScoringJobs.Add(new ScoringJob
        {
            Id = 3,
            TickerId = 1,
            ArticleId = 2,
            StatusId = ScoringJobStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            Ticker = ticker,
            Article = article
        });
        await db.SaveChangesAsync();

        var llm = new Mock<ISentimentLLM>(MockBehavior.Strict);
        llm.Setup(x => x.ScoreArticles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
        
        // Add 2 jobs
        for(int i = 1; i <= 2; i++)
        {
            var art = new Article { Id = i, Title = "T"+i, Url = "U"+i, CreatedAt = DateTime.UtcNow };
            db.Artice.Add(art);
            db.ScoringJobs.Add(new ScoringJob 
            { 
                Id = i, TickerId = 1, ArticleId = i, StatusId = ScoringJobStatus.Pending, 
                CreatedAt = DateTime.UtcNow, Ticker = ticker, Article = art 
            });
        }
        await db.SaveChangesAsync();

        var llm = new Mock<ISentimentLLM>();
        llm.Setup(x => x.ScoreArticles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SentimentResultDto { ScoreLabel = "Neutral" });

        var logger = new Mock<ILogger<ProcessScoringUseCase>>();
        var sut = new ProcessScoringUseCase(db, llm.Object, logger.Object);

        // Daily limit of 1
        await sut.ExecuteAsync(dailyLimit: 1, batchSize: 10);

        // Should have 1 completed and 1 pending
        Assert.Equal(1, db.ScoringJobs.Count(j => j.StatusId == ScoringJobStatus.Completed));
        Assert.Equal(1, db.ScoringJobs.Count(j => j.StatusId == ScoringJobStatus.Pending));
    }

    [Fact]
    public async Task ExecuteAsync_WhenBatchSizeIsSmallerThanQueue_ProcessesInMultipleSteps()
    {
        await using var db = new TestAppDbContext();
        var ticker = new Ticker { Id = 1, Symbol = "T", CompanyName = "Test" };
        db.Ticker.Add(ticker);
        
        // Add 3 jobs
        for(int i = 1; i <= 3; i++)
        {
            var art = new Article { Id = i, Title = "T"+i, Url = "U"+i, CreatedAt = DateTime.UtcNow };
            db.Artice.Add(art);
            db.ScoringJobs.Add(new ScoringJob 
            { 
                Id = i, TickerId = 1, ArticleId = i, StatusId = ScoringJobStatus.Pending, 
                CreatedAt = DateTime.UtcNow, Ticker = ticker, Article = art 
            });
        }
        await db.SaveChangesAsync();

        var llm = new Mock<ISentimentLLM>();
        llm.Setup(x => x.ScoreArticles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SentimentResultDto { ScoreLabel = "Neutral" });

        var logger = new Mock<ILogger<ProcessScoringUseCase>>();
        var sut = new ProcessScoringUseCase(db, llm.Object, logger.Object);

        // Batch size 2. Should process 2, then 1.
        await sut.ExecuteAsync(dailyLimit: 10, batchSize: 2);

        Assert.Equal(3, db.ScoringJobs.Count(j => j.StatusId == ScoringJobStatus.Completed));
    }
}
