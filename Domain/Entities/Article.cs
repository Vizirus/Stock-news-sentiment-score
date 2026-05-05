namespace Domain.Entities;

public class Article
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public DateTime PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<ArticleScore> ArticleScores { get; set; } = [];
    public ICollection<ScoringJob> ScoringJobs { get; set; } = [];
}
