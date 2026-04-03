using System.Net;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FiddlerCapture.Engine;

public sealed class FiddlerCaptureEngine : IHostedService, IFiddlerCaptureEngine, IDisposable
{
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly ILogger<FiddlerCaptureEngine> _logger;
    private readonly IOptionsMonitor<FiddlerCaptureOptions> _optionsMonitor;
    private readonly ICapturedSessionStore _store;
    private ProxyServer? _proxyServer;
    private ExplicitProxyEndPoint? _explicitEndPoint;
    private bool _isRunning;
    private int _sessionSequence;

    public FiddlerCaptureEngine(
        ILogger<FiddlerCaptureEngine> logger,
        IOptionsMonitor<FiddlerCaptureOptions> optionsMonitor,
        ICapturedSessionStore store)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _store = store;
    }

    public CaptureEngineState GetState()
    {
        var options = _optionsMonitor.CurrentValue;

        return new CaptureEngineState(
            _isRunning,
            options.ListenPort,
            _store.Count,
            options.RegisterAsSystemProxy,
            options.DecryptHttps);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_optionsMonitor.CurrentValue.StartOnApplicationStart)
        {
            _logger.LogInformation("Capture engine is registered but not started automatically.");
            return Task.CompletedTask;
        }

        return EnsureStartedAsync(cancellationToken);
    }

    public async ValueTask StartCaptureAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return EnsureStoppedAsync(cancellationToken);
    }

    public async ValueTask StopCaptureAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStoppedAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _proxyServer?.Dispose();
        _lifecycleGate.Dispose();
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_isRunning)
            {
                return;
            }

            var options = _optionsMonitor.CurrentValue;
            var proxyServer = CreateProxyServer(options);
            var endPointAddress = options.AllowRemoteClients ? IPAddress.Any : IPAddress.Loopback;
            var explicitEndPoint = new ExplicitProxyEndPoint(endPointAddress, options.ListenPort, options.DecryptHttps);

            proxyServer.BeforeRequest += OnBeforeRequestAsync;
            proxyServer.BeforeResponse += OnBeforeResponseAsync;
            proxyServer.AddEndPoint(explicitEndPoint);
            proxyServer.Start(changeSystemProxySettings: false);

            if (options.RegisterAsSystemProxy)
            {
                proxyServer.SetAsSystemHttpProxy(explicitEndPoint);
                proxyServer.SetAsSystemHttpsProxy(explicitEndPoint);
            }

            if (options.EnableHttp2)
            {
                _logger.LogInformation("Titanium.Web.Proxy does not expose HTTP/2 toggles in this sample. Traffic may downgrade to HTTP/1.1.");
            }

            _proxyServer = proxyServer;
            _explicitEndPoint = explicitEndPoint;
            _isRunning = true;

            _logger.LogInformation(
                "Titanium.Web.Proxy capture started on {Address}:{ListenPort}. SystemProxy={RegisterAsSystemProxy} HTTPS={DecryptHttps}",
                endPointAddress,
                options.ListenPort,
                options.RegisterAsSystemProxy,
                options.DecryptHttps);
        }
        catch
        {
            TearDownProxy();
            throw;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task EnsureStoppedAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!_isRunning)
            {
                return;
            }

            TearDownProxy();
            _isRunning = false;
            _logger.LogInformation("Capture engine stopped.");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private ProxyServer CreateProxyServer(FiddlerCaptureOptions options)
    {
        var proxyServer = new ProxyServer();

        if (options.DecryptHttps)
        {
            proxyServer.CertificateManager.CreateRootCertificate(false);

            if (options.TrustRootCertificate)
            {
                proxyServer.CertificateManager.TrustRootCertificate(options.TrustRootCertificateAsAdmin);
            }
        }

        return proxyServer;
    }

    private void TearDownProxy()
    {
        if (_proxyServer is null)
        {
            return;
        }

        try
        {
            _proxyServer.BeforeRequest -= OnBeforeRequestAsync;
            _proxyServer.BeforeResponse -= OnBeforeResponseAsync;

            if (_optionsMonitor.CurrentValue.RegisterAsSystemProxy)
            {
                _proxyServer.RestoreOriginalProxySettings();
            }

            _proxyServer.Stop();
            _proxyServer.Dispose();
        }
        finally
        {
            _proxyServer = null;
            _explicitEndPoint = null;
        }
    }

    private async Task OnBeforeRequestAsync(object sender, SessionEventArgs e)
    {
        var options = _optionsMonitor.CurrentValue;
        var uri = e.HttpClient.Request.RequestUri;
        var requestHeaders = FlattenHeaders(e.HttpClient.Request.Headers);
        var (requestBodyLength, requestBodyPreview) = await ReadRequestBodyAsync(e, options).ConfigureAwait(false);

        e.UserData = new PendingSessionData
        {
            Method = e.HttpClient.Request.Method,
            Url = uri.AbsoluteUri,
            Host = uri.Host,
            Scheme = uri.Scheme,
            PathAndQuery = uri.PathAndQuery,
            Port = uri.Port,
            IsHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase),
            RequestHeaders = requestHeaders,
            RequestContentType = TryGetHeader(requestHeaders, "Content-Type"),
            RequestBodyLength = requestBodyLength,
            RequestBodyPreview = requestBodyPreview
        };
    }

    private async Task OnBeforeResponseAsync(object sender, SessionEventArgs e)
    {
        var options = _optionsMonitor.CurrentValue;
        var responseHeaders = FlattenHeaders(e.HttpClient.Response.Headers);
        var (responseBodyLength, responseBodyPreview) = await ReadResponseBodyAsync(e, options).ConfigureAwait(false);
        var pending = e.UserData as PendingSessionData ?? PendingSessionData.FromRequest(e);

        var capturedSession = new CapturedSessionRecord
        {
            SessionId = Interlocked.Increment(ref _sessionSequence),
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Method = pending.Method,
            Url = pending.Url,
            Host = pending.Host,
            Scheme = pending.Scheme,
            PathAndQuery = pending.PathAndQuery,
            Port = pending.Port,
            StatusCode = (int)e.HttpClient.Response.StatusCode,
            IsHttps = pending.IsHttps,
            ClientProcessName = null,
            ClientProcessId = null,
            RequestContentType = pending.RequestContentType,
            ResponseContentType = TryGetHeader(responseHeaders, "Content-Type"),
            RequestBodyLength = pending.RequestBodyLength,
            ResponseBodyLength = responseBodyLength,
            RequestBodyPreview = pending.RequestBodyPreview,
            ResponseBodyPreview = responseBodyPreview,
            RequestHeaders = pending.RequestHeaders,
            ResponseHeaders = responseHeaders
        };

        _store.Add(capturedSession);
        _logger.LogInformation(
            "Captured session {SessionId}: {Method} {Url} -> {StatusCode}",
            capturedSession.SessionId,
            capturedSession.Method,
            capturedSession.Url,
            capturedSession.StatusCode);
    }

    private static async Task<(long? Length, string? Preview)> ReadRequestBodyAsync(SessionEventArgs e, FiddlerCaptureOptions options)
    {
        if (!options.IncludeBodies || !CanHaveRequestBody(e.HttpClient.Request.Method))
        {
            return (null, null);
        }

        try
        {
            var bodyBytes = await e.GetRequestBody().ConfigureAwait(false);
            var preview = await e.GetRequestBodyAsString().ConfigureAwait(false);
            return (bodyBytes?.LongLength, Truncate(preview, options.MaxBodyPreviewChars));
        }
        catch
        {
            return (null, null);
        }
    }

    private static async Task<(long? Length, string? Preview)> ReadResponseBodyAsync(SessionEventArgs e, FiddlerCaptureOptions options)
    {
        if (!options.IncludeBodies)
        {
            return (null, null);
        }

        try
        {
            var bodyBytes = await e.GetResponseBody().ConfigureAwait(false);
            var preview = options.DecodeCompressedResponses
                ? await e.GetResponseBodyAsString().ConfigureAwait(false)
                : null;

            return (bodyBytes?.LongLength, Truncate(preview, options.MaxBodyPreviewChars));
        }
        catch
        {
            return (null, null);
        }
    }

    private static bool CanHaveRequestBody(string method)
    {
        return method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            || method.Equals("PUT", StringComparison.OrdinalIgnoreCase)
            || method.Equals("PATCH", StringComparison.OrdinalIgnoreCase)
            || method.Equals("DELETE", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> FlattenHeaders(System.Collections.IEnumerable headers)
    {
        var flattened = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            var headerType = header.GetType();
            var name = headerType.GetProperty("Name")?.GetValue(header)?.ToString();
            var value = headerType.GetProperty("Value")?.GetValue(header)?.ToString();

            if (!string.IsNullOrWhiteSpace(name))
            {
                flattened[name] = value ?? string.Empty;
            }
        }

        return flattened;
    }

    private static string? TryGetHeader(IReadOnlyDictionary<string, string> headers, string key)
    {
        return headers.TryGetValue(key, out var value) ? value : null;
    }

    private static string? Truncate(string? value, int maxChars)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.Length <= maxChars)
        {
            return value;
        }

        return value[..maxChars];
    }

    private sealed class PendingSessionData
    {
        public required string Method { get; init; }

        public required string Url { get; init; }

        public required string Host { get; init; }

        public required string Scheme { get; init; }

        public required string PathAndQuery { get; init; }

        public required int Port { get; init; }

        public required bool IsHttps { get; init; }

        public required IReadOnlyDictionary<string, string> RequestHeaders { get; init; }

        public string? RequestContentType { get; init; }

        public long? RequestBodyLength { get; init; }

        public string? RequestBodyPreview { get; init; }

        public static PendingSessionData FromRequest(SessionEventArgs e)
        {
            var uri = e.HttpClient.Request.RequestUri;
            var headers = FlattenHeaders(e.HttpClient.Request.Headers);

            return new PendingSessionData
            {
                Method = e.HttpClient.Request.Method,
                Url = uri.AbsoluteUri,
                Host = uri.Host,
                Scheme = uri.Scheme,
                PathAndQuery = uri.PathAndQuery,
                Port = uri.Port,
                IsHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase),
                RequestHeaders = headers,
                RequestContentType = TryGetHeader(headers, "Content-Type"),
                RequestBodyLength = null,
                RequestBodyPreview = null
            };
        }
    }
}
