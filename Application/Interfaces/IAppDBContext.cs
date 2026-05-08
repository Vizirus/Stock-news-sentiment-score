using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Interfaces;

public interface IAppDBContext
{
    DbSet<Ticker> Ticker { get; set; }
    DbSet<ArticleScore> ArticleScores { get; set; }
    DbSet<Article> Artice { get; set; }
    DbSet<ScoringJob> ScoringJobs { get; set; }
    DbSet<TickerDailySummary> TickerDailySummaries { get; set; }

    Task SaveChangesAsync(CancellationToken token = default);
}
