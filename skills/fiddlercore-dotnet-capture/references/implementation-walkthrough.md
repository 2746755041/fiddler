# Implementation Walkthrough

## Step 1: Build The FiddlerCore Capture Engine

1. Configure package feeds.
   - Add Telerik's NuGet feed.
   - Provide the Telerik NuGet key through a local `NuGet.Config`, environment secret, or CI secret.

2. Add the engine project.
   - Target `net8.0` unless the repo has a different standard.
   - Reference `FiddlerCore`, hosting abstractions, logging abstractions, and options.

3. Add capture options.
   - Define listen port, HTTPS toggle, remote-client toggle, cache size, and preview size.

4. Add a bounded session store.
   - Use a singleton in-memory store first.
   - Trim the oldest sessions when the cache exceeds its cap.

5. Add a hosted capture engine.
   - Subscribe to Fiddler events before startup.
   - Call `FiddlerApplication.Startup(...)`.
   - Decode compressed responses when analysis requires readable text.
   - Materialize each session into a compact record.
   - Unsubscribe and call `FiddlerApplication.Shutdown()` on stop.

6. Register services.
   - Register the store as a singleton.
   - Register the engine as both the hosted service and the `IFiddlerCaptureEngine` interface.

## Step 2: Wrap The Engine As An AI-Callable Plugin

1. Create a separate Semantic Kernel project or folder.
   - Keep plugin logic away from low-level Fiddler startup code.

2. Create a native plugin class.
   - Annotate each callable method with `[KernelFunction("function_name")]`.
   - Annotate the class and each method with `[Description("...")]`.

3. Expose only the high-value functions.
   - `get_capture_state`
   - `start_capture`
   - `stop_capture`
   - `get_recent_sessions`
   - `analyze_traffic`
   - `clear_sessions`

4. Return structured DTOs.
   - `CaptureEngineState` for lifecycle state.
   - `CapturedSessionRecord` for raw evidence.
   - `TrafficAnalysisResult` for aggregated analysis.

5. Register the plugin with Semantic Kernel.
   - Add the plugin to DI.
   - Register it with the kernel builder using the plugin name you want the model to see.

## Step 3: Generate Detailed Documentation

Document these items after implementation:

- Telerik feed and secret setup
- whether HTTPS decryption is fully configured
- proxy side effects and shutdown behavior
- cache limits and body preview limits
- example questions the model can answer with the plugin
- anything not validated locally

## Example User Intents The Skill Should Handle

- "Add FiddlerCore capture to this .NET service."
- "Create a Semantic Kernel plugin so the model can start capture and analyze requests."
- "Summarize the last 100 captured API calls and highlight failures."
- "Generate implementation notes for a FiddlerCore-based network diagnostics service."
