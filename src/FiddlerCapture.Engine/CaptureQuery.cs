namespace FiddlerCapture.Engine;

public sealed record CaptureQuery
{
    public string? HostContains { get; init; }

    public string? UrlContains { get; init; }

    public string? Method { get; init; }

    public int? MinStatusCode { get; init; }

    public int? MaxStatusCode { get; init; }

    public int Limit { get; init; } = 50;
}
