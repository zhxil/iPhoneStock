namespace Stock.Core;

public sealed class ArrivalTracker
{
    private readonly Dictionary<Target, StockKind> previous = [];
    public bool Update(Result result)
    {
        var hit = result.Kind == StockKind.InStock && (!previous.TryGetValue(result.Target, out var old) || old != StockKind.InStock);
        previous[result.Target] = result.Kind;
        return hit;
    }
}

public sealed class Monitor(AppleClient client, Catalog catalog)
{
    private readonly SemaphoreSlim roundGate = new(1);
    private readonly ArrivalTracker arrivals = new();
    public event Action<IReadOnlyList<Result>>? Results;
    public event Action<Result>? Arrived;
    public event Action<DateTimeOffset>? Scheduled;
    public async Task CheckOnceAsync(Settings settings, CancellationToken token)
    {
        if (!await roundGate.WaitAsync(0, token)) return;
        try
        {
            foreach (var group in settings.Targets.GroupBy(t => catalog.Stores.FirstOrDefault(s => s.Number == t.StoreNumber)?.Location ?? "山东 青岛"))
            {
                var wait = client.NextAllowed - DateTimeOffset.UtcNow;
                if (wait > TimeSpan.Zero) { Scheduled?.Invoke(client.NextAllowed); await WaitUntilAsync(client.NextAllowed, token); }
                var results = await client.CheckAsync(group.ToArray(), group.Key, token);
                Results?.Invoke(results);
                foreach (var result in results) if (arrivals.Update(result)) Arrived?.Invoke(result);
            }
        }
        finally { roundGate.Release(); }
    }
    public async Task RunAsync(Func<Settings> settings, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await CheckOnceAsync(settings(), token);
            var next = DateTimeOffset.UtcNow.AddSeconds(settings().IntervalSeconds);
            if (client.NextAllowed > next) next = client.NextAllowed;
            Scheduled?.Invoke(next);
            await WaitUntilAsync(next, token);
        }
    }
    private static async Task WaitUntilAsync(DateTimeOffset next, CancellationToken token)
    {
        // Long Retry-After values must not overflow Task.Delay's supported duration.
        while (next > DateTimeOffset.UtcNow)
        {
            token.ThrowIfCancellationRequested();
            var remaining = next - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) return;
            await Task.Delay(remaining > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : remaining, token);
        }
    }

}
