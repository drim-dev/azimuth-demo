# Plan: poison-capture-isolation

- [x] Add the accepted intent delta.
- [x] Establish deterministic pending order for the starvation case.
- [x] Quarantine invalid signed quotes atomically with their terminal failure.
- [x] Continue after terminal and transient per-intent failures while preserving cancellation.
- [x] Add component evidence through the real database and dispatch endpoint.
- [x] Run the agent tier, measure fingerprint invalidation and archive the change.
