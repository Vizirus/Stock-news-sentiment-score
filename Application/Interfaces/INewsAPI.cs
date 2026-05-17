using Application.DTOs;

namespace Application.Interfaces;

public interface INewsAPI
{
    Task<List<FetchedArticleDto>> GetArticlesForCompany(string ticker, DateTime fromDate, DateTime toDate, CancellationToken token = default);
}
