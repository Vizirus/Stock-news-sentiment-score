using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;

namespace Application.UseCases;

public class FetchArticlesUseCase
{
    private readonly IAppDBContext dbContext;
    private readonly INewsAPI newsAPI;

    public FetchArticlesUseCase(IAppDBContext appDBContext, INewsAPI newsAPI)
    {
        dbContext = appDBContext;
        this.newsAPI = newsAPI;
    }

    public async Task ExecuteAsync(CancellationToken token = default)
    {
        var tickers = await dbContext.Ticker.AsNoTracking().ToListAsync(token);

        foreach (var ticker in tickers)
        {
            token.ThrowIfCancellationRequested();
          
            var toDate = DateTime.UtcNow.Date;
            var fromDate = toDate.AddDays(-1);
            var fetchedArticles = await newsAPI.GetArticlesForCompany(ticker.Symbol, fromDate, toDate, token);
            if (fetchedArticles.Count == 0)
            {
                continue;
            }

            var fetchedUrls = fetchedArticles
                .Select(a => a.Url)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var fetchedTitles = fetchedArticles
                .Select(a => a.Title)
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingKeys = await dbContext.Article
                .AsNoTracking()
                .Where(a =>
                    (fetchedUrls.Count > 0 && fetchedUrls.Contains(a.Url)) ||
                    (fetchedTitles.Count > 0 && fetchedTitles.Contains(a.Title)))
                .Select(a => new { a.Url, a.Title })
                .ToListAsync(token);

            var existingUrls = new HashSet<string>(
                existingKeys.Select(x => x.Url).Where(url => !string.IsNullOrWhiteSpace(url)),
                StringComparer.OrdinalIgnoreCase);

            var existingTitles = new HashSet<string>(
                existingKeys.Select(x => x.Title).Where(title => !string.IsNullOrWhiteSpace(title)),
                StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;

            foreach (var fetched in fetchedArticles)
            {
                if ((string.IsNullOrWhiteSpace(fetched.Url) && string.IsNullOrWhiteSpace(fetched.Title)) ||
                    (!string.IsNullOrWhiteSpace(fetched.Url) && existingUrls.Contains(fetched.Url)) ||
                    (!string.IsNullOrWhiteSpace(fetched.Title) && existingTitles.Contains(fetched.Title)))
                {
                    continue;
                }

                var article = new Article
                {
                    Title = fetched.Title,
                    Description = fetched.Description,
                    Url = fetched.Url,
                    SourceName = fetched.SourceName,
                    PublishedAt = fetched.PublishedAt,
                    CreatedAt = fetched.CreatedAt == default ? now : fetched.CreatedAt
                };

                dbContext.Article.Add(article);

                dbContext.ScoringJobs.Add(new ScoringJob
                {
                    Article = article,
                    TickerId = ticker.Id,
                    StatusId = ScoringJobStatus.Pending,
                    CreatedAt = now,
                    StartedAt = default,
                    CompletdAt = default
                });

                if (!string.IsNullOrWhiteSpace(article.Url))
                {
                    existingUrls.Add(article.Url);
                }

                if (!string.IsNullOrWhiteSpace(article.Title))
                {
                    existingTitles.Add(article.Title);
                }
            }

            await dbContext.SaveChangesAsync(token);
        }
    }
}
