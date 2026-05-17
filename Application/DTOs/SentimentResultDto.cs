namespace Application.DTOs;

public class SentimentResultDto
{
    /// <summary>
    /// Matches ArticleInputDto.Index to correlate result back to original job.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Sentiment score: -1.0 (very negative) to 1.0 (very positive).
    /// </summary>
    public decimal Score { get; set; }

    /// <summary>
    /// Human-readable label: Very Positive / Positive / Neutral / Negative / Very Negative.
    /// </summary>
    public string ScoreLabel { get; set; } = string.Empty;

    /// <summary>
    /// Model confidence in the result: 0.0 to 1.0.
    /// </summary>
    public decimal Confidence { get; set; }
}
