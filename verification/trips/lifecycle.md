# Verification: trips/lifecycle

## Claim: unpermitted-transition-rejected
Oracle: model-based

The claim quantifies over every state and every transition. The honest check enumerates the
machine and asserts that exactly the permitted pairs are accepted — a model, not a list of
examples. Scope stays `unit`: the transition relation is a pure function and needs nothing real.

## Claim: replayed-transition-is-inert
Scope: component

Replay tolerance depends on a conditional write against committed state. At unit scope this
verifies that the handler compares a version it was handed, which is not the claim.

## Claim: history-is-append-only
Scope: component

The claim is about what the store permits after the fact. A substituted repository that never
had an update path proves nothing about one that does.
