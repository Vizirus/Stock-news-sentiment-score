using Application.DTOs;
using Application.Interfaces;
using Application.Options;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Application.Services;

public class RuntimeSettingsService : IRuntimeSettingsService
{
    private RuntimeSettingsDto _cachedSettings = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProcessingLimitsOptions _limits;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public RuntimeSettingsService(IServiceScopeFactory scopeFactory, IOptions<ProcessingLimitsOptions> limits)
    {
        _scopeFactory = scopeFactory;
        _limits = limits.Value;
    }

    public RuntimeSettingsDto GetSettings()
    {
        return _cachedSettings;
    }

    public async Task ReloadAsync(CancellationToken token = default)
    {
        await _semaphore.WaitAsync(token);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDBContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            
            var adminUser = await userManager.FindByEmailAsync("My_Admin@local.com");
            UserSettings? dbSettings = null;
            if (adminUser != null)
            {
                dbSettings = await dbContext.UserSettings.FirstOrDefaultAsync(s => s.UserId == adminUser.Id, token);
            }
            
            if (dbSettings != null)
            {
                _cachedSettings = new RuntimeSettingsDto
                {
                    DailyLlmCallLimit = Math.Clamp(dbSettings.DailyLlmCallLimit, 1, _limits.MaxDailyLlmCalls),
                    BatchSize = Math.Clamp(dbSettings.BatchSize, 1, _limits.MaxBatchSize),
                    FetchIntervalHours = Math.Clamp(dbSettings.FetchIntervalHours, 1, _limits.MaxFetchIntervalHours)
                };
            }
            else
            {
                // Fallback to defaults if missing
                _cachedSettings = new RuntimeSettingsDto
                {
                    DailyLlmCallLimit = Math.Clamp(100, 1, _limits.MaxDailyLlmCalls),
                    BatchSize = Math.Clamp(20, 1, _limits.MaxBatchSize),
                    FetchIntervalHours = Math.Clamp(6, 1, _limits.MaxFetchIntervalHours)
                };
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
