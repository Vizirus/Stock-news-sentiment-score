namespace Domain.Entities;

public class UserTicker
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int TickerId { get; set; }
    public Ticker Ticker { get; set; } = null!;
}
