# FiddlerCore Engine

## Goal

Build a reusable .NET capture engine that:

- starts and stops FiddlerCore cleanly,
- owns proxy and certificate side effects,
- listens to capture events,
- materializes sessions into small immutable records,
- stores them in a bounded cache for later AI analysis.

## Package Setup

FiddlerCore is delivered from Telerik's feed. The official documentation shows adding the source with a command in this shape:

```powershell
dotnet nuget add source https://nuget.telerik.com/v3/index.json `
  --name telerik.com `
  --username api-key `
  --password <YOUR_NUGET_KEY> `
  --store-password-in-clear-text
```

Use a local `NuGet.Config` or CI secret instead of hardcoding credentials in project files.

Template guidance:

- Use `FiddlerCore` as a package reference from Telerik's feed.
- Use `Microsoft.Extensions.Hosting.*` and `Microsoft.Extensions.Options` for lifecycle and configuration.
- Use central package management when the solution has multiple projects.

## Recommended Architecture

Keep three layers:

1. Capture engine
   - A hosted service or lifecycle manager that calls `FiddlerApplication.Startup(...)` and `FiddlerApplication.Shutdown()`.
   - Owns event subscription and unsubscription.

2. Session store
   - A bounded in-memory store for recent sessions.
   - Supports filtering, clearing, and snapshot queries.

3. AI plugin
   - Reads engine state and store contents.
   - Does not contain low-level Fiddler startup code.

## Startup Pattern

FiddlerCore's documented flow is:

1. Attach event handlers such as `FiddlerApplication.BeforeResponse` and `FiddlerApplication.AfterSessionComplete`.
2. Build `FiddlerCoreStartupSettings` with `FiddlerCoreStartupSettingsBuilder`.
3. Call `FiddlerApplication.Startup(settings)`.
4. Call `FiddlerApplication.Shutdown()` on stop.

Prefer this pattern inside a singleton hosted service so that startup is idempotent and shutdown is always paired.

## Options To Expose

Keep configuration explicit:

- `ListenPort`
- `RegisterAsSystemProxy`
- `AllowRemoteClients`
- `DecryptHttps`
- `EnableHttp2`
- `DecodeCompressedResponses`
- `IncludeBodies`
- `MaxCachedSessions`
- `MaxBodyPreviewChars`

## HTTPS Decryption

HTTPS capture is not just a boolean flag. The model should remember:

- `DecryptSSL()` only enables decryption mode.
- The environment also needs a certificate provider and root-certificate trust flow.
- The Telerik docs show configuring `CertMaker` and trusting the root certificate before startup.
- If the environment lacks the certificate provider package or permission to trust certificates, document the blocker instead of pretending HTTPS decryption is fully enabled.

## Materializing Sessions

Use `AfterSessionComplete` for final records. Extract only the fields the model actually needs:

- id
- full URL
- host
- method
- status code
- content types
- body length
- bounded body previews
- request and response headers
- client process name and process id when available

Do not keep full raw payloads by default. Large payload retention causes prompt bloat and memory pressure.

## Event-Handling Guidance

- Keep event handlers small and synchronous.
- Avoid long-running I/O in Fiddler callbacks.
- Decode compressed responses only when the response is actually needed for analysis.
- Catch payload-decoding exceptions because `GetRequestBodyAsString()` and `GetResponseBodyAsString()` may throw on malformed content.

## Safety Notes

- `AllowRemoteClients` has security implications. Enable it only when the user explicitly wants mobile or cross-device capture.
- `RegisterAsSystemProxy` changes operating-system proxy behavior. Call this out in docs and shutdown cleanly.
- Bounded caches matter. A long-running proxy can grow indefinitely otherwise.

## Validation Checklist

- Verify the hosted service starts once.
- Verify `FiddlerApplication.Shutdown()` is called on graceful stop.
- Verify session cache limits are enforced.
- Verify compressed response decoding is optional.
- Verify HTTPS decryption path is documented if not fully testable in the current environment.
