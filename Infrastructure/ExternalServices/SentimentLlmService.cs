using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs;
using Application.Interfaces;
using Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices;

/// <summary>
/// Infrastructure implementation of ISentimentLLM using the Google Gemini REST API.
/// Sends article batches in a single prompt and parses a JSON array response.
/// </summary>
public class SentimentLlmService : ISentimentLLM
{
    private readonly HttpClient _httpClient;
    private readonly SentimentLlmOptions _options;
    private readonly ILogger<SentimentLlmService> _logger;

    public SentimentLlmService(
        HttpClient httpClient,
        IOptions<SentimentLlmOptions> options,
        ILogger<SentimentLlmService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<SentimentResultDto>> ScoreArticlesAsync(
        string ticker,
        string companyName,
        IReadOnlyList<ArticleInputDto> articles,
        CancellationToken token = default)
    {
        if (articles.Count == 0)
            return [];

        var prompt = BuildPrompt(ticker, companyName, articles);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json"
            }
        };

        var url = _options.BaseUrl;

        _logger.LogInformation(
            "Sending batch of {Count} articles to Gemini for ticker {Ticker}",
            articles.Count, ticker);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(url, requestBody, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP request to Gemini failed for ticker {Ticker}", ticker);
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(token);
            throw new HttpRequestException(
                $"Gemini API returned {(int)response.StatusCode}: {response.ReasonPhrase}. Body: {errorBody}");
        }

        // Gemini wraps the output in: candidates[0].content.parts[0].text
        var geminiResponse = await response.Content
            .ReadFromJsonAsync<GeminiResponse>(cancellationToken: token);

        var jsonText = geminiResponse?
            .Candidates?.FirstOrDefault()?
            .Content?.Parts?.FirstOrDefault()?
            .Text;

        if (string.IsNullOrWhiteSpace(jsonText))
        {
            throw new InvalidOperationException(
                $"Gemini returned an empty result for ticker {ticker}.");
        }

        List<SentimentResultDto>? results;
        try
        {
            results = JsonSerializer.Deserialize<List<SentimentResultDto>>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Gemini response for ticker {Ticker}. Raw: {Json}", ticker, jsonText);
            throw new InvalidOperationException($"Failed to deserialize Gemini sentiment response: {ex.Message}", ex);
        }

        if (results is null || results.Count == 0)
        {
            throw new InvalidOperationException(
                $"Gemini returned no results for ticker {ticker}.");
        }

        _logger.LogInformation(
            "Received {Count} results from Gemini for ticker {Ticker}",
            results.Count, ticker);

        return results;
    }

    private static string BuildPrompt(
        string ticker,
        string companyName,
        IReadOnlyList<ArticleInputDto> articles)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"You are a financial sentiment analyst. Analyze the following news articles about {companyName} ({ticker}).");
        sb.AppendLine();
        sb.AppendLine("Rate each article using this scale:");
        sb.AppendLine("  Score range  | Label");
        sb.AppendLine("  0.8 to 1.0   | Very Positive");
        sb.AppendLine("  0.2 to 0.8   | Positive");
        sb.AppendLine(" -0.2 to 0.2   | Neutral");
        sb.AppendLine(" -0.8 to -0.2  | Negative");
        sb.AppendLine(" -1.0 to -0.8  | Very Negative");
        sb.AppendLine();
        sb.AppendLine("Articles:");

        for (int i = 0; i < articles.Count; i++)
        {
            sb.AppendLine($"[{articles[i].Index}] Title: {articles[i].Title}");
            sb.AppendLine($"     Description: {articles[i].Description}");
        }

        sb.AppendLine();
        sb.AppendLine("Return a JSON ARRAY ONLY — one object per article, in this exact format:");
        sb.AppendLine("[");
        sb.AppendLine("  { \"Index\": 0, \"Score\": 0.75, \"ScoreLabel\": \"Positive\", \"Confidence\": 0.91 },");
        sb.AppendLine("  { \"Index\": 1, \"Score\": -0.3, \"ScoreLabel\": \"Negative\", \"Confidence\": 0.85 }");
        sb.AppendLine("]");
        sb.AppendLine("No extra text. No markdown. Raw JSON array only.");

        return sb.ToString();
    }

    // --- Gemini response deserialization models ---

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart>? Parts { get; set; }
    }

    private class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
