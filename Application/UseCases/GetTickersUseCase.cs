using Application.DTOs;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Application.UseCases;

public class GetTickersUseCase
{
    private readonly IAppDBContext _dbContext;

    public GetTickersUseCase(IAppDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TickerSummaryDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var tickers = await _dbContext.Ticker
            .AsNoTracking()
            .OrderBy(t => t.Symbol)
            .ToListAsync(ct);

        var result = new List<TickerSummaryDto>();

        foreach (var ticker in tickers)
        {
            // Get all scores to calculate avg
            var scores = await _dbContext.ArticleScores
                .AsNoTracking()
                .Where(s => s.TickerId == ticker.Id)
                .OrderByDescending(s => s.ScoredAt)
                .ToListAsync(ct);

            // Get last daily summaries to determine trend
            var summaries = await _dbContext.TickerDailySummaries
                .AsNoTracking()
                .Where(s => s.TickerId == ticker.Id)
                .OrderByDescending(s => s.SummaryDate)
                .Take(2)
                .ToListAsync(ct);

            var lastScore = scores.FirstOrDefault();
            
            var trend = "→";
            if (summaries.Count >= 2)
            {
                var current = summaries[0].AverageScore;
                var previous = summaries[1].AverageScore;
                
                if (current > previous + 0.05m) trend = "↑";
                else if (current > previous) trend = "↗";
                else if (current < previous - 0.05m) trend = "↓";
                else if (current < previous) trend = "↘";
            }

            result.Add(new TickerSummaryDto
            {
                Symbol = ticker.Symbol,
                CompanyName = ticker.CompanyName,
                ArticlesCount = scores.Count,
                AvgSentiment = scores.Count > 0 ? scores.Average(s => s.Score) : 0m,
                LastScore = lastScore?.Score,
                LastLabel = lastScore?.ScoreLabel ?? (scores.Count > 0 ? "Neutral" : "No data"),
                LastUpdated = lastScore?.ScoredAt.ToString("MMM dd, yyyy") ?? "No data",
                Trend = trend
            });
        }

        return result;
    }
}
