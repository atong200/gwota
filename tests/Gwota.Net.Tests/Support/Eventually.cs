namespace Gwota.Net.Tests.Support;

/// <summary>Waits for something done on another thread (sockets, timers) to become true.</summary>
public static class Eventually
{
    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(5);

    public static async Task True(Func<bool> condition, TimeSpan? wait = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var deadline = DateTime.UtcNow + (wait ?? DefaultWait);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition not met in time.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
