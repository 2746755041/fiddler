# Semantic Kernel Plugin

## Goal

Wrap the capture engine in a native Semantic Kernel plugin so the model can choose when to:

- start capture,
- stop capture,
- inspect state,
- fetch recent sessions,
- summarize traffic,
- clear cached sessions.

## Plugin Design Principles

- Use a normal C# class.
- Use `[KernelFunction]` on each callable method.
- Use `[Description]` on the class, methods, and important parameters.
- Keep function names action-oriented and narrow.
- Return structured objects or short records rather than oversized prose.

## What The Model Actually Reads

The `Description` attributes are part of the tool-selection surface. Write them from the model's point of view:

- bad: "Calls service layer method for capture status"
- good: "Inspect whether live traffic capture is running and how many sessions are cached before deciding whether to start or stop capture"

## Recommended Function Surface

- `get_capture_state`
  - Use before any live-capture action or when reporting current readiness.
- `start_capture`
  - Use when the user asks to begin live traffic capture and it is not already running.
- `stop_capture`
  - Use when capture should stop or when proxy side effects need to be reverted.
- `get_recent_sessions`
  - Use when the user wants concrete request rows or filtered endpoint evidence.
- `analyze_traffic`
  - Use when the user wants trends, anomalies, counts, error hotspots, or report-ready summaries.
- `clear_sessions`
  - Use before a fresh experiment or after a report is complete.

## Parameter Guidance

Prefer a few high-value filters:

- `limit`
- `hostContains`
- `urlContains`
- `method`
- `minStatusCode`
- `maxStatusCode`

Keep defaults conservative so tool calls stay small.

## Return Shapes

Prefer stable DTOs:

- `CaptureEngineState`
- `CapturedSessionRecord`
- `TrafficAnalysisResult`
- `AggregateMetric`

This lets the model do a second-pass explanation without reparsing free-form text.

## Registration Notes

In current Semantic Kernel C# usage, a common approach is:

```csharp
builder.Services.AddSingleton<NetworkCapturePlugin>();
builder.Services.AddKernel();
builder.Plugins.AddFromType<NetworkCapturePlugin>("network_capture");
```

Adjust the exact registration shape to match the repo's existing Semantic Kernel version and host builder pattern.

## Description Writing Checklist

- Say when to call the function.
- Mention the user intent it serves.
- Mention decision boundaries when relevant.
- Avoid internal jargon unless the model needs it.
- Keep wording concrete and short enough to scan quickly.
