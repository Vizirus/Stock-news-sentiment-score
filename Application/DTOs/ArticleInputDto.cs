namespace Application.DTOs;

public class ArticleInputDto
{
    /// <summary>
    /// Position in the batch — used to match results back to the original ScoringJob.
    /// </summary>
    public int Index { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
