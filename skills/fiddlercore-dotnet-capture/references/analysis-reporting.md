# Analysis Reporting

## Objective

Turn raw captured sessions into structured evidence that can be summarized by the model without dumping every request into the prompt.

## Minimum Summary

For a useful first-pass report, compute:

- total session count,
- distinct host count,
- counts by method,
- counts by status code,
- counts by host,
- top error endpoints,
- content-type distribution when available,
- short findings list.

## Useful Findings Heuristics

Generate concise findings such as:

- a single host dominates traffic,
- 4xx responses cluster around one endpoint,
- 5xx responses indicate backend instability,
- redirects are unexpectedly high,
- JSON APIs are mixed with HTML responses where an API contract was expected,
- body previews suggest authentication failures or validation errors.

## Privacy And Size Controls

- Prefer previews over full bodies.
- Truncate aggressively.
- Redact obvious secrets before storing or returning previews.
- Avoid returning cookies, authorization tokens, or full PII payloads unless the user explicitly asks and the environment allows it.

## Report Structure

When asked to write documentation or a traffic report, use sections like:

1. Capture scope
2. Environment and proxy setup
3. Summary metrics
4. Notable endpoints
5. Error patterns
6. Recommendations
7. Risks or blockers

## Documentation Expectations

The implementation notes should explicitly mention:

- how Telerik feed credentials are provided,
- whether HTTPS decryption is enabled,
- whether remote-client capture is enabled,
- cache limits and retention policy,
- what could not be validated locally.
