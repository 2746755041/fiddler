using System.ComponentModel;
using System.Globalization;
using FiddlerCapture.Engine;
using Microsoft.SemanticKernel;

namespace FiddlerCapture.SemanticKernel;

[Description("Operate the FiddlerCore capture engine and summarize captured HTTP or HTTPS traffic for debugging, packet capture, and report generation.")]
public sealed class NetworkCapturePlugin
{
    private readonly IFiddlerCaptureEngine _engine;
    private readonly ICapturedSessionStore _store;

    public NetworkCapturePlugin(IFiddlerCaptureEngine engine, ICapturedSessionStore store)
    {
        _engine = engine;
        _store = store;
    }

    [KernelFunction("get_capture_state")]
    [Description("Inspect whether live traffic capture is running and how many sessions are cached before deciding whether to start, stop, or analyze capture.")]
    public CaptureEngineState GetCaptureState()
    {
        return _engine.GetState();
    }

    [KernelFunction("start_capture")]
    [Description("Start the FiddlerCore capture engine when the user wants to begin live traffic capture and the proxy is not already running.")]
    public async Task<CaptureEngineState> StartCaptureAsync(CancellationToken cancellationToken = default)
    {
        await _engine.StartCaptureAsync(cancellationToken).ConfigureAwait(false);
        return _engine.GetState();
    }

    [KernelFunction("stop_capture")]
    [Description("Stop the FiddlerCore capture engine when capture should end or when proxy side effects need to be reverted.")]
    public async Task<CaptureEngineState> StopCaptureAsync(CancellationToken cancellationToken = default)
    {
        await _engine.StopCaptureAsync(cancellationToken).ConfigureAwait(false);
        return _engine.GetState();
    }

    [KernelFunction("clear_sessions")]
    [Description("Clear cached captured sessions before a new experiment or after a report is complete.")]
    public string ClearSessions()
    {
        _store.Clear();
        return "Captured session cache cleared.";
    }

    [KernelFunction("get_recent_sessions")]
    [Description("Return recent captured HTTP or HTTPS sessions when the user asks which requests were made, which endpoint failed, or which traffic should be inspected in detail.")]
    public IReadOnlyList<CapturedSessionRecord> GetRecentSessions(
        [Description("Maximum number of sessions to return. Keep this small unless the user explicitly asks for more.")] int limit = 20,
        [Description("Optional case-insensitive host filter.")] string? hostContains = null,
        [Description("Optional case-insensitive URL filter.")] string? urlContains = null,
        [Description("Optional HTTP method filter such as GET or POST.")] string? method = null,
        [Description("Optional minimum HTTP status code filter.")] int? minStatusCode = null,
        [Description("Optional maximum HTTP status code filter.")] int? maxStatusCode = null)
    {
        return _store.Query(new CaptureQuery
        {
            HostContains = hostContains,
            UrlContains = urlContains,
            Method = method,
            MinStatusCode = minStatusCode,
            MaxStatusCode = maxStatusCode,
            Limit = limit
        });
    }

    [KernelFunction("analyze_traffic")]
    [Description("Summarize captured traffic into counts by host, method, and status code, and produce concise findings about error spikes, redirects, or dominant endpoints.")]
    public TrafficAnalysisResult AnalyzeTraffic(
        [Description("Maximum number of recent sessions to inspect for the summary.")] int limit = 200,
        [Description("Optional case-insensitive host filter.")] string? hostContains = null,
        [Description("Optional case-insensitive URL filter.")] string? urlContains = null,
        [Description("Optional HTTP method filter.")] string? method = null,
        [Description("Optional minimum HTTP status code filter.")] int? minStatusCode = null,
        [Description("Optional maximum HTTP status code filter.")] int? maxStatusCode = null)
    {
        var sessions = _store.Query(new CaptureQuery
        {
            HostContains = hostContains,
            UrlContains = urlContains,
            Method = method,
            MinStatusCode = minStatusCode,
            MaxStatusCode = maxStatusCode,
            Limit = limit
        });

        var findings = BuildFindings(sessions);

        return new TrafficAnalysisResult
        {
            TotalSessions = sessions.Count,
            DistinctHosts = sessions.Select(session => session.Host)
                .Where(host => !string.IsNullOrWhiteSpace(host))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            ErrorSessions = sessions.Count(session => session.StatusCode >= 400),
            RedirectSessions = sessions.Count(session => session.StatusCode is >= 300 and < 400),
            Methods = sessions
                .GroupBy(session => session.Method, StringComparer.OrdinalIgnoreCase)
                .Select(group => new AggregateMetric { Key = group.Key, Count = group.Count() })
                .OrderByDescending(metric => metric.Count)
                .ToArray(),
            Hosts = sessions
                .GroupBy(session => session.Host, StringComparer.OrdinalIgnoreCase)
                .Select(group => new AggregateMetric { Key = group.Key, Count = group.Count() })
                .OrderByDescending(metric => metric.Count)
                .Take(10)
                .ToArray(),
            StatusCodes = sessions
                .GroupBy(session => session.StatusCode.ToString(CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase)
                .Select(group => new AggregateMetric { Key = group.Key, Count = group.Count() })
                .OrderByDescending(metric => metric.Count)
                .ToArray(),
            Findings = findings
        };
    }

    private static string[] BuildFindings(IReadOnlyList<CapturedSessionRecord> sessions)
    {
        var findings = new List<string>();

        if (sessions.Count == 0)
        {
            findings.Add("No sessions matched the current filters.");
            return findings.ToArray();
        }

        var topHost = sessions
            .Where(session => !string.IsNullOrWhiteSpace(session.Host))
            .GroupBy(session => session.Host, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();

        if (topHost is not null && topHost.Count() * 2 >= sessions.Count)
        {
            findings.Add($"Host '{topHost.Key}' accounts for most captured traffic.");
        }

        var errorSessions = sessions.Where(session => session.StatusCode >= 400).ToArray();
        if (errorSessions.Length > 0)
        {
            var topErrorEndpoint = errorSessions
                .GroupBy(session => session.PathAndQuery, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .First();

            findings.Add(
                $"HTTP errors are concentrated around '{topErrorEndpoint.Key}' with {topErrorEndpoint.Count()} failing sessions.");
        }

        var jsonFailures = errorSessions
            .Where(session => session.ResponseContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
            .Length;

        if (jsonFailures > 0)
        {
            findings.Add("Some failing responses are JSON APIs, which usually means the payloads contain actionable error details.");
        }

        var redirects = sessions.Count(session => session.StatusCode is >= 300 and < 400);
        if (redirects > 0 && redirects * 3 >= sessions.Count)
        {
            findings.Add("Redirect volume is high relative to the sampled traffic.");
        }

        return findings.ToArray();
    }
}
