namespace Application.Options;

public class ProcessingLimitsOptions
{
    public int MaxDailyLlmCalls { get; set; } = 1000;

    public int MaxBatchSize { get; set; } = 50;

    public int MaxFetchIntervalHours { get; set; } = 24;
}
