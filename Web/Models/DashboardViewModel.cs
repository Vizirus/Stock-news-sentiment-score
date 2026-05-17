using Application.DTOs;

namespace Web.Models;

public class DashboardViewModel
{
    // --- Filter bar ---
    public string SelectedTicker { get; set; } = "AAPL";
    public List<string> AvailableTickers { get; set; } = new();
    public DateTime DateRangeStart { get; set; }
    public DateTime DateRangeEnd { get; set; }

    // --- KPI Cards ---
    public string CompanyName { get; set; } = string.Empty;
    public decimal CurrentAvgSentiment { get; set; }
    public string SentimentLabel { get; set; } = string.Empty;
    public string TrendDirection { get; set; } = string.Empty;
    public int ArticlesToday { get; set; }
    public string LastUpdated { get; set; } = string.Empty;
    public string LastUpdatedTime { get; set; } = string.Empty;

    // --- Sentiment Trend (for Chart.js line chart) ---
    public List<string> TrendLabels { get; set; } = new();
    public List<decimal> TrendScores { get; set; } = new();

    // --- Sentiment Distribution (for Chart.js pie chart) ---
    public int PositivePercent { get; set; }
    public int NeutralPercent { get; set; }
    public int NegativePercent { get; set; }
    public int TotalArticles { get; set; }

    // --- Recent Articles ---
    public List<ArticleRowViewModel> RecentArticles { get; set; } = new();

    // --- Daily Summaries ---
    public List<DailySummaryViewModel> DailySummaries { get; set; } = new();

    // --- Scoring Jobs ---
    public int PendingJobs { get; set; }
    public int FailedJobs { get; set; }
}

public class ArticleRowViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string PublishedAt { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string ScoreLabel { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string Url { get; set; } = string.Empty;
}

public class DailySummaryViewModel
{
    public string Date { get; set; } = string.Empty;
    public decimal AverageScore { get; set; }
    public int ArticleCount { get; set; }
}
