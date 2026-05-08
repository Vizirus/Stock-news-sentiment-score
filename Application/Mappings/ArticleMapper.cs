using System.Linq.Expressions;
using Application.DTOs;
using Domain.Entities;

namespace Application.Mappings;

public static class ArticleMapper
{
    public static readonly Expression<Func<Article, FetchedArticleDto>> ToFetchedArticleDtoExpr =
        article => new FetchedArticleDto
        {
            Title = article.Title,
            Description = article.Description,
            Url = article.Url,
            SourceName = article.SourceName,
            PublishedAt = article.PublishedAt,
            CreatedAt = article.CreatedAt
        };

    public static readonly Expression<Func<ArticleScore, RecentArticleDto>> ToRecentArticleDtoExpr =
        articleScore => new RecentArticleDto
        {
            Title = articleScore.Article.Title,
            SourceName = articleScore.Article.SourceName,
            PublishedAt = articleScore.Article.PublishedAt,
            Url = articleScore.Article.Url,
            Score = articleScore.Score,
            ScoreLabel = articleScore.ScoreLabel ?? string.Empty,
            Confidence = articleScore.Confidence
        };

    public static FetchedArticleDto ToFetchedArticleDto(this Article article) =>
        new()
        {
            Title = article.Title,
            Description = article.Description,
            Url = article.Url,
            SourceName = article.SourceName,
            PublishedAt = article.PublishedAt,
            CreatedAt = article.CreatedAt
        };

    public static RecentArticleDto ToRecentArticleDto(this ArticleScore articleScore) =>
        new()
        {
            Title = articleScore.Article.Title,
            SourceName = articleScore.Article.SourceName,
            PublishedAt = articleScore.Article.PublishedAt,
            Url = articleScore.Article.Url,
            Score = articleScore.Score,
            ScoreLabel = articleScore.ScoreLabel ?? string.Empty,
            Confidence = articleScore.Confidence
        };

    public static RecentArticleDto ToRecentArticleDto(
        this Article article,
        decimal score,
        string? scoreLabel,
        decimal confidence) =>
        new()
        {
            Title = article.Title,
            SourceName = article.SourceName,
            PublishedAt = article.PublishedAt,
            Url = article.Url,
            Score = score,
            ScoreLabel = scoreLabel ?? string.Empty,
            Confidence = confidence
        };
}
