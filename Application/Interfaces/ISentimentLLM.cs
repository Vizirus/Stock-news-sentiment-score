using Application.DTOs;
using Domain.Entities;

namespace Application.Interfaces;

public interface ISentimentLLM
{
    Task<SentimentResultDto> ScoreArticles(string ticker, string companyName, string title, string description, string Url, CancellationToken token = default);
}
