using System.Collections.Concurrent;

namespace HenryTracker.Services;

/// <summary>
/// Very small in-memory brute-force guard for the login endpoint: after a
/// handful of wrong-password attempts from the same client, further
/// attempts are rejected for a cooldown period. State is per-process and
/// resets on restart, which is fine for a single-user app behind a home
/// network or reverse proxy.
/// </summary>
public class LoginThrottle
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private class Entry
    {
        public int Failures;
        public DateTime LockedUntilUtc;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public bool IsLockedOut(string key, out TimeSpan retryAfter)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            var remaining = entry.LockedUntilUtc - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                retryAfter = remaining;
                return true;
            }
        }

        retryAfter = TimeSpan.Zero;
        return false;
    }

    public void RecordFailure(string key)
    {
        var entry = _entries.GetOrAdd(key, _ => new Entry());
        lock (entry)
        {
            entry.Failures++;
            if (entry.Failures >= MaxFailures)
            {
                entry.LockedUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
                entry.Failures = 0;
            }
        }
    }

    public void Reset(string key) => _entries.TryRemove(key, out _);
}
