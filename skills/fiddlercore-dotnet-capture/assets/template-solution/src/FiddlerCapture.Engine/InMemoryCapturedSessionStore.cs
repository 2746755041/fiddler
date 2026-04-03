using Microsoft.Extensions.Options;

namespace FiddlerCapture.Engine;

public sealed class InMemoryCapturedSessionStore : ICapturedSessionStore
{
    private readonly object _gate = new();
    private readonly List<CapturedSessionRecord> _sessions = [];
    private readonly IOptionsMonitor<FiddlerCaptureOptions> _optionsMonitor;

    public InMemoryCapturedSessionStore(IOptionsMonitor<FiddlerCaptureOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _sessions.Count;
            }
        }
    }

    public void Add(CapturedSessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_gate)
        {
            _sessions.Add(session);

            var maxCachedSessions = Math.Max(1, _optionsMonitor.CurrentValue.MaxCachedSessions);
            var overflow = _sessions.Count - maxCachedSessions;

            if (overflow > 0)
            {
                _sessions.RemoveRange(0, overflow);
            }
        }
    }

    public IReadOnlyList<CapturedSessionRecord> Query(CaptureQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        lock (_gate)
        {
            IEnumerable<CapturedSessionRecord> filtered = _sessions;

            if (!string.IsNullOrWhiteSpace(query.HostContains))
            {
                filtered = filtered.Where(session =>
                    session.Host.Contains(query.HostContains, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.UrlContains))
            {
                filtered = filtered.Where(session =>
                    session.Url.Contains(query.UrlContains, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Method))
            {
                filtered = filtered.Where(session =>
                    string.Equals(session.Method, query.Method, StringComparison.OrdinalIgnoreCase));
            }

            if (query.MinStatusCode is not null)
            {
                filtered = filtered.Where(session => session.StatusCode >= query.MinStatusCode.Value);
            }

            if (query.MaxStatusCode is not null)
            {
                filtered = filtered.Where(session => session.StatusCode <= query.MaxStatusCode.Value);
            }

            var limit = Math.Max(1, query.Limit);

            return filtered
                .OrderByDescending(session => session.CapturedAtUtc)
                .Take(limit)
                .ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _sessions.Clear();
        }
    }
}
