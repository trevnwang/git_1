using System.Collections.Concurrent;

namespace MbdGitTool.Core.Files;

/// <summary>
/// 进程内 TTL 缓存（code-plan §4.11）：线程安全、容量超限整体失效（M1 量级下最简且无锁竞争）。
/// </summary>
public sealed class TtlCache<TValue>
    where TValue : class
{
    private readonly ConcurrentDictionary<string, Entry> _items = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl;
    private readonly int _capacity;

    public TtlCache(TimeSpan ttl, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _ttl = ttl;
        _capacity = capacity;
    }

    public int Count => _items.Count;

    public bool TryGet(string key, out TValue value)
    {
        if (_items.TryGetValue(key, out var entry))
        {
            if (DateTimeOffset.UtcNow - entry.StoredAt <= _ttl)
            {
                value = entry.Value;
                return true;
            }

            _items.TryRemove(key, out _);
        }

        value = null!;
        return false;
    }

    public void Set(string key, TValue value)
    {
        if (_items.Count >= _capacity)
        {
            _items.Clear();
        }

        _items[key] = new Entry(value, DateTimeOffset.UtcNow);
    }

    public void Invalidate(string key) => _items.TryRemove(key, out _);

    public void InvalidateAll() => _items.Clear();

    private sealed record Entry(TValue Value, DateTimeOffset StoredAt);
}
