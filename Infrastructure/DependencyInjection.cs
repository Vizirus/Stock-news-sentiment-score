using Application.Interfaces;
using Infrastructure.DB;
using Infrastructure.ExternalServices;
using Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Database Configuration — switch between SQLite (dev/test) and SQL Server (prod)
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);

                    sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                });
            }
        });

        services.AddScoped<IAppDBContext>(provider => provider.GetRequiredService<AppDbContext>());

        // 2. Options Configuration
        services.Configure<NewsApiOptions>(configuration.GetSection("NewsApi"));
        services.Configure<SentimentLlmOptions>(configuration.GetSection("SentimentLlm"));

        // 3. News API Service with HttpClient
        services.AddHttpClient<INewsAPI, FinnhubNewsApiService>((provider, client) =>
        {
            var options = configuration.GetSection("NewsApi").Get<NewsApiOptions>();
            if (options != null && !string.IsNullOrEmpty(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            }
        });

        // 4. LLM Service with HttpClient
        services.AddHttpClient<ISentimentLLM, SentimentLlmService>();

        return services;
    }
}
