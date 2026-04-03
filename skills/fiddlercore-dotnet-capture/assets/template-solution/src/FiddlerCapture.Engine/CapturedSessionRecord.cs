using System.Collections.Generic;

namespace FiddlerCapture.Engine;

public sealed record CapturedSessionRecord
{
    public int SessionId { get; init; }

    public DateTimeOffset CapturedAtUtc { get; init; }

    public string Method { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string Host { get; init; } = string.Empty;

    public string Scheme { get; init; } = string.Empty;

    public string PathAndQuery { get; init; } = string.Empty;

    public int Port { get; init; }

    public int StatusCode { get; init; }

    public bool IsHttps { get; init; }

    public string? ClientProcessName { get; init; }

    public int? ClientProcessId { get; init; }

    public string? RequestContentType { get; init; }

    public string? ResponseContentType { get; init; }

    public long? RequestBodyLength { get; init; }

    public long? ResponseBodyLength { get; init; }

    public string? RequestBodyPreview { get; init; }

    public string? ResponseBodyPreview { get; init; }

    public IReadOnlyDictionary<string, string> RequestHeaders { get; init; } =
        new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> ResponseHeaders { get; init; } =
        new Dictionary<string, string>();
}
