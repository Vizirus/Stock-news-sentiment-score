namespace Application.DTOs;

public class SentimentResultDto
{
    public decimal Score { get; set; }
    public string ScoreLabel { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
}
