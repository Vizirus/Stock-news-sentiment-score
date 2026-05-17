using Application.DTOs;

namespace Application.Interfaces;

public interface ISentimentLLM
{
    /// <summary>
    /// Scores a batch of articles for a given company in a single API call.
    /// Returns one result per input article, matched by Index.
    /// </summary>
    Task<List<SentimentResultDto>> ScoreArticlesAsync(
        string ticker,
        string companyName,
        IReadOnlyList<ArticleInputDto> articles,
        CancellationToken token = default);
}
