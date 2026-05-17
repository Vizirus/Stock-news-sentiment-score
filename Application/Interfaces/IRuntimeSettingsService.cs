using Application.DTOs;

namespace Application.Interfaces;

public interface IRuntimeSettingsService
{
    RuntimeSettingsDto GetSettings();

    Task ReloadAsync(CancellationToken token = default);
}
