using Domain.Enums;

namespace Domain.Entities;

public class ArticleScore
{
    public int Id { get; set; }

    public int ArticleId { get; set; }

    public int TickerId { get; set; }

    public decimal Score { get; set; }

    public string? ScoreLabel { get; set; }

    public decimal Confidence { get; set; }

    public DateTime ScoredAt { get; set; }

    public Article Article { get; set; } = null!;
    public Ticker Ticker { get; set; } = null!;
}
