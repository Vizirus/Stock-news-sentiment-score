using Application.DTOs;
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
            throw new ArgumentOutOfRangeException(nameof(dailyLimit), "Daily limit must be greater than zero.");

        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");

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

            // --- Validate jobs before sending to LLM ---
            var now = DateTime.UtcNow;
            var validJobs = new List<ScoringJob>();

            foreach (var job in jobs)
            {
                job.StartedAt = now;

                if (job.Ticker == null)
                {
                    job.StatusId = ScoringJobStatus.Failed;
                    job.ErrorMessage = "Ticker navigation property is missing.";
                    job.CompletdAt = now;
                    processedToday++;
                    continue;
                }

                if (job.Article == null)
                {
                    job.StatusId = ScoringJobStatus.Failed;
                    job.ErrorMessage = "Article navigation property is missing.";
                    job.CompletdAt = now;
                    processedToday++;
                    continue;
                }

                validJobs.Add(job);
            }

            if (validJobs.Count == 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            // --- Group valid jobs by Ticker for batch LLM calls ---
            var jobsByTicker = validJobs.GroupBy(j => j.TickerId);

            foreach (var tickerGroup in jobsByTicker)
            {
                var tickerJobs = tickerGroup.ToList();
                var ticker = tickerJobs[0].Ticker!;

                // Build the input batch with an Index for result matching
                var articleInputs = tickerJobs
                    .Select((j, i) => new ArticleInputDto
                    {
                        Index = i,
                        Title = j.Article!.Title,
                        Description = j.Article.Description
                    })
                    .ToList();

                // Wait 4 seconds to guarantee we stay under the 15 RPM Free Tier limit
                await Task.Delay(4000, cancellationToken);

                int maxRetries = 3;
                int currentRetry = 0;
                bool success = false;

                while (!success && currentRetry < maxRetries)
                {
                    try
                    {
                        var results = await _sentimentLlm.ScoreArticlesAsync(
                            ticker.Symbol,
                            ticker.CompanyName,
                            articleInputs,
                            cancellationToken);

                        // Match results back to jobs by Index
                        var resultByIndex = results.ToDictionary(r => r.Index);

                        for (int i = 0; i < tickerJobs.Count; i++)
                        {
                            var job = tickerJobs[i];

                            if (!resultByIndex.TryGetValue(i, out var result))
                            {
                                job.StatusId = ScoringJobStatus.Failed;
                                job.ErrorMessage = $"LLM did not return a result for article at index {i}.";
                                job.CompletdAt = DateTime.UtcNow;
                                processedToday++;
                                continue;
                            }

                            // Only save scores for articles that are actually relevant to the company
                            if (!string.Equals(result.ScoreLabel, "Irrelevant", StringComparison.OrdinalIgnoreCase))
                            {
                                _dbContext.ArticleScores.Add(new ArticleScore
                                {
                                    ArticleId = job.ArticleId,
                                    TickerId = job.TickerId,
                                    Score = result.Score,
                                    ScoreLabel = result.ScoreLabel,
                                    Confidence = result.Confidence,
                                    ScoredAt = DateTime.UtcNow
                                });
                            }
                            else
                            {
                                _logger.LogInformation("Discarded irrelevant article '{Title}' for ticker {Ticker}", job.Article?.Title, ticker.Symbol);
                            }

                            job.StatusId = ScoringJobStatus.Completed;
                            job.CompletdAt = DateTime.UtcNow;
                            processedToday++;
                        }
                        
                        success = true;
                    }
                    catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                    {
                        currentRetry++;
                        _logger.LogWarning("Rate limit 429 hit for ticker {Ticker}. Waiting 60 seconds before retry {Retry}/{Max}...", ticker.Symbol, currentRetry, maxRetries);
                        
                        if (currentRetry >= maxRetries)
                        {
                            _logger.LogError(ex, "Failed to score batch for ticker {Ticker} after {Max} retries.", ticker.Symbol, maxRetries);
                            foreach (var job in tickerJobs)
                            {
                                job.StatusId = ScoringJobStatus.Failed;
                                job.ErrorMessage = $"Batch scoring failed after {maxRetries} rate limit retries: {ex.Message}";
                                job.CompletdAt = DateTime.UtcNow;
                                processedToday++;
                            }
                        }
                        else
                        {
                            await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to score batch for ticker {Ticker}", ticker.Symbol);

                        foreach (var job in tickerJobs)
                        {
                            job.StatusId = ScoringJobStatus.Failed;
                            job.ErrorMessage = $"Batch scoring failed: {ex.Message}";
                            job.CompletdAt = DateTime.UtcNow;
                            processedToday++;
                        }
                        break; // break retry loop for non-429 errors
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Batch saved. Processed today: {Count}/{Limit}", processedToday, dailyLimit);
        }

        if (processedToday >= dailyLimit)
            _logger.LogWarning("Daily LLM limit of {Limit} reached. Halting scoring.", dailyLimit);
    }
}
