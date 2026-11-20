# Stock News Sentiment Tracker

A Clean Architecture .NET system that ingests financial news for tracked stock tickers, evaluates market sentiment in rate-limited batches, and aggregates daily sentiment trends.

## Architecture

The solution follows **Clean Architecture** with strict separation of concerns:

- **`Domain`** — Core entities (`Ticker`, `Article`, `ArticleScore`, `ScoringJob`, `TickerDailySummary`, `UserTicker`) and domain enums.
- **`Application`** — Business use cases (`FetchArticlesUseCase`, `ProcessScoringUseCase`, `CreateDailyAggregateUseCase`, `RetryFailedJobsUseCase`, cleanup & dashboard queries), DTOs, and port interfaces (`IAppDBContext`, `INewsAPI`, `ISentimentLLM`, `IJobTriggerService`).
- **`Infrastructure`** — EF Core + SQL Server (`AppDbContext`, entity configurations, migrations, `DbSeeder`), Finnhub news API integration (`FinnhubNewsApiService`), LLM sentiment scoring (`SentimentLlmService`), and queue-based job triggering (`QueueJobTriggerService`).
- **`ServerlessWorker`** — Azure Functions workers for scheduled ingestion, batch scoring, daily aggregation, and data retention cleanup (`ArticleFetchingFunction`, `ScoringFunction`, `DailyAggregationFunction`, `DataCleanupFunction`).
- **`Web`** — ASP.NET Core web application & dashboard behind `nginx` (Dockerized).
- **`Application.Tests`** — Unit test suite using **xUnit** and **Moq** with an in-memory test `DbContext`.

## Key Engineering Highlights

- **Batching & Rate-Limit Control:** `ProcessScoringUseCase` groups pending jobs by ticker, enforces daily processing quotas, and throttles batch requests to respect external API limits.
- **Fault Tolerance & Retries:** Failed scoring jobs capture structured error diagnostics and support selective or bulk retry workflows (`RetryFailedJobsUseCase`, `RetryAllFailedJobsUseCase`).
- **Automated Pipeline:** Decoupled serverless functions handle periodic fetching, async scoring, daily OHLC-style sentiment aggregation, and raw data cleanup.

## Tech Stack

- **Language & Runtime:** C# / .NET
- **Backend & Workers:** ASP.NET Core, Azure Functions (`ServerlessWorker`), Entity Framework Core, SQL Server
- **External Integrations:** Finnhub Stock News API, LLM Sentiment Scoring API
- **Testing:** xUnit, Moq
- **Infrastructure:** Docker, Nginx, GitHub Actions CI

## Running Tests

```bash
dotnet test
```
