namespace Application.DTOs;

public class TickerSummaryDto
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal AvgSentiment { get; set; }
    public int ArticlesCount { get; set; }
    public decimal? LastScore { get; set; }
    public string? LastLabel { get; set; }
    public string LastUpdated { get; set; } = "No data";
    public string Trend { get; set; } = "→";
}
