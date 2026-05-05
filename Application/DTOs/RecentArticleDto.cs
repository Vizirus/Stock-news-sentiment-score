namespace Application.DTOs;

public class RecentArticleDto
{
    public string Title { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public DateTime PublishedAt { get; set; }

    public string Url { get; set; } = string.Empty;

    public decimal Score { get; set; }

    public string ScoreLabel { get; set; } = string.Empty;

    public decimal Confidence { get; set; }
}
