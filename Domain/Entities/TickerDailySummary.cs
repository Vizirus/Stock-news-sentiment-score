namespace Domain.Entities;

public class TickerDailySummary
{
    public int Id { get; set; }

    public int TickerId { get; set; }

    public DateTime SummaryDate { get; set; }

    public decimal AverageScore { get; set; }

    public int ArticleCount { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Ticker? Ticker { get; set; } = null;
}
