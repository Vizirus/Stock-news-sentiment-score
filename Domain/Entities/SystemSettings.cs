namespace Domain.Entities;

public class SystemSettings
{
    public int Id { get; set; }

    public int DailyLlmCallLimit { get; set; } = 100;

    public int BatchSize { get; set; } = 20;

    public int FetchIntervalHours { get; set; } = 6;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastArticleFetchTime { get; set; }
}
