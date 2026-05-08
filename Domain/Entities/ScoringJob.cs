using Domain.Enums;

namespace Domain.Entities;

public class ScoringJob
{
    public int Id { get; set; }

    public int ArticleId { get; set; }

    public int TickerId { get; set; }

    public ScoringJobStatus StatusId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime CompletdAt { get; set; }

    public string? ErrorMessage { get; set; }

    public Article? Article { get; set; } = null;

    public Ticker? Ticker { get; set; } = null;

}
