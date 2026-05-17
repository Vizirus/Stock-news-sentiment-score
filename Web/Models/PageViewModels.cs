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
    public List<string> AvailableTickers { get; set; } = ["AAPL", "MSFT", "TSLA", "NVDA", "AMZN", "GOOGL", "META"];
    public string SelectedTicker { get; set; } = "AAPL";
    public string CompanyName { get; set; } = "Apple Inc.";

    public List<SummaryRowViewModel> Summaries { get; set; } =
    [
        new() { Date = "May 19, 2025", AvgScore = 0.32m, Label = "Positive",  ArticleCount = 48, PosCount = 25, NeutCount = 14, NegCount = 9  },
        new() { Date = "May 18, 2025", AvgScore = 0.18m, Label = "Neutral",   ArticleCount = 62, PosCount = 22, NeutCount = 28, NegCount = 12 },
        new() { Date = "May 17, 2025", AvgScore = 0.05m, Label = "Neutral",   ArticleCount = 41, PosCount = 15, NeutCount = 19, NegCount = 7  },
        new() { Date = "May 16, 2025", AvgScore = -0.12m, Label = "Neutral",  ArticleCount = 55, PosCount = 18, NeutCount = 20, NegCount = 17 },
        new() { Date = "May 15, 2025", AvgScore = 0.41m, Label = "Positive",  ArticleCount = 73, PosCount = 38, NeutCount = 21, NegCount = 14 },
        new() { Date = "May 14, 2025", AvgScore = 0.27m, Label = "Positive",  ArticleCount = 59, PosCount = 27, NeutCount = 22, NegCount = 10 },
        new() { Date = "May 13, 2025", AvgScore = 0.33m, Label = "Positive",  ArticleCount = 50, PosCount = 24, NeutCount = 18, NegCount = 8  },
        new() { Date = "May 12, 2025", AvgScore = -0.08m, Label = "Neutral",  ArticleCount = 44, PosCount = 14, NeutCount = 20, NegCount = 10 },
        new() { Date = "May 11, 2025", AvgScore = -0.31m, Label = "Negative", ArticleCount = 37, PosCount = 8,  NeutCount = 13, NegCount = 16 },
        new() { Date = "May 10, 2025", AvgScore = 0.19m, Label = "Neutral",   ArticleCount = 52, PosCount = 20, NeutCount = 24, NegCount = 8  },
    ];

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
    public bool Success { get; set; }
}
