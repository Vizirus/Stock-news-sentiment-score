using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.UseCases;

public class RawDataCleanUpUseCase
{
    private readonly IAppDBContext _dbContext;
    private readonly ILogger<RawDataCleanUpUseCase> _logger;

    public RawDataCleanUpUseCase(IAppDBContext dbContext, ILogger<RawDataCleanUpUseCase> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ExecuteAsync(int retentionDays = 30, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retentionDays);
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        _logger.LogInformation("Starting Raw Data Cleanup. Deleting records older than {CutoffDate}", cutoffDate);

        // 1. Delete old Scoring Jobs
        var oldJobsCount = await _dbContext.ScoringJobs
            .Where(j => j.CreatedAt < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        // 2. Delete old Article Scores
        var oldScoresCount = await _dbContext.ArticleScores
            .Where(s => s.ScoredAt < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        // 3. Delete old Articles
        // Since Articles are the parent of ScoringJobs and ArticleScores, those child records 
        // will be cascade-deleted by the DB if cascading is set up, but we explicitly deleted them above 
        // to be safe and free up space cleanly.
        var oldArticlesCount = await _dbContext.Artice
            .Where(a => a.CreatedAt < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Raw Data Cleanup completed. Deleted {Jobs} Jobs, {Scores} Scores, {Articles} Articles.", oldJobsCount, oldScoresCount, oldArticlesCount);
    }
}
