using Microsoft.AspNetCore.Mvc;
using Web.Models;
using Application.UseCases;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.IO;
using Domain.Entities;

using Microsoft.AspNetCore.Authorization;

namespace Web.Controllers;

[Authorize(Roles = "Admin")]
public class TickersController : Controller
{
    private readonly GetTickersUseCase _getTickersUseCase;
    private readonly AddTickerUseCase _addTickerUseCase;

    public TickersController(GetTickersUseCase getTickersUseCase, AddTickerUseCase addTickerUseCase)
    {
        _getTickersUseCase = getTickersUseCase;
        _addTickerUseCase = addTickerUseCase;
    }

    public async Task<IActionResult> Index()
    {
        var tickersDto = await _getTickersUseCase.ExecuteAsync();
        
        var viewModel = new TickersViewModel
        {
            Tickers = tickersDto.Select(t => new TickerRowViewModel
            {
                Symbol = t.Symbol,
                CompanyName = t.CompanyName,
                AvgSentiment = t.AvgSentiment,
                Articles = t.ArticlesCount,
                LastScore = t.LastScore ?? 0m,
                LastLabel = t.LastLabel ?? "No data",
                LastUpdated = t.LastUpdated,
                Trend = t.Trend
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Add(string symbol, string companyName)
    {
        await _addTickerUseCase.ExecuteAsync(symbol, companyName);
        return RedirectToAction("Index");
    }
}

public class ArticlesController : Controller
{
    private readonly IAppDBContext _dbContext;

    public ArticlesController(IAppDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index(string? ticker, string? label, string? source, DateTime? startDate, DateTime? endDate, int page = 1)
    {
        var pageSize = 10;
        var start = startDate ?? DateTime.UtcNow.AddDays(-7);
        var end = endDate ?? DateTime.UtcNow;

        var query = _dbContext.ArticleScores
            .Include(ascore => ascore.Article)
            .Include(ascore => ascore.Ticker)
            .Where(ascore => ascore.ScoredAt >= start && ascore.ScoredAt <= end);

        if (!string.IsNullOrEmpty(ticker) && ticker != "All Tickers")
            query = query.Where(ascore => ascore.Ticker.Symbol == ticker);

        if (!string.IsNullOrEmpty(label) && label != "All Labels")
            query = query.Where(ascore => ascore.ScoreLabel == label);

        if (!string.IsNullOrEmpty(source) && source != "All Sources")
            query = query.Where(ascore => ascore.Article.SourceName == source);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(ascore => ascore.ScoredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ascore => new ArticleDetailViewModel
            {
                Id = ascore.Article.Id,
                Title = ascore.Article.Title,
                Source = ascore.Article.SourceName,
                Ticker = ascore.Ticker.Symbol,
                PublishedAt = ascore.Article.PublishedAt.ToString("MMM dd, yyyy HH:mm"),
                Score = ascore.Score,
                Label = ascore.ScoreLabel ?? "Neutral",
                Confidence = ascore.Confidence
            })
            .ToListAsync();

        var viewModel = new ArticlesViewModel
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            SelectedTicker = ticker,
            SelectedLabel = label,
            SelectedSource = source,
            DateRangeStart = start,
            DateRangeEnd = end,
            Articles = items,
            AvailableTickers = await _dbContext.Ticker.Select(t => t.Symbol).OrderBy(s => s).ToListAsync(),
            AvailableSources = await _dbContext.Article.Select(a => a.SourceName).Distinct().OrderBy(s => s).ToListAsync()
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Export(string? ticker, string? label, string? source, DateTime? startDate, DateTime? endDate)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        var query = _dbContext.ArticleScores
            .Include(ascore => ascore.Article)
            .Include(ascore => ascore.Ticker)
            .Where(ascore => ascore.ScoredAt >= start && ascore.ScoredAt <= end);

        if (!string.IsNullOrEmpty(ticker) && ticker != "All Tickers")
            query = query.Where(ascore => ascore.Ticker.Symbol == ticker);

        if (!string.IsNullOrEmpty(label) && label != "All Labels")
            query = query.Where(ascore => ascore.ScoreLabel == label);

        if (!string.IsNullOrEmpty(source) && source != "All Sources")
            query = query.Where(ascore => ascore.Article.SourceName == source);

        var items = await query
            .OrderByDescending(ascore => ascore.ScoredAt)
            .Select(ascore => new
            {
                ascore.Article.Id,
                ascore.Article.Title,
                ascore.Ticker.Symbol,
                ascore.Article.SourceName,
                ascore.Article.PublishedAt,
                ascore.Score,
                ascore.ScoreLabel,
                ascore.Confidence
            })
            .ToListAsync();

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Id,Title,Ticker,Source,PublishedAt,Score,Label,Confidence");

        foreach (var item in items)
        {
            csv.AppendLine($"{item.Id},\"{item.Title.Replace("\"", "\"\"")}\",{item.Symbol},{item.SourceName},{item.PublishedAt:yyyy-MM-dd HH:mm},{item.Score},{item.ScoreLabel},{item.Confidence}");
        }

        // Construct filename: Articles_CurrentDate_FiltersApplied
        var filterInfo = new List<string>();

        if (!string.IsNullOrEmpty(ticker) && ticker != "All Tickers")
            filterInfo.Add(SafeFileNamePart(ticker));

        if (!string.IsNullOrEmpty(label) && label != "All Labels")
            filterInfo.Add(SafeFileNamePart(label));

        if (!string.IsNullOrEmpty(source) && source != "All Sources")
            filterInfo.Add(SafeFileNamePart(source));

        var filterString = filterInfo.Count > 0 ? string.Join("_", filterInfo) : "All";
        var fileName = $"Articles_{DateTime.UtcNow:yyyyMMdd}_{filterString}.csv";

        var bytes = System.Text.Encoding.UTF8
            .GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();

        return new FileContentResult(bytes, "text/csv; charset=utf-8")
        {
            FileDownloadName = fileName
        };
    }

    private static string SafeFileNamePart(string value)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '_');
        }

        return value.Replace(" ", "");
    }
}

public class ScoringJobsController : Controller
{
    private readonly GetScoringJobsUseCase _getScoringJobsUseCase;
    private readonly RetryAllFailedJobsUseCase _retryAllFailedJobsUseCase;

    public ScoringJobsController(GetScoringJobsUseCase getScoringJobsUseCase, RetryAllFailedJobsUseCase retryAllFailedJobsUseCase)
    {
        _getScoringJobsUseCase = getScoringJobsUseCase;
        _retryAllFailedJobsUseCase = retryAllFailedJobsUseCase;
    }

    public async Task<IActionResult> Index()
    {
        var result = await _getScoringJobsUseCase.ExecuteAsync();
        
        var viewModel = new ScoringJobsViewModel
        {
            TotalPending = result.TotalPending,
            TotalFailed = result.TotalFailed,
            TotalCompleted = result.TotalCompleted,
            Jobs = result.Jobs.Select(j => new ScoringJobDetailViewModel
            {
                Id = j.Id,
                ArticleTitle = j.Article?.Title ?? "Unknown Article",
                Ticker = j.Ticker?.Symbol ?? "Unknown",
                Status = j.StatusId.ToString(),
                CreatedAt = j.CreatedAt.ToString("MMM dd, yyyy HH:mm"),
                StartedAt = j.StartedAt == default ? null : j.StartedAt.ToString("MMM dd, yyyy HH:mm"),
                CompletedAt = j.CompletdAt == default ? null : j.CompletdAt.ToString("MMM dd, yyyy HH:mm"),
                ErrorMessage = j.ErrorMessage,
                Score = null,
                Label = null
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> RetryAll()
    {
        await _retryAllFailedJobsUseCase.ExecuteAsync();
        return RedirectToAction(nameof(Index));
    }
}

public class SummariesController : Controller
{
    private readonly GetSummariesUseCase _getSummariesUseCase;
    private readonly IAppDBContext _dbContext;

    public SummariesController(GetSummariesUseCase getSummariesUseCase, IAppDBContext dbContext)
    {
        _getSummariesUseCase = getSummariesUseCase;
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index(string? ticker, string? dateMode, DateTime? singleDate, DateTime? rangeFrom, DateTime? rangeTo)
    {
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;

        // Default to range mode if not specified
        dateMode ??= "range";

        if (dateMode == "single" && singleDate.HasValue)
        {
            start = singleDate.Value;
            end = singleDate.Value;
        }
        else if (dateMode == "range")
        {
            if (rangeFrom.HasValue) start = rangeFrom.Value;
            if (rangeTo.HasValue) end = rangeTo.Value;
        }

        var summaries = await _getSummariesUseCase.ExecuteAsync(ticker!, start, end);

        string GetLabel(decimal score) => score >= 0.2m ? "Positive" : (score <= -0.2m ? "Negative" : "Neutral");

        var viewModel = new SummariesViewModel
        {
            AvailableTickers = await _dbContext.Ticker.Select(t => t.Symbol).OrderBy(s => s).ToListAsync(),
            SelectedTicker = ticker ?? "All Tickers",
            Summaries = summaries.Select(s => new SummaryRowViewModel
            {
                Date = s.SummaryDate.ToString("MMM dd, yyyy"),
                AvgScore = s.AverageScore,
                Label = GetLabel(s.AverageScore),
                ArticleCount = s.ArticleCount,
                PosCount = 0, // Not stored in DB currently
                NeutCount = 0,
                NegCount = 0
            }).ToList()
        };

        // UI state persistence (using ViewData or ViewBag since we don't have these props on ViewModel)
        ViewData["DateMode"] = dateMode;
        ViewData["SingleDate"] = start.ToString("yyyy-MM-dd");
        ViewData["RangeFrom"] = start.ToString("yyyy-MM-dd");
        ViewData["RangeTo"] = end.ToString("yyyy-MM-dd");

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Export(string? ticker, string? dateMode, DateTime? singleDate, DateTime? rangeFrom, DateTime? rangeTo)
    {
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;

        dateMode ??= "range";

        if (dateMode == "single" && singleDate.HasValue)
        {
            start = singleDate.Value;
            end = singleDate.Value;
        }
        else if (dateMode == "range")
        {
            if (rangeFrom.HasValue) start = rangeFrom.Value;
            if (rangeTo.HasValue) end = rangeTo.Value;
        }

        var summaries = await _getSummariesUseCase.ExecuteAsync(ticker!, start, end);

        string GetLabel(decimal score) => score >= 0.2m ? "Positive" : (score <= -0.2m ? "Negative" : "Neutral");

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Date,Ticker,AverageScore,Label,ArticleCount");

        foreach (var s in summaries)
        {
            csv.AppendLine($"{s.SummaryDate:yyyy-MM-dd},{s.Ticker?.Symbol ?? ""},{s.AverageScore},{GetLabel(s.AverageScore)},{s.ArticleCount}");
        }

        var filterInfo = new List<string>();
        if (!string.IsNullOrEmpty(ticker) && ticker != "All Tickers") filterInfo.Add(SafeFileNamePart(ticker));
        filterInfo.Add(start.ToString("yyyyMMdd"));
        if (dateMode == "range") filterInfo.Add(end.ToString("yyyyMMdd"));

        var filterString = filterInfo.Count > 0 ? string.Join("_", filterInfo) : "All";
        var fileName = $"Summaries_Export_{filterString}.csv";

        var bytes = System.Text.Encoding.UTF8
            .GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();

        return new FileContentResult(bytes, "text/csv; charset=utf-8")
        {
            FileDownloadName = fileName
        };
    }

    private static string SafeFileNamePart(string value)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '_');
        }
        return value.Replace(" ", "");
    }
}

[Authorize(Roles = "Admin")]
public class SettingsController : Controller
{
    private readonly IAppDBContext _dbContext;

    public SettingsController(IAppDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool success = false)
    {
        var dbSettings = await _dbContext.SystemSettings.FirstOrDefaultAsync();
        if (dbSettings == null)
        {
            // Graceful lazy-initialization fallback trigger
            dbSettings = new SystemSettings
            {
                DailyLlmCallLimit = 100,
                BatchSize = 20,
                FetchIntervalHours = 6,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.SystemSettings.Add(dbSettings);
            await _dbContext.SaveChangesAsync();
        }

        var viewModel = new SettingsViewModel
        {
            DailyLlmCallLimit = dbSettings.DailyLlmCallLimit,
            BatchSize = dbSettings.BatchSize,
            FetchIntervalHours = dbSettings.FetchIntervalHours,
            ActiveTickerCount = await _dbContext.Ticker.CountAsync(),
            Success = success
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSettings(SettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.ActiveTickerCount = await _dbContext.Ticker.CountAsync(cancellationToken);
            return View("Index", model);
        }

        var tickerCount = await _dbContext.Ticker.CountAsync(cancellationToken);
        var estimatedRequests = Math.Ceiling((double)model.DailyLlmCallLimit / model.BatchSize) * tickerCount;
        if (estimatedRequests > 1000)
        {
            ModelState.AddModelError("", $"These settings would generate ~{estimatedRequests:0} LLM requests/day, exceeding the 1,000 request limit.");
            model.ActiveTickerCount = tickerCount;
            return View("Index", model);
        }

        var dbSettings = await _dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        if (dbSettings == null)
        {
            dbSettings = new SystemSettings 
            { 
                DailyLlmCallLimit = model.DailyLlmCallLimit,
                BatchSize = model.BatchSize,
                FetchIntervalHours = model.FetchIntervalHours,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.SystemSettings.Add(dbSettings);
        }
        else
        {
            dbSettings.DailyLlmCallLimit = model.DailyLlmCallLimit;
            dbSettings.BatchSize = model.BatchSize;
            dbSettings.FetchIntervalHours = model.FetchIntervalHours;
            dbSettings.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction("Index", new { success = true });
    }
}
