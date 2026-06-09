namespace Application.Interfaces;

public interface IJobTriggerService
{
    Task TriggerScoringJobAsync();
}
