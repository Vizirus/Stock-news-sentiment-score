using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.UseCases;

public class ProcessScoringUseCase
{
    private readonly IAppDBContext _dbContext;
    private readonly ISentimentLLM _sentimentLlm;
    private readonly ILogger<ProcessScoringUseCase> _logger;

    public ProcessScoringUseCase(
        IAppDBContext dbContext, 
        ISentimentLLM sentimentLlm, 
        ILogger<ProcessScoringUseCase> logger)
    {
        _dbContext = dbContext;
        _sentimentLlm = sentimentLlm;
        _logger = logger;
    }

    public async Task ExecuteAsync(int dailyLimit, int batchSize = 20, CancellationToken cancellationToken = default)
    {
        if (dailyLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyLimit), "Daily limit must be greater than zero.");
        }

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
        }
        var today = DateTime.UtcNow.Date;
        
        var processedToday = await _dbContext.ScoringJobs
            .Where(j =>
                j.StartedAt >= today &&
                (j.StatusId == ScoringJobStatus.Completed ||
                 j.StatusId == ScoringJobStatus.Failed))
            .CountAsync(cancellationToken);

        _logger.LogInformation("Starting ProcessScoringUseCase. Processed today: {Count}/{Limit}", processedToday, dailyLimit);

        while (!cancellationToken.IsCancellationRequested && processedToday < dailyLimit)
        {
            var remainingQuota = dailyLimit - processedToday;
            var currentBatchSize = Math.Min(batchSize, remainingQuota);

            var jobs = await _dbContext.ScoringJobs
                .Include(j => j.Article)
                .Include(j => j.Ticker)
                .Where(j => j.StatusId == ScoringJobStatus.Pending)
                .OrderBy(j => j.CreatedAt)
                .Take(currentBatchSize)
                .ToListAsync(cancellationToken);

            if (jobs.Count == 0)
            {
                _logger.LogInformation("No more pending scoring jobs. Finishing loop.");
                break;
            }

            _logger.LogInformation("Fetched {Count} pending jobs. Processing batch...", jobs.Count);

            foreach (var job in jobs)
            {
                try
                {
                    job.StartedAt = DateTime.UtcNow;

                    // Note: Your ISentimentLLM expects Ticker object properties and Url.
                    // If Ticker or Article is null (due to nullable navigation properties), we handle it.
                    if (job.Ticker == null)
                    {
                        job.StatusId = ScoringJobStatus.Failed;
                        job.ErrorMessage = "Ticker data is missing.";
                        job.CompletdAt = DateTime.UtcNow;
                        continue;
                    }
                    if (job.Article == null)
                    {
                        job.StatusId = ScoringJobStatus.Failed;
                        job.ErrorMessage = "Article data is missing.";
                        job.CompletdAt = DateTime.UtcNow;
                        continue;
                    }
                    var tickerSymbol = job.Ticker.Symbol;
                    var companyName = job.Ticker.CompanyName;
                    var title = job.Article.Title;
                    var description = job.Article.Description;
                    var url = job.Article.Url;

                    var result = await _sentimentLlm.ScoreArticles(
                        tickerSymbol, 
                        companyName, 
                        title, 
                        description, 
                        url, 
                        cancellationToken);

                    var articleScore = new ArticleScore
                    {
                        ArticleId = job.ArticleId,
                        TickerId = job.TickerId,
                        Score = result.Score,
                        ScoreLabel = result.ScoreLabel, // DTO and Entity are now both strings!
                        Confidence = result.Confidence,
                        ScoredAt = DateTime.UtcNow
                    };

                    _dbContext.ArticleScores.Add(articleScore);

                    job.StatusId = ScoringJobStatus.Completed;
                    job.CompletdAt = DateTime.UtcNow; // Using your modified typo property 'CompltedAt'
                    
                    processedToday++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to score job {JobId}", job.Id);
                    
                    job.StatusId = ScoringJobStatus.Failed;
                    job.ErrorMessage = "An unknow error happend during the program execution! \nSomething happend while scoring the articles";
                    job.CompletdAt = DateTime.UtcNow;
                    
                    processedToday++; 
                }
            }

            // Await the new parameterless SaveChangesAsync Task signature
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Batch saved successfully. Processed today: {Count}/{Limit}", processedToday, dailyLimit);
        }

        if (processedToday >= dailyLimit)
        {
            _logger.LogWarning("Daily LLM limit of {Limit} reached. Halting scoring.", dailyLimit);
        }
    }
}
