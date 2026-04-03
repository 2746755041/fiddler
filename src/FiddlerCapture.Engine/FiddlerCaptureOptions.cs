namespace FiddlerCapture.Engine;

public sealed class FiddlerCaptureOptions
{
    public bool StartOnApplicationStart { get; set; } = false;

    public int ListenPort { get; set; } = 8877;

    public bool RegisterAsSystemProxy { get; set; } = true;

    public bool AllowRemoteClients { get; set; }

    public bool DecryptHttps { get; set; }

    public bool TrustRootCertificate { get; set; }

    public bool TrustRootCertificateAsAdmin { get; set; }

    public bool EnableHttp2 { get; set; }

    public bool DecodeCompressedResponses { get; set; } = true;

    public bool IncludeBodies { get; set; } = true;

    public int MaxCachedSessions { get; set; } = 500;

    public int MaxBodyPreviewChars { get; set; } = 4096;
}
