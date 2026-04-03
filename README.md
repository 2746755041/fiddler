# FiddlerCapture

`FiddlerCapture` is a .NET 8 sample solution that combines:

- a Titanium.Web.Proxy-backed packet capture engine,
- a bounded in-memory session cache,
- a Semantic Kernel native plugin for traffic analysis.

## Projects

- `src/FiddlerCapture.Engine`
  - Titanium.Web.Proxy lifecycle, session materialization, and retention.
- `src/FiddlerCapture.SemanticKernel`
  - `[KernelFunction]` plugin surface for AI-driven capture and analysis.
- `src/FiddlerCapture.App`
  - Runnable host that binds configuration, registers services, and keeps the engine available.

## Prerequisites

1. Install .NET 8 SDK.
2. Use a shell that can trust a generated root certificate if you want HTTPS decryption.

## Restore And Run

The project restores from `nuget.org` only.

```powershell
$env:DOTNET_CLI_HOME = "$PWD\\.dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
& "C:\Program Files\dotnet\dotnet.exe" restore .\FiddlerCapture.sln --configfile .\NuGet.Config
& "C:\Program Files\dotnet\dotnet.exe" run --project .\src\FiddlerCapture.App\FiddlerCapture.App.csproj --no-restore
```

## Configuration

Settings live in `src/FiddlerCapture.App/appsettings.json`.

- `StartOnApplicationStart`
  - `false` keeps the engine idle until a plugin call starts capture.
- `RegisterAsSystemProxy`
  - `true` lets the host attach itself as the system proxy.
- `DecryptHttps`
  - `true` enables HTTPS interception through Titanium.Web.Proxy.
- `TrustRootCertificate`
  - `true` asks the proxy library to trust its generated root certificate for the current user.
- `EnableHttp2`
  - Left for compatibility with the original design, but the current sample logs and ignores this toggle.

## Semantic Kernel Plugin Surface

The plugin class is `NetworkCapturePlugin` and exposes:

- `get_capture_state`
- `start_capture`
- `stop_capture`
- `get_recent_sessions`
- `analyze_traffic`
- `clear_sessions`

## Current Validation Status

- Skill validation passed with `quick_validate.py`.
- Project structure and XML files were validated locally.
- The project no longer depends on Telerik licensing or private package feeds.
