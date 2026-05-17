namespace Application.DTOs;

public class RuntimeSettingsDto
{
    public int DailyLlmCallLimit { get; set; }

    public int BatchSize { get; set; }

    public int FetchIntervalHours { get; set; }
}
