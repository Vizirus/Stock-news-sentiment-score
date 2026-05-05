namespace Domain.Entities;

public class Ticker
{
    public int Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public ICollection<ArticleScore> ArticleScores { get; set; } = [];
    public ICollection<ScoringJob> ScoringJobs { get; set; } = [];
    public ICollection<TickerDailySummary> DailySummaries { get; set; } = [];
}
