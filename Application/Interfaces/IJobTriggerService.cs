namespace Application.Interfaces;

public interface IJobTriggerService
{
    void TriggerScoringJob();
    Task WaitAsync(CancellationToken cancellationToken);
}
