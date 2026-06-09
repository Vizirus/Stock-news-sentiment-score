using Application.Interfaces;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.UseCases;

public class RetryAllFailedJobsUseCase
{
    private readonly IAppDBContext _dbContext;
    private readonly IJobTriggerService _jobTriggerService;

    public RetryAllFailedJobsUseCase(IAppDBContext dbContext, IJobTriggerService jobTriggerService)
    {
        _dbContext = dbContext;
        _jobTriggerService = jobTriggerService;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var failedJobs = await _dbContext.ScoringJobs
            .Where(j => j.StatusId == ScoringJobStatus.Failed)
            .ToListAsync(cancellationToken);

        if (failedJobs.Count == 0) return;

        foreach (var job in failedJobs)
        {
            job.StatusId = ScoringJobStatus.Pending;
            job.ErrorMessage = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Signal background worker
        await _jobTriggerService.TriggerScoringJobAsync();
    }
}
