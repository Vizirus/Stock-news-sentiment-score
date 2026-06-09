using Application.Interfaces;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.UseCases;

public class RetryFailedJobsUseCase
{
    private readonly IAppDBContext _dbContext;
    private readonly IJobTriggerService _jobTrigger;

    public RetryFailedJobsUseCase(IAppDBContext dbContext, IJobTriggerService jobTrigger)
    {
        _dbContext = dbContext;
        _jobTrigger = jobTrigger;
    }

    public async Task<int> ExecuteAsync(string tickerSymbol, CancellationToken cancellationToken = default)
    {
        var ticker = await _dbContext.Ticker
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Symbol == tickerSymbol, cancellationToken);

        if (ticker == null)
            return 0;

        // Fetch up to 10 failed jobs for the ticker
        var failedJobs = await _dbContext.ScoringJobs
            .Where(j => j.TickerId == ticker.Id && j.StatusId == ScoringJobStatus.Failed)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (failedJobs.Count == 0)
            return 0;

        foreach (var job in failedJobs)
        {
            job.StatusId = ScoringJobStatus.Pending;
            job.ErrorMessage = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Notify the background worker to wake up and start processing
        await _jobTrigger.TriggerScoringJobAsync();

        return failedJobs.Count;
    }
}
