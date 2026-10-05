using System.Threading.Channels;

namespace Elyndor.Infrastructure.Pvp;

/// <summary>
/// Coalesces queue mutations into a single wake-up for the matchmaking worker.
/// The bounded channel prevents bursts of joins from creating an unbounded work backlog.
/// </summary>
public sealed class ArenaMatchmakingSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });

    public void Pulse() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan reconciliationInterval, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(reconciliationInterval);

        try
        {
            _ = await _channel.Reader.ReadAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Periodic reconciliation is the safety net for process restarts or a lost/coalesced pulse.
        }
    }
}
