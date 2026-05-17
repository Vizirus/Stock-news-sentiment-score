using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.UseCases;

public class GetScoringJobsUseCase
{
    private readonly IAppDBContext _dbContext;

    public GetScoringJobsUseCase(IAppDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ScoringJobsResultDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var pendingCount = await _dbContext.ScoringJobs.CountAsync(j => j.StatusId == Domain.Enums.ScoringJobStatus.Pending, cancellationToken);
        var failedCount = await _dbContext.ScoringJobs.CountAsync(j => j.StatusId == Domain.Enums.ScoringJobStatus.Failed, cancellationToken);
        var completedCount = await _dbContext.ScoringJobs.CountAsync(j => j.StatusId == Domain.Enums.ScoringJobStatus.Completed, cancellationToken);

        var jobs = await _dbContext.ScoringJobs
            .AsNoTracking()
            .Include(j => j.Ticker)
            .Include(j => j.Article)
            .OrderByDescending(j => j.CreatedAt)
            .Take(100) // Limit to top 100 for now to prevent massive payloads
            .ToListAsync(cancellationToken);

        return new ScoringJobsResultDto
        {
            Jobs = jobs,
            TotalPending = pendingCount,
            TotalFailed = failedCount,
            TotalCompleted = completedCount
        };
    }
}

public class ScoringJobsResultDto
{
    public List<ScoringJob> Jobs { get; set; } = new();
    public int TotalPending { get; set; }
    public int TotalFailed { get; set; }
    public int TotalCompleted { get; set; }
}
