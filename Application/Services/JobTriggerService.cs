using System.Threading.Channels;
using Application.Interfaces;

namespace Application.Services;

public class JobTriggerService : IJobTriggerService
{
    private readonly Channel<bool> _channel;

    public JobTriggerService()
    {
        // Bounded channel to prevent memory leaks if triggered too many times rapidly
        var options = new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        };
        _channel = Channel.CreateBounded<bool>(options);
    }

    public void TriggerScoringJob()
    {
        _channel.Writer.TryWrite(true);
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _channel.Reader.ReadAsync(cancellationToken);
    }
}
