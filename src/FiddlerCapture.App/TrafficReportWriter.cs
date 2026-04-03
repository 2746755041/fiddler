using FiddlerCapture.Engine;
using FiddlerCapture.SemanticKernel;
using System.Globalization;
using System.Text;

internal static class TrafficReportWriter
{
    public static string BuildReport(
        FiddlerCaptureOptions options,
        TimeSpan duration,
        int limit,
        IReadOnlyList<CapturedSessionRecord> sessions,
        TrafficAnalysisResult analysis)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Capture scope");
        builder.AppendLine($"- Duration: {FormatDuration(duration)}");
        builder.AppendLine($"- Listen: 127.0.0.1:{options.ListenPort}");
        builder.AppendLine($"- System proxy: {options.RegisterAsSystemProxy}");
        builder.AppendLine($"- HTTPS decrypt: {options.DecryptHttps}");
        builder.AppendLine($"- Remote clients: {options.AllowRemoteClients}");
        builder.AppendLine($"- Body capture: {options.IncludeBodies} (preview {options.MaxBodyPreviewChars} chars)");
        builder.AppendLine($"- Cache limit: {options.MaxCachedSessions} sessions");
        builder.AppendLine();

        builder.AppendLine("Summary metrics");
        builder.AppendLine($"- Sampled sessions: {analysis.TotalSessions} (limit {limit})");
        builder.AppendLine($"- Distinct hosts: {analysis.DistinctHosts}");
        builder.AppendLine($"- Errors (>=400): {analysis.ErrorSessions}");
        builder.AppendLine($"- Redirects (3xx): {analysis.RedirectSessions}");
        builder.AppendLine();

        AppendAggregate(builder, "Top hosts", analysis.Hosts);
        AppendAggregate(builder, "Methods", analysis.Methods);
        AppendAggregate(builder, "Status codes", analysis.StatusCodes);

        AppendEndpoints(builder, sessions, "Top endpoints", includeErrors: false);
        AppendEndpoints(builder, sessions, "Top error endpoints", includeErrors: true);
        AppendContentTypes(builder, sessions);
        AppendFindings(builder, analysis.Findings);

        return builder.ToString();
    }

    private static void AppendAggregate(StringBuilder builder, string title, IReadOnlyList<AggregateMetric> metrics)
    {
        if (metrics.Count == 0)
        {
            return;
        }

        builder.AppendLine(title);
        foreach (var metric in metrics)
        {
            builder.AppendLine($"- {metric.Key}: {metric.Count}");
        }
        builder.AppendLine();
    }

    private static void AppendEndpoints(StringBuilder builder, IReadOnlyList<CapturedSessionRecord> sessions, string title, bool includeErrors)
    {
        var filtered = includeErrors
            ? sessions.Where(session => session.StatusCode >= 400)
            : sessions;

        var groups = filtered
            .GroupBy(session => SanitizeEndpoint(session))
            .Select(group => new
            {
                Endpoint = group.Key,
                Count = group.Count(),
                Statuses = group.GroupBy(item => item.StatusCode)
                    .OrderByDescending(item => item.Count())
                    .Take(3)
                    .Select(item => $"{item.Key}x{item.Count()}")
                    .ToArray()
            })
            .OrderByDescending(group => group.Count)
            .Take(10)
            .ToArray();

        if (groups.Length == 0)
        {
            return;
        }

        builder.AppendLine(title);
        foreach (var group in groups)
        {
            var statusSummary = group.Statuses.Length > 0
                ? $" (status {string.Join(", ", group.Statuses)})"
                : string.Empty;
            builder.AppendLine($"- {group.Endpoint}: {group.Count}{statusSummary}");
        }
        builder.AppendLine();
    }

    private static void AppendContentTypes(StringBuilder builder, IReadOnlyList<CapturedSessionRecord> sessions)
    {
        var groups = sessions
            .Select(session => session.ResponseContentType ?? session.RequestContentType)
            .Where(contentType => !string.IsNullOrWhiteSpace(contentType))
            .Select(contentType => contentType!.Split(';', 2)[0].Trim())
            .GroupBy(contentType => contentType, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { ContentType = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .Take(10)
            .ToArray();

        if (groups.Length == 0)
        {
            return;
        }

        builder.AppendLine("Content types");
        foreach (var group in groups)
        {
            builder.AppendLine($"- {group.ContentType}: {group.Count}");
        }
        builder.AppendLine();
    }

    private static void AppendFindings(StringBuilder builder, IReadOnlyList<string> findings)
    {
        if (findings.Count == 0)
        {
            return;
        }

        builder.AppendLine("Findings");
        foreach (var finding in findings)
        {
            builder.AppendLine($"- {finding}");
        }
        builder.AppendLine();
    }

    private static string SanitizeEndpoint(CapturedSessionRecord session)
    {
        var host = string.IsNullOrWhiteSpace(session.Host) ? "(unknown-host)" : session.Host;
        var path = SanitizePathAndQuery(session.PathAndQuery);
        var method = string.IsNullOrWhiteSpace(session.Method) ? "UNKNOWN" : session.Method;

        return $"{method} {host}{path}";
    }

    private static string SanitizePathAndQuery(string? pathAndQuery)
    {
        if (string.IsNullOrWhiteSpace(pathAndQuery))
        {
            return "/";
        }

        var queryIndex = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0)
        {
            return pathAndQuery;
        }

        var path = pathAndQuery[..queryIndex];
        var query = pathAndQuery[(queryIndex + 1)..];
        if (string.IsNullOrWhiteSpace(query))
        {
            return path;
        }

        var sanitized = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair =>
            {
                var equalsIndex = pair.IndexOf('=', StringComparison.Ordinal);
                if (equalsIndex < 0)
                {
                    return pair;
                }

                var key = pair[..equalsIndex];
                return $"{key}=***";
            });

        return $"{path}?{string.Join("&", sanitized)}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 60)
        {
            return $"{duration.TotalSeconds:F0}s";
        }

        return duration.ToString("mm\\:ss", CultureInfo.InvariantCulture);
    }
}
