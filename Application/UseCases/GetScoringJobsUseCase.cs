using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.UseCases;

public class GetScoringJobsUseCase
{
    private readonly IAppDBContext _dbContext;

    public GetScoringJobsUseCase(IAppDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ScoringJobsResultDto> ExecuteAsync(
        string? ticker = null,
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? label = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var start = startDate?.Date ?? DateTime.UtcNow.Date.AddDays(-7);
        var end = endDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

        var query = _dbContext.ScoringJobs
            .AsNoTracking()
            .Where(j => j.CreatedAt >= start && j.CreatedAt <= end);

        if (!string.IsNullOrEmpty(ticker) && ticker != "All Tickers")
            query = query.Where(j => j.Ticker!.Symbol == ticker);

        if (!string.IsNullOrEmpty(status) && status != "all" && status != "All Statuses")
        {
            if (Enum.TryParse<Domain.Enums.ScoringJobStatus>(status, true, out var parsedStatus))
                query = query.Where(j => j.StatusId == parsedStatus);
        }

        var joinedQuery = query.Select(j => new
        {
            Job = j,
            Score = _dbContext.ArticleScores.FirstOrDefault(a => a.ArticleId == j.ArticleId && a.TickerId == j.TickerId)
        });

        if (!string.IsNullOrEmpty(label) && label != "All Labels")
            joinedQuery = joinedQuery.Where(x => x.Score != null && x.Score.ScoreLabel == label);

        var totalCount = await joinedQuery.CountAsync(cancellationToken);
        
        var pendingCount = await joinedQuery.CountAsync(x => x.Job.StatusId == Domain.Enums.ScoringJobStatus.Pending, cancellationToken);
        var failedCount = await joinedQuery.CountAsync(x => x.Job.StatusId == Domain.Enums.ScoringJobStatus.Failed, cancellationToken);
        var completedCount = await joinedQuery.CountAsync(x => x.Job.StatusId == Domain.Enums.ScoringJobStatus.Completed, cancellationToken);

        var items = await joinedQuery
            .OrderByDescending(x => x.Job.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ScoringJobResultItemDto
            {
                Id = x.Job.Id,
                Ticker = x.Job.Ticker!.Symbol,
                ArticleTitle = x.Job.Article != null ? x.Job.Article.Title : "Unknown Article",
                CreatedAt = x.Job.CreatedAt,
                StartedAt = x.Job.StartedAt == default ? null : x.Job.StartedAt,
                CompletedAt = x.Job.CompletdAt == default ? null : x.Job.CompletdAt,
                Status = x.Job.StatusId,
                ErrorMessage = x.Job.ErrorMessage,
                Score = x.Score != null ? x.Score.Score : null,
                Label = x.Score != null ? x.Score.ScoreLabel : (x.Job.StatusId == Domain.Enums.ScoringJobStatus.Completed ? "Irrelevant" : null)
            })
            .ToListAsync(cancellationToken);

        return new ScoringJobsResultDto
        {
            Jobs = items,
            TotalCount = totalCount,
            TotalPending = pendingCount,
            TotalFailed = failedCount,
            TotalCompleted = completedCount
        };
    }
}

public class ScoringJobResultItemDto
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string ArticleTitle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Domain.Enums.ScoringJobStatus Status { get; set; }
    public decimal? Score { get; set; }
    public string? Label { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ScoringJobsResultDto
{
    public List<ScoringJobResultItemDto> Jobs { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPending { get; set; }
    public int TotalFailed { get; set; }
    public int TotalCompleted { get; set; }
}
