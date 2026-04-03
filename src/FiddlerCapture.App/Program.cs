<<<<<<< HEAD
// See https://aka.ms/new-console-template for more information
=======
﻿// See https://aka.ms/new-console-template for more information
>>>>>>> 93925c7 (add fiddler capture skill and titanium proxy host)
using FiddlerCapture.Engine;
using FiddlerCapture.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddSimpleConsole(options =>
    {
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    });
});

builder.Services
    .AddOptions<FiddlerCaptureOptions>()
    .Bind(builder.Configuration.GetSection("FiddlerCapture"));

builder.Services.AddFiddlerCaptureEngine();
builder.Services.AddSingleton<NetworkCapturePlugin>();
builder.Services.AddHostedService<CaptureConsoleStatusService>();

using var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);

internal sealed class CaptureConsoleStatusService : IHostedService
{
    private readonly ILogger<CaptureConsoleStatusService> _logger;
    private readonly IFiddlerCaptureEngine _engine;
    private readonly NetworkCapturePlugin _plugin;

    public CaptureConsoleStatusService(
        ILogger<CaptureConsoleStatusService> logger,
        IFiddlerCaptureEngine engine,
        NetworkCapturePlugin plugin)
    {
        _logger = logger;
        _engine = engine;
        _plugin = plugin;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var state = _engine.GetState();

        _logger.LogInformation(
            "App started. Capture running={IsRunning}, port={ListenPort}, cachedSessions={CachedSessions}, decryptHttps={DecryptHttps}",
            state.IsRunning,
            state.ListenPort,
            state.CachedSessions,
            state.DecryptHttps);

        _logger.LogInformation("Native Semantic Kernel plugin registered in DI: {PluginType}", typeof(NetworkCapturePlugin).FullName);
        _logger.LogInformation("Plugin functions: get_capture_state, start_capture, stop_capture, get_recent_sessions, analyze_traffic, clear_sessions");
        _logger.LogInformation("Press Ctrl+C to stop the host.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        var state = _plugin.GetCaptureState();
        _logger.LogInformation("App stopping. Final cached session count: {CachedSessions}", state.CachedSessions);
        return Task.CompletedTask;
    }
}
