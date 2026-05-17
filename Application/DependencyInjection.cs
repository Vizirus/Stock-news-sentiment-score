using Application.Interfaces;
using Application.Services;
using Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetDashboardDataUseCase>();
        services.AddScoped<GetTickersUseCase>();
        services.AddScoped<AddTickerUseCase>();
        services.AddScoped<RetryFailedJobsUseCase>();
        services.AddScoped<GetScoringJobsUseCase>();
        services.AddScoped<RetryAllFailedJobsUseCase>();
        services.AddScoped<GetSummariesUseCase>();
        services.AddSingleton<IRuntimeSettingsService, RuntimeSettingsService>();
        services.AddScoped<ProcessScoringUseCase>();
        services.AddScoped<FetchArticlesUseCase>();
        services.AddScoped<CreateDailyAggregationUseCase>();
        services.AddScoped<SummaryDataCleanUpUseCase>();
        services.AddScoped<RawDataCleanUpUseCase>();

        // Background Job Services
        services.AddSingleton<Application.Interfaces.IJobTriggerService, Application.Services.JobTriggerService>();

        return services;
    }
}
