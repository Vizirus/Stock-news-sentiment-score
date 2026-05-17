using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application.DTOs;
using Application.Interfaces;
using Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices;

public class FinnhubNewsApiService : INewsAPI
{
    private readonly HttpClient _httpClient;
    private readonly NewsApiOptions _options;

    public FinnhubNewsApiService(HttpClient httpClient, IOptions<NewsApiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<List<FetchedArticleDto>> GetArticlesForCompany(string ticker, DateTime fromDate, DateTime toDate, CancellationToken token = default)
    {
        var from = fromDate.ToString("yyyy-MM-dd");
        var to = toDate.ToString("yyyy-MM-dd");

        var url = $"company-news?symbol={ticker}&from={from}&to={to}&token={_options.ApiKey}";

        var response = await _httpClient.GetAsync(url, token);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(token);

            throw new HttpRequestException(
                $"Finnhub request failed with status code {(int)response.StatusCode}: {response.ReasonPhrase}. Response: {errorBody}");
        }
        var finnhubArticles = await response.Content.ReadFromJsonAsync<List<FinnhubArticle>>(cancellationToken: token);

        if (finnhubArticles == null)
            return new List<FetchedArticleDto>();

        return finnhubArticles
            .Where(a => IsValid(a, ticker))
            .Select(a => new FetchedArticleDto
            {
                Description = a.Summary?.Trim() ?? string.Empty,
                Title = a.Headline?.Trim() ?? string.Empty,
                Url = a.Url?.Trim() ?? string.Empty,
                SourceName = a.Source?.Trim() ?? "Finnhub",
                PublishedAt = DateTimeOffset.FromUnixTimeSeconds(a.Datetime).UtcDateTime,
                CreatedAt = DateTime.UtcNow
            })
            .DistinctBy(a => a.Url)
            .ToList();
    }

    private static bool IsValid(FinnhubArticle article, string ticker)
    {
        // Skip articles with empty title.
        if (string.IsNullOrWhiteSpace(article.Headline)) return false;
        
        // Skip articles with empty URL.
        if (string.IsNullOrWhiteSpace(article.Url)) return false;
        
        // Skip articles without PublishedAt.
        if (article.Datetime <= 0) return false;


        return true;
    }

    private class FinnhubArticle
    {
        [JsonPropertyName("headline")]
        public string Headline { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("datetime")]
        public long Datetime { get; set; }

        [JsonPropertyName("id")]
        public long Id { get; set; }
    }
}
