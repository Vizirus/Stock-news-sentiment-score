namespace Web.Models;

// ===================== TICKERS =====================
public class TickersViewModel
{
    public List<TickerRowViewModel> Tickers { get; set; } = [];
}

public class TickerRowViewModel
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal AvgSentiment { get; set; }
    public int Articles { get; set; }
    public decimal LastScore { get; set; }
    public string LastLabel { get; set; } = string.Empty;
    public string LastUpdated { get; set; } = string.Empty;
    public string Trend { get; set; } = string.Empty;
}

// ===================== ARTICLES =====================
public class ArticlesViewModel
{
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    // Aggregate stats computed over ALL filtered rows (not just current page)
    public int ScoredCount { get; set; }
    public decimal AvgConfidence { get; set; }
    public int SourcesCount { get; set; }

    public string? SelectedTicker { get; set; }
    public string? SelectedLabel { get; set; }
    public string? SelectedSource { get; set; }
    public DateTime DateRangeStart { get; set; } = DateTime.UtcNow.AddDays(-7);
    public DateTime DateRangeEnd { get; set; } = DateTime.UtcNow;

    public List<string> AvailableTickers { get; set; } = [];
    public List<string> AvailableLabels { get; set; } = ["Very Positive", "Positive", "Neutral", "Negative", "Very Negative"];
    public List<string> AvailableSources { get; set; } = [];

    public List<ArticleDetailViewModel> Articles { get; set; } = [];
}

public class ArticleDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public string PublishedAt { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
}

// ===================== SCORING JOBS =====================
public class ScoringJobsViewModel
{
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    public string? SelectedTicker { get; set; }
    public string? SelectedStatus { get; set; }
    public string? SelectedLabel { get; set; }
    public DateTime DateRangeStart { get; set; } = DateTime.UtcNow.AddDays(-7);
    public DateTime DateRangeEnd { get; set; } = DateTime.UtcNow;

    public List<string> AvailableTickers { get; set; } = [];
    public List<string> AvailableStatuses { get; set; } = ["Pending", "Completed", "Failed"];
    public List<string> AvailableLabels { get; set; } = ["Very Positive", "Positive", "Neutral", "Negative", "Very Negative", "Irrelevant"];

    public int TotalPending { get; set; }
    public int TotalFailed { get; set; }
    public int TotalCompleted { get; set; }

    public List<ScoringJobDetailViewModel> Jobs { get; set; } = [];
}

public class ScoringJobDetailViewModel
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string ArticleTitle { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public string? Label { get; set; }
    public string? ErrorMessage { get; set; }
}

// ===================== SUMMARIES =====================
public class SummariesViewModel
{
    public List<string> AvailableTickers { get; set; } = [];
    public string SelectedTicker { get; set; } = "All Tickers";
    public string CompanyName { get; set; } = string.Empty;

    public List<SummaryRowViewModel> Summaries { get; set; } = [];

    // For mini sparkline chart
    public List<string> ChartLabels => Summaries.AsEnumerable().Reverse().Select(s => s.Date).ToList();
    public List<decimal> ChartScores => Summaries.AsEnumerable().Reverse().Select(s => s.AvgScore).ToList();
}

public class SummaryRowViewModel
{
    public string Date { get; set; } = string.Empty;
    public decimal AvgScore { get; set; }
    public string Label { get; set; } = string.Empty;
    public int ArticleCount { get; set; }
    public int PosCount { get; set; }
    public int NeutCount { get; set; }
    public int NegCount { get; set; }
}

// ===================== SETTINGS =====================
public class SettingsViewModel
{
    public int DailyLlmCallLimit { get; set; }
    public int BatchSize { get; set; }
    public int FetchIntervalHours { get; set; }
    public int ActiveTickerCount { get; set; }
    public bool Success { get; set; }
}
