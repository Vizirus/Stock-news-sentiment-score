using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.DB;

/// <summary>
/// Seeds the SQLite development database with representative mock data on startup.
/// Only runs when using the SQLite provider (development/testing).
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        // EnsureCreated creates the schema from the model — no migrations needed for SQLite dev
        await db.Database.EnsureCreatedAsync();

        logger.LogInformation("Seeding SQLite development database...");
 
        // --- Identity ---
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
 
        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
 
        // Clean up any improperly registered My_Admin user without the admin role
        var incorrectUserByEmail = await userManager.FindByEmailAsync("My_Admin@local.com");
        if (incorrectUserByEmail != null && !await userManager.IsInRoleAsync(incorrectUserByEmail, "Admin"))
        {
            logger.LogWarning("Deleting incorrectly registered non-admin user: {Email}", incorrectUserByEmail.Email);
            await userManager.DeleteAsync(incorrectUserByEmail);
        }
 
        var incorrectUserByName = await userManager.FindByNameAsync("My_Admin");
        if (incorrectUserByName != null && !await userManager.IsInRoleAsync(incorrectUserByName, "Admin"))
        {
            logger.LogWarning("Deleting incorrectly registered non-admin user: {UserName}", incorrectUserByName.UserName);
            await userManager.DeleteAsync(incorrectUserByName);
        }
 
        var adminEmail = "My_Admin@local.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                Name = "System",
                Surname = "Admin",
                EmailConfirmed = true
            };

            var adminPassword = configuration["AdminPassword"] ?? "BaBeLo$12";
            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Admin user seeded successfully with 'Admin' role.");
            }
            else
            {
                logger.LogError("Failed to seed admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        if (adminUser != null)
        {
            var adminSettings = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == adminUser.Id);
            if (adminSettings == null)
            {
                db.UserSettings.Add(new UserSettings
                {
                    UserId = adminUser.Id,
                    DailyLlmCallLimit = 100,
                    BatchSize = 20,
                    FetchIntervalHours = 6,
                    UpdatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
                logger.LogInformation("Admin default user settings seeded successfully.");
            }
        }
 
        // --- Tickers ---
        if (!await db.Ticker.AnyAsync())
        {
            var tickers = new List<Ticker>
            {
                new() { Id = 1, Symbol = "AAPL",  CompanyName = "Apple Inc." },
                new() { Id = 2, Symbol = "MSFT",  CompanyName = "Microsoft Corp." },
                new() { Id = 3, Symbol = "TSLA",  CompanyName = "Tesla Inc." },
                new() { Id = 4, Symbol = "NVDA",  CompanyName = "NVIDIA Corp." },
                new() { Id = 5, Symbol = "AMZN",  CompanyName = "Amazon.com Inc." },
            };
            db.Ticker.AddRange(tickers);
            await db.SaveChangesAsync();
            logger.LogInformation("Tickers seeded successfully.");
        }


        // --- Articles ---
        if (!await db.Article.AnyAsync())
        {
            var now = DateTime.UtcNow;
            var articles = new List<Article>
            {
                new() { Id = 1,  Title = "Apple unveils new AI features across iPhone, iPad, and Mac",      Description = "Apple announced major AI updates at WWDC 2025.",                       Url = "https://techcrunch.com/1", SourceName = "TechCrunch", PublishedAt = now.AddHours(-2),  CreatedAt = now.AddHours(-2) },
                new() { Id = 2,  Title = "iPhone demand softens in key markets, analyst says",               Description = "A Bloomberg analyst report signals slowing iPhone demand in Asia.",    Url = "https://bloomberg.com/2",  SourceName = "Bloomberg",  PublishedAt = now.AddHours(-4),  CreatedAt = now.AddHours(-4) },
                new() { Id = 3,  Title = "Apple stock rises as services revenue hits record",                Description = "Apple's services segment hit an all-time high in Q2 2025.",            Url = "https://cnbc.com/3",       SourceName = "CNBC",       PublishedAt = now.AddHours(-6),  CreatedAt = now.AddHours(-6) },
                new() { Id = 4,  Title = "Microsoft Azure revenue grows 28% year-over-year",                Description = "Microsoft's cloud division continues to post strong growth.",           Url = "https://theverge.com/4",   SourceName = "The Verge",  PublishedAt = now.AddHours(-5),  CreatedAt = now.AddHours(-5) },
                new() { Id = 5,  Title = "Tesla recalls 125,000 vehicles over software defect",             Description = "NHTSA confirms Tesla is recalling Model 3 and Model Y vehicles.",      Url = "https://reuters.com/5",    SourceName = "Reuters",    PublishedAt = now.AddHours(-8),  CreatedAt = now.AddHours(-8) },
                new() { Id = 6,  Title = "NVIDIA smashes Q1 earnings expectations, raises guidance",        Description = "NVIDIA reported record revenue of $26B, beating analyst estimates.",   Url = "https://cnbc.com/6",       SourceName = "CNBC",       PublishedAt = now.AddHours(-3),  CreatedAt = now.AddHours(-3) },
                new() { Id = 7,  Title = "Amazon expands same-day delivery to 20 new cities",               Description = "Amazon announced an expansion of its Prime same-day delivery network.",Url = "https://wsj.com/7",        SourceName = "WSJ",        PublishedAt = now.AddHours(-10), CreatedAt = now.AddHours(-10) },
                new() { Id = 8,  Title = "Apple suppliers see mixed results in Q2",                         Description = "Key Apple suppliers reported mixed Q2 earnings results.",              Url = "https://reuters.com/8",    SourceName = "Reuters",    PublishedAt = now.AddHours(-20), CreatedAt = now.AddHours(-20) },
                new() { Id = 9,  Title = "Microsoft announces Copilot integration in all Office products",  Description = "Microsoft is rolling out AI Copilot across the entire Office suite.",  Url = "https://theverge.com/9",   SourceName = "The Verge",  PublishedAt = now.AddDays(-1),   CreatedAt = now.AddDays(-1) },
                new() { Id = 10, Title = "NVIDIA announces H200 availability for cloud providers",          Description = "The H200 GPU is now available on AWS, Azure, and GCP.",               Url = "https://arstechnica.com/10",SourceName = "Ars Technica",PublishedAt = now.AddDays(-1),  CreatedAt = now.AddDays(-1) },
                // --- 8 New Articles (Non-Apple) ---
                new() { Id = 11, Title = "Microsoft strikes new partnership for Azure AI expansion",        Description = "Microsoft announces a major deal to expand its AI infrastructure globally.", Url = "https://techcrunch.com/11",SourceName = "TechCrunch",PublishedAt = now.AddHours(-1), CreatedAt = now.AddHours(-1) },
                new() { Id = 12, Title = "Tesla Cybertruck production hits milestone",                      Description = "Tesla announced it has reached its production target for the Cybertruck.", Url = "https://bloomberg.com/12",SourceName = "Bloomberg", PublishedAt = now.AddHours(-2), CreatedAt = now.AddHours(-2) },
                new() { Id = 13, Title = "Amazon AWS reports 35% growth driven by AI demand",               Description = "AWS continues its massive growth trajectory thanks to new AI workloads.",  Url = "https://cnbc.com/13",    SourceName = "CNBC",      PublishedAt = now.AddHours(-4), CreatedAt = now.AddHours(-4) },
                new() { Id = 14, Title = "NVIDIA reveals next-gen architecture details",                    Description = "Jensen Huang teased the next generation of NVIDIA GPUs at a tech summit.", Url = "https://theverge.com/14",SourceName = "The Verge", PublishedAt = now.AddHours(-5), CreatedAt = now.AddHours(-5) },
                new() { Id = 15, Title = "Tesla faces new regulatory probe in Europe",                      Description = "European regulators are looking into Tesla's autopilot claims.",           Url = "https://reuters.com/15", SourceName = "Reuters",   PublishedAt = now.AddHours(-7), CreatedAt = now.AddHours(-7) },
                new() { Id = 16, Title = "Amazon expands drone delivery to UK and Italy",                   Description = "Prime Air drone delivery is expanding internationally later this year.",   Url = "https://wsj.com/16",     SourceName = "WSJ",       PublishedAt = now.AddHours(-9), CreatedAt = now.AddHours(-9) },
                new() { Id = 17, Title = "Microsoft introduces new Surface devices with ARM chips",         Description = "The new Surface laptops feature the latest Snapdragon processors.",        Url = "https://arstechnica.com/17",SourceName = "Ars Technica",PublishedAt = now.AddDays(-2),CreatedAt = now.AddDays(-2) },
                new() { Id = 18, Title = "NVIDIA stock splits 10-for-1 following massive rally",            Description = "NVIDIA announced a stock split to make shares more accessible to retail.", Url = "https://cnbc.com/18",    SourceName = "CNBC",      PublishedAt = now.AddDays(-2),CreatedAt = now.AddDays(-2) },
            };
            db.Article.AddRange(articles);
 
            // --- Scoring Jobs ---
            var jobs = new List<ScoringJob>
            {
                new() { Id = 1,  TickerId = 1, ArticleId = 1,  StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddHours(-2),  StartedAt = now.AddHours(-2),  CompletdAt = now.AddHours(-2) },
                new() { Id = 2,  TickerId = 1, ArticleId = 2,  StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddHours(-4),  StartedAt = now.AddHours(-4),  CompletdAt = now.AddHours(-4) },
                new() { Id = 3,  TickerId = 1, ArticleId = 3,  StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddHours(-6),  StartedAt = now.AddHours(-6),  CompletdAt = now.AddHours(-6) },
                new() { Id = 4,  TickerId = 2, ArticleId = 4,  StatusId = ScoringJobStatus.Pending,   CreatedAt = now.AddHours(-5) },
                new() { Id = 5,  TickerId = 3, ArticleId = 5,  StatusId = ScoringJobStatus.Failed,    CreatedAt = now.AddHours(-8),  StartedAt = now.AddHours(-8),  ErrorMessage = "Gemini API rate limit exceeded" },
                new() { Id = 6,  TickerId = 4, ArticleId = 6,  StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddHours(-3),  StartedAt = now.AddHours(-3),  CompletdAt = now.AddHours(-3) },
                new() { Id = 7,  TickerId = 5, ArticleId = 7,  StatusId = ScoringJobStatus.Pending,   CreatedAt = now.AddHours(-10) },
                new() { Id = 8,  TickerId = 1, ArticleId = 8,  StatusId = ScoringJobStatus.Failed,    CreatedAt = now.AddHours(-20), StartedAt = now.AddHours(-20), ErrorMessage = "Deserialization error: unexpected JSON token" },
                new() { Id = 9,  TickerId = 2, ArticleId = 9,  StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddDays(-1),   StartedAt = now.AddDays(-1),   CompletdAt = now.AddDays(-1) },
                new() { Id = 10, TickerId = 4, ArticleId = 10, StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddDays(-1),   StartedAt = now.AddDays(-1),   CompletdAt = now.AddDays(-1) },
                new() { Id = 11, TickerId = 2, ArticleId = 11, StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddHours(-1),  StartedAt = now.AddHours(-1),  CompletdAt = now.AddHours(-1) },
                new() { Id = 12, TickerId = 3, ArticleId = 12, StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddHours(-2),  StartedAt = now.AddHours(-2),  CompletdAt = now.AddHours(-2) },
                new() { Id = 13, TickerId = 5, ArticleId = 13, StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddHours(-4),  StartedAt = now.AddHours(-4),  CompletdAt = now.AddHours(-4) },
                new() { Id = 14, TickerId = 4, ArticleId = 14, StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddHours(-5),  StartedAt = now.AddHours(-5),  CompletdAt = now.AddHours(-5) },
                new() { Id = 15, TickerId = 3, ArticleId = 15, StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddHours(-7),  StartedAt = now.AddHours(-7),  CompletdAt = now.AddHours(-7) },
                new() { Id = 16, TickerId = 5, ArticleId = 16, StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddHours(-9),  StartedAt = now.AddHours(-9),  CompletdAt = now.AddHours(-9) },
                new() { Id = 17, TickerId = 2, ArticleId = 17, StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddDays(-2),   StartedAt = now.AddDays(-2),   CompletdAt = now.AddDays(-2) },
                new() { Id = 18, TickerId = 4, ArticleId = 18, StatusId = ScoringJobStatus.Completed, CreatedAt = now.AddDays(-2),   StartedAt = now.AddDays(-2),   CompletdAt = now.AddDays(-2) },
            };
            db.ScoringJobs.AddRange(jobs);
 
            // --- Article Scores ---
            var scores = new List<ArticleScore>
            {
                new() { Id = 1, ArticleId = 1, TickerId = 1, Score = 0.68m,  ScoreLabel = "Positive",      Confidence = 0.86m, ScoredAt = now.AddHours(-2) },
                new() { Id = 2, ArticleId = 2, TickerId = 1, Score = -0.45m, ScoreLabel = "Negative",      Confidence = 0.79m, ScoredAt = now.AddHours(-4) },
                new() { Id = 3, ArticleId = 3, TickerId = 1, Score = 0.42m,  ScoreLabel = "Positive",      Confidence = 0.81m, ScoredAt = now.AddHours(-6) },
                new() { Id = 4, ArticleId = 6, TickerId = 4, Score = 0.91m,  ScoreLabel = "Very Positive", Confidence = 0.97m, ScoredAt = now.AddHours(-3) },
                new() { Id = 5, ArticleId = 9, TickerId = 2, Score = 0.74m,  ScoreLabel = "Positive",      Confidence = 0.91m, ScoredAt = now.AddDays(-1) },
                new() { Id = 6, ArticleId = 10,TickerId = 4, Score = 0.65m,  ScoreLabel = "Positive",      Confidence = 0.85m, ScoredAt = now.AddDays(-1) },
                new() { Id = 7, ArticleId = 11,TickerId = 2, Score = 0.82m,  ScoreLabel = "Positive",      Confidence = 0.92m, ScoredAt = now.AddHours(-1) },
                new() { Id = 8, ArticleId = 12,TickerId = 3, Score = 0.55m,  ScoreLabel = "Positive",      Confidence = 0.80m, ScoredAt = now.AddHours(-2) },
                new() { Id = 9, ArticleId = 13,TickerId = 5, Score = 0.60m,  ScoreLabel = "Positive",      Confidence = 0.88m, ScoredAt = now.AddHours(-4) },
                new() { Id = 10,ArticleId = 14,TickerId = 4, Score = 0.88m,  ScoreLabel = "Very Positive", Confidence = 0.95m, ScoredAt = now.AddHours(-5) },
                new() { Id = 11,ArticleId = 15,TickerId = 3, Score = -0.40m, ScoreLabel = "Negative",      Confidence = 0.75m, ScoredAt = now.AddHours(-7) },
                new() { Id = 12,ArticleId = 16,TickerId = 5, Score = 0.35m,  ScoreLabel = "Positive",      Confidence = 0.65m, ScoredAt = now.AddHours(-9) },
                new() { Id = 13,ArticleId = 17,TickerId = 2, Score = 0.70m,  ScoreLabel = "Positive",      Confidence = 0.89m, ScoredAt = now.AddDays(-2) },
                new() { Id = 14,ArticleId = 18,TickerId = 4, Score = 0.95m,  ScoreLabel = "Very Positive", Confidence = 0.98m, ScoredAt = now.AddDays(-2) },
            };
            db.ArticleScores.AddRange(scores);
 
            // --- Ticker Daily Summaries ---
            var summaries = new List<TickerDailySummary>
            {
                new() { Id = 1, TickerId = 1, SummaryDate = now.Date,              AverageScore = 0.32m, ArticleCount = 3, UpdatedAt = now },
                new() { Id = 2, TickerId = 1, SummaryDate = now.AddDays(-1).Date,  AverageScore = 0.15m, ArticleCount = 2, UpdatedAt = now },
                new() { Id = 3, TickerId = 2, SummaryDate = now.Date,              AverageScore = 0.47m, ArticleCount = 1, UpdatedAt = now },
                new() { Id = 4, TickerId = 4, SummaryDate = now.Date,              AverageScore = 0.78m, ArticleCount = 2, UpdatedAt = now },
            };
 
            // Add fake historical data back to early April for testing the graphs for ALL tickers
            var random = new Random(42);
            int nextId = 5;
            for (int t = 1; t <= 5; t++) // For each TickerId
            {
                for (int i = 2; i <= 40; i++) // Go back 40 days
                {
                    summaries.Add(new TickerDailySummary
                    {
                        Id = nextId++,
                        TickerId = t,
                        SummaryDate = now.AddDays(-i).Date,
                        AverageScore = (decimal)(random.NextDouble() * 1.8 - 0.9), // Random score between -0.9 and 0.9
                        ArticleCount = random.Next(1, 15),
                        UpdatedAt = now
                    });
                }
            }
 
            db.TickerDailySummaries.AddRange(summaries);
 
            await db.SaveChangesAsync();
            logger.LogInformation("Database seeded successfully with {Articles} articles, {Jobs} jobs, {Scores} scores.",
                articles.Count, jobs.Count, scores.Count);
        }
    }
}
