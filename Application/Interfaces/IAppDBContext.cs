using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Interfaces;

public interface IAppDBContext
{
    DbSet<Ticker> Ticker { get; set; }
    DbSet<ArticleScore> ArticleScores { get; set; }
    DbSet<Article> Article { get; set; }
    DbSet<ScoringJob> ScoringJobs { get; set; }
    DbSet<TickerDailySummary> TickerDailySummaries { get; set; }
    DbSet<UserSettings> UserSettings { get; set; }
    DbSet<UserTicker> UserTickers { get; set; }

    Task<int> SaveChangesAsync(CancellationToken token = default);
}
