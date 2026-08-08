# Change: poison-capture-isolation

Status: accepted and complete

## Problem

One invalid signed quote throws out of the dispatcher loop. The worker survives, but every cycle
reaches the same poison intent and aborts before attempting valid intents behind it.

## Scope

Quarantine deterministic invalid-quote intents, continue independent work, and add standard
requirement `capture-batch-isolates-invalid-intents` with scenario
`malformed-intent-does-not-starve-batch`.

## Completion

- terminal malformed intents record a reason and leave the pending set;
- valid intents later in the same batch are attempted;
- cancellation still stops the batch immediately;
- unexpected/transient failures remain pending and do not starve sibling intents.
