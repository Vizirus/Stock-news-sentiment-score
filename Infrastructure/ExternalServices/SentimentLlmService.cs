using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs;
using Application.Interfaces;
using Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace Infrastructure.ExternalServices;

/// <summary>
/// Infrastructure implementation of ISentimentLLM using the OpenAI SDK.
/// Sends article batches in a single prompt and parses a JSON array response.
/// </summary>
public class SentimentLlmService : ISentimentLLM
{
    private readonly SentimentLlmOptions _options;
    private readonly ILogger<SentimentLlmService> _logger;

    public SentimentLlmService(
        IOptions<SentimentLlmOptions> options,
        ILogger<SentimentLlmService> logger)
    {
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

        _logger.LogInformation(
            "Sending batch of {Count} articles to OpenAI for ticker {Ticker}",
            articles.Count, ticker);

        ChatCompletion completion;
        try
        {
            var chatClient = new ChatClient("gpt-4o-mini", _options.ApiKey);
            
            completion = await chatClient.CompleteChatAsync(
                [new UserChatMessage(prompt)],
                new ChatCompletionOptions { Temperature = 0.1f },
                cancellationToken: token);
        }
        catch (Exception ex) when (ex.Message.Contains("429") || (ex is System.ClientModel.ClientResultException cre && cre.Status == 429))
        {
            _logger.LogWarning(ex, "Rate limit hit for ticker {Ticker}", ticker);
            // Throw HttpRequestException so the ProcessScoringUseCase's existing 429 retry logic seamlessly catches it!
            throw new HttpRequestException("429 Too Many Requests from OpenAI", ex, System.Net.HttpStatusCode.TooManyRequests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request to OpenAI failed for ticker {Ticker}", ticker);
            throw;
        }

        var jsonText = completion?.Content?.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(jsonText))
        {
            throw new InvalidOperationException(
                $"OpenAI returned an empty result for ticker {ticker}.");
        }

        // GPT-4o often wraps the JSON in markdown blocks
        if (jsonText.StartsWith("```json"))
        {
            jsonText = jsonText.Substring(7);
            if (jsonText.EndsWith("```"))
            {
                jsonText = jsonText.Substring(0, jsonText.Length - 3);
            }
            jsonText = jsonText.Trim();
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
            _logger.LogError(ex, "Failed to deserialize OpenAI response for ticker {Ticker}. Raw: {Json}", ticker, jsonText);
            throw new InvalidOperationException($"Failed to deserialize OpenAI sentiment response: {ex.Message}", ex);
        }

        if (results is null || results.Count == 0)
        {
            throw new InvalidOperationException(
                $"OpenAI returned no results for ticker {ticker}.");
        }

        _logger.LogInformation(
            "Received {Count} results from OpenAI for ticker {Ticker}",
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
        sb.AppendLine("      0.0      | Irrelevant");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT: If the article is primarily about the broader market, a competitor, or only mentions the company in passing, you MUST return a score of 0 and the label \"Irrelevant\".");
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
        sb.AppendLine("  { \"Index\": 1, \"Score\": 0.0, \"ScoreLabel\": \"Irrelevant\", \"Confidence\": 0.99 }");
        sb.AppendLine("]");
        sb.AppendLine("No extra text. No markdown. Raw JSON array only.");

        return sb.ToString();
    }

}
