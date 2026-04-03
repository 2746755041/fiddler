namespace FiddlerCapture.Engine;

public sealed record CaptureEngineState(
    bool IsRunning,
    int ListenPort,
    int CachedSessions,
    bool RegisterAsSystemProxy,
    bool DecryptHttps);
