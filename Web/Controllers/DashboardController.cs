using Application.UseCases;
using Infrastructure.DB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Models;

namespace Web.Controllers;

public class DashboardController : Controller
{
    private readonly GetDashboardDataUseCase _getDashboardData;
    private readonly RetryFailedJobsUseCase _retryFailedJobs;
    private readonly AppDbContext _dbContext;

    public DashboardController(
        GetDashboardDataUseCase getDashboardData, 
        RetryFailedJobsUseCase retryFailedJobs,
        AppDbContext dbContext)
    {
        _getDashboardData = getDashboardData;
        _retryFailedJobs = retryFailedJobs;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? ticker, DateTime? startDate, DateTime? endDate)
    {
        var tickers = await _dbContext.Ticker
            .AsNoTracking()
            .Select(t => t.Symbol)
            .ToListAsync();

        var selectedTicker = string.IsNullOrWhiteSpace(ticker) 
            ? (tickers.FirstOrDefault() ?? "AAPL") 
            : ticker;

        var end = endDate ?? DateTime.UtcNow.Date;
        var start = startDate ?? end.AddDays(-7);

        var data = await _getDashboardData.ExecuteAsync(selectedTicker, start, end);

        var viewModel = new DashboardViewModel
        {
            SelectedTicker = selectedTicker,
            AvailableTickers = tickers,
            DateRangeStart = start,
            DateRangeEnd = end,
        };

        if (data != null)
        {
            viewModel.CompanyName = data.CompanyName;
            viewModel.CurrentAvgSentiment = data.AverageSentiment;
            viewModel.SentimentLabel = data.SentimentLabel;
            viewModel.ArticlesToday = data.ArticlesForToday;
            viewModel.LastUpdated = DateTime.UtcNow.ToString("MMM dd, yyyy");
            viewModel.LastUpdatedTime = DateTime.UtcNow.ToString("h:mm tt");
            
            // Generate the list of dates from start to endDate
            var dateRange = new List<DateTime>();
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            {
                dateRange.Add(d);
            }

            viewModel.TrendLabels = new List<string>();
            viewModel.TrendScores = new List<decimal>();

            foreach (var d in dateRange)
            {
                viewModel.TrendLabels.Add(d.ToString("MMM dd"));
                var summaryForDate = data.Trend.FirstOrDefault(t => t.Date == DateOnly.FromDateTime(d));
                viewModel.TrendScores.Add(summaryForDate?.AverageScore ?? 0m);
            }

            if (data.Trend.Count > 1)
            {
                var first = data.Trend.First().AverageScore;
                var last = data.Trend.Last().AverageScore;
                viewModel.TrendDirection = last > first ? "Upward" : (last < first ? "Downward" : "Stable");
            }
            else
            {
                viewModel.TrendDirection = "Stable";
            }

            int totalPos = data.RecentArticles.Count(a => a.ScoreLabel.Contains("Positive", StringComparison.OrdinalIgnoreCase));
            int totalNeg = data.RecentArticles.Count(a => a.ScoreLabel.Contains("Negative", StringComparison.OrdinalIgnoreCase));
            int totalNeut = data.RecentArticles.Count(a => a.ScoreLabel.Contains("Neutral", StringComparison.OrdinalIgnoreCase));
            int total = data.RecentArticles.Count;
            
            viewModel.TotalArticles = data.ArticlesForToday; // Number of articles in the period
            viewModel.PositivePercent = total > 0 ? (totalPos * 100 / total) : 0;
            viewModel.NegativePercent = total > 0 ? (totalNeg * 100 / total) : 0;
            viewModel.NeutralPercent = total > 0 ? (totalNeut * 100 / total) : 0;

            viewModel.RecentArticles = data.RecentArticles.Select(a => new ArticleRowViewModel
            {
                Title = a.Title,
                Source = a.SourceName,
                PublishedAt = a.PublishedAt.ToString("MMM dd, yyyy h:mm tt"),
                Score = a.Score,
                ScoreLabel = a.ScoreLabel,
                Confidence = a.Confidence,
                Url = a.Url
            }).ToList();

            viewModel.DailySummaries = data.DailySummaries.Select(d => new DailySummaryViewModel
            {
                Date = d.Date.ToString("MMM dd, yyyy"),
                AverageScore = d.AverageScore,
                // The mock data had ArticleCount per day, but our UseCase didn't return it in TickerTrendDto.
                // Let's set it to 0 for now since the UI doesn't strongly depend on it being accurate in the dashboard view,
                // or we can update the DTO later if needed. The dashboard only shows the trend line.
                ArticleCount = 0 
            }).ToList();

            viewModel.PendingJobs = data.PendingJobsCount;
            viewModel.FailedJobs = data.FailedJobsCount;
        }

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> RetryFailedJobs(string ticker)
    {
        await _retryFailedJobs.ExecuteAsync(ticker);
        return RedirectToAction(nameof(Index), new { ticker });
    }

    [HttpGet]
    public async Task<IActionResult> ExportRecentArticles(string ticker, DateTime? startDate, DateTime? endDate)
    {
        var end = endDate ?? DateTime.UtcNow.Date;
        var start = startDate ?? end.AddDays(-7);

        var data = await _getDashboardData.ExecuteAsync(ticker, start, end);
        if (data == null) return NotFound();

        var csv = "Title,Source,PublishedAt,Score,ScoreLabel,Confidence,Url\n";
        foreach (var article in data.RecentArticles)
        {
            var title = article.Title.Replace("\"", "\"\"");
            csv += $"\"{title}\",{article.SourceName},{article.PublishedAt:yyyy-MM-dd HH:mm},{article.Score},{article.ScoreLabel},{article.Confidence},{article.Url}\n";
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"{ticker}_RecentArticles_{start:yyyyMMdd}_{end:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportDailySummaries(string ticker, DateTime? startDate, DateTime? endDate)
    {
        var end = endDate ?? DateTime.UtcNow.Date;
        var start = startDate ?? end.AddDays(-7);

        var data = await _getDashboardData.ExecuteAsync(ticker, start, end);
        if (data == null) return NotFound();

        var csv = "Date,AverageScore\n";
        foreach (var summary in data.DailySummaries)
        {
            csv += $"{summary.Date:yyyy-MM-dd},{summary.AverageScore}\n";
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"{ticker}_DailySummaries_{start:yyyyMMdd}_{end:yyyyMMdd}.csv");
    }
}
