---
name: fiddlercore-dotnet-capture
description: Implement or extend .NET FiddlerCore capture services, Semantic Kernel plugins, and HTTP/S traffic analysis workflows. Use when Codex needs to add packet capture, proxy lifecycle management, session caching, request/response summarization, AI-callable KernelFunction tools, or documentation around FiddlerCore, Semantic Kernel, network capture, traffic analysis, zhua bao, or data organization in C# projects.
---

# FiddlerCore Dotnet Capture

## Quick Start

- Inspect the target solution first. Identify the hosting model, existing DI registration, target framework, logging approach, and whether a background hosted service already exists.
- Copy or adapt the sample files under `assets/template-solution/` when the repo does not already have a capture engine or Semantic Kernel plugin.
- Read `references/fiddlercore-engine.md` before touching Fiddler lifecycle, proxy registration, HTTPS decryption, or session materialization.
- Read `references/semantic-kernel-plugin.md` before exposing capture controls or analysis methods to the model.
- Read `references/analysis-reporting.md` when the user asks for traffic summaries, anomaly detection, report generation, data organization, or privacy-safe exports.
- Read `references/implementation-walkthrough.md` when the user wants a single end-to-end sequence for building the engine and plugin from scratch.

## Workflow

1. Confirm package strategy.
   - FiddlerCore is distributed from Telerik's NuGet feed, not the public `nuget.org` feed.
   - Prefer central package management or explicit `PackageReference` entries.
   - Keep package versions floating only in templates; pin exact versions in production repositories.

2. Build the capture engine first.
   - Keep FiddlerCore startup and shutdown inside a dedicated hosted service or lifecycle manager.
   - Store captured sessions in a bounded in-memory cache unless the user explicitly asks for durable storage.
   - Decode compressed responses only when analysis needs readable payloads.
   - Truncate or redact body previews by default.

3. Expose a narrow Semantic Kernel surface.
   - Use small, task-shaped functions such as `start_capture`, `stop_capture`, `get_capture_state`, `get_recent_sessions`, `analyze_traffic`, and `clear_sessions`.
   - Write `Description` attributes for the model, not for developers.
   - Return structured data that the model can summarize again, instead of returning large prose blobs from every function.

4. Generate implementation notes.
   - Document Telerik feed setup, certificate requirements, proxy side effects, and validation steps.
   - Call out any environment blockers such as missing `dotnet`, Telerik credentials, or certificate trust permissions.

## Rules

- Separate responsibilities: capture engine manages lifecycle; store manages retention; plugin manages AI-callable affordances.
- Default to safe capture settings. Do not silently enable remote clients or HTTPS decryption without explicit intent.
- Treat request and response bodies as sensitive. Prefer previews, filters, redaction, and bounded caches.
- Keep plugin descriptions concrete about when each function should be used.
- Favor deterministic summaries: counts by host, method, status code, dominant content types, and notable errors.

## Assets

- `assets/template-solution/Directory.Packages.props`
  - Central package template with floating versions that can be pinned later.
- `assets/template-solution/NuGet.Config.example`
  - Example Telerik feed configuration without hardcoded secrets.
- `assets/template-solution/src/FiddlerCapture.Engine/`
  - Hosted service, options, store, and captured-session models.
- `assets/template-solution/src/FiddlerCapture.SemanticKernel/`
  - Semantic Kernel plugin and traffic analysis DTOs.

## References

- `references/fiddlercore-engine.md`
  - Architecture, NuGet feed setup, lifecycle boundaries, and FiddlerCore-specific pitfalls.
- `references/semantic-kernel-plugin.md`
  - How to design `KernelFunction` and `Description` annotations so the model can choose tools well.
- `references/analysis-reporting.md`
  - How to turn raw sessions into structured summaries and documentation-ready analysis.
- `references/implementation-walkthrough.md`
  - A step-by-step build order for the capture engine, cache, and Semantic Kernel plugin.
