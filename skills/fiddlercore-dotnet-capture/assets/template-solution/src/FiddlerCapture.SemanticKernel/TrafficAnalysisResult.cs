namespace FiddlerCapture.SemanticKernel;

public sealed class TrafficAnalysisResult
{
    public int TotalSessions { get; init; }

    public int DistinctHosts { get; init; }

    public int ErrorSessions { get; init; }

    public int RedirectSessions { get; init; }

    public IReadOnlyList<AggregateMetric> Methods { get; init; } = [];

    public IReadOnlyList<AggregateMetric> Hosts { get; init; } = [];

    public IReadOnlyList<AggregateMetric> StatusCodes { get; init; } = [];

    public IReadOnlyList<string> Findings { get; init; } = [];
}

public sealed class AggregateMetric
{
    public string Key { get; init; } = string.Empty;

    public int Count { get; init; }
}
