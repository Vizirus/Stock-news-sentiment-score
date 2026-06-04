namespace Domain.Entities;

public class UserSettings
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int DailyLlmCallLimit { get; set; } = 100;

    public int BatchSize { get; set; } = 20;

    public int FetchIntervalHours { get; set; } = 6;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
