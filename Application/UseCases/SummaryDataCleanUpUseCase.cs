using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.UseCases;

public class SummaryDataCleanUpUseCase
{
    private readonly IAppDBContext _dbContext;
    private readonly ILogger<SummaryDataCleanUpUseCase> _logger;

    public SummaryDataCleanUpUseCase(IAppDBContext dbContext, ILogger<SummaryDataCleanUpUseCase> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ExecuteAsync(int retentionDays = 365, CancellationToken cancellationToken = default)
    {
        // By default, we keep summaries for 1 year, as they take up very little space
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retentionDays);
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        _logger.LogInformation("Starting Summary Data Cleanup. Deleting summaries older than {CutoffDate}", cutoffDate);

        var deletedCount = await _dbContext.TickerDailySummaries
            .Where(s => s.SummaryDate < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Summary Data Cleanup completed. Deleted {Count} old summaries.", deletedCount);
    }
}
