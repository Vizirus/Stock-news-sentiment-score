namespace Application.DTOs;

public class DashboardDto
{
    public string StockLabel { get; set; } = string.Empty; 

    public string CompanyName { get; set; } = string.Empty; 

    public decimal AverageSentiment { get; set; } 

    public string SentimentLabel { get; set; } = string.Empty; 

    public int ArticlesForToday { get; set; }

    public List<HourlyTrendDto> HourlyTrend { get; set; } = new();
    public List<RecentArticleDto> RecentArticles { get; set; } = new();
    public List<TickerTrendDto> DailySummaries { get; set; } = new();

    public int PendingJobsCount { get; set; }

    public int FailedJobsCount { get; set; }

    public int PositiveArticlesCount { get; set; }
    public int NegativeArticlesCount { get; set; }
    public int NeutralArticlesCount { get; set; }
}
