using Fiddler;
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
    private bool _isRunning;

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
            var settings = BuildStartupSettings(options);

            FiddlerApplication.BeforeResponse += OnBeforeResponse;
            FiddlerApplication.AfterSessionComplete += OnAfterSessionComplete;
            FiddlerApplication.Startup(settings);

            _isRunning = true;

            _logger.LogInformation(
                "FiddlerCore capture started on port {ListenPort}. SystemProxy={RegisterAsSystemProxy} HTTPS={DecryptHttps}",
                options.ListenPort,
                options.RegisterAsSystemProxy,
                options.DecryptHttps);
        }
        catch
        {
            FiddlerApplication.BeforeResponse -= OnBeforeResponse;
            FiddlerApplication.AfterSessionComplete -= OnAfterSessionComplete;
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

            FiddlerApplication.BeforeResponse -= OnBeforeResponse;
            FiddlerApplication.AfterSessionComplete -= OnAfterSessionComplete;
            FiddlerApplication.Shutdown();

            _isRunning = false;
            _logger.LogInformation("FiddlerCore capture stopped.");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private FiddlerCoreStartupSettings BuildStartupSettings(FiddlerCaptureOptions options)
    {
        var builder = new FiddlerCoreStartupSettingsBuilder()
            .ListenOnPort((ushort)options.ListenPort)
            .MonitorAllConnections();

        if (options.RegisterAsSystemProxy)
        {
            builder.RegisterAsSystemProxy();
        }

        if (options.AllowRemoteClients)
        {
            builder.AllowRemoteClients();
        }

        if (options.DecryptHttps)
        {
            builder.DecryptSSL();
        }

        if (options.EnableHttp2)
        {
            builder.EnableHTTP2();
        }

        return builder.Build();
    }

    private void OnBeforeResponse(Session session)
    {
        var options = _optionsMonitor.CurrentValue;

        if (!options.DecodeCompressedResponses)
        {
            return;
        }

        try
        {
            session.utilDecodeResponse(true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to decode response for session {SessionId}", session.id);
        }
    }

    private void OnAfterSessionComplete(Session session)
    {
        try
        {
            _store.Add(MaterializeSession(session, _optionsMonitor.CurrentValue));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to materialize session {SessionId}", session.id);
        }
    }

    private static CapturedSessionRecord MaterializeSession(Session session, FiddlerCaptureOptions options)
    {
        var requestHeaders = FlattenHeaders(Utilities.GetRequestHeaders(session));
        var responseHeaders = FlattenHeaders(Utilities.GetResponseHeaders(session));
        var requestPreview = options.IncludeBodies ? GetPreview(() => session.GetRequestBodyAsString(), options) : null;
        var responsePreview = options.IncludeBodies ? GetPreview(() => session.GetResponseBodyAsString(), options) : null;
        var requestBodyLength = GetBodyLength(() => session.GetRequestBodyAsBytes());
        var responseBodyLength = GetBodyLength(() => session.GetResponseBodyAsBytes());

        return new CapturedSessionRecord
        {
            SessionId = session.id,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Method = session.RequestMethod,
            Url = session.fullUrl,
            Host = session.hostname,
            Scheme = session.isHTTPS ? "https" : "http",
            PathAndQuery = session.PathAndQuery,
            Port = session.port,
            StatusCode = session.responseCode,
            IsHttps = session.isHTTPS,
            ClientProcessName = string.IsNullOrWhiteSpace(session.LocalProcess) ? null : session.LocalProcess,
            ClientProcessId = session.LocalProcessID == 0 ? null : session.LocalProcessID,
            RequestContentType = TryGetHeader(requestHeaders, "Content-Type"),
            ResponseContentType = TryGetHeader(responseHeaders, "Content-Type"),
            RequestBodyLength = requestBodyLength,
            ResponseBodyLength = responseBodyLength,
            RequestBodyPreview = requestPreview,
            ResponseBodyPreview = responsePreview,
            RequestHeaders = requestHeaders,
            ResponseHeaders = responseHeaders
        };
    }

    private static Dictionary<string, string> FlattenHeaders(IEnumerable<KeyValuePair<string, string>> headers)
    {
        var flattened = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            flattened[header.Key] = header.Value;
        }

        return flattened;
    }

    private static string? TryGetHeader(IReadOnlyDictionary<string, string> headers, string key)
    {
        return headers.TryGetValue(key, out var value) ? value : null;
    }

    private static string? GetPreview(Func<string> previewFactory, FiddlerCaptureOptions options)
    {
        try
        {
            var preview = previewFactory();

            if (string.IsNullOrEmpty(preview))
            {
                return null;
            }

            if (preview.Length <= options.MaxBodyPreviewChars)
            {
                return preview;
            }

            return preview[..options.MaxBodyPreviewChars];
        }
        catch
        {
            return null;
        }
    }

    private static long? GetBodyLength(Func<byte[]> bodyFactory)
    {
        try
        {
            return bodyFactory().LongLength;
        }
        catch
        {
            return null;
        }
    }
}
