# Change: ride-hailing-delivery-alerts

Status: accepted and complete

Exploration: canonical-alpha-release
Carries decisions: CAR16, CAR17

## Problem

`generic-assurance-observations` currently owns two different transitions: a provider-neutral
framework protocol and the ride-hailing fixture's delivery-alert intent, Prometheus linkage and
claim-specific evidence. Transferring that change unchanged would make the canonical framework
repository authoritative for domain-owned claims. Leaving the whole change here would instead
strand the generic protocol outside its intended authority.

The two alert scenarios are already applied to the current model and their rules, rule tests and
judgments already exist. The defect is the active change boundary, not product behavior. CAR16 and
CAR17 require that boundary to be corrected before repository authority moves.

## Outcome

This demo-local change owns the applied intent transition for the delivery backlog and dead-letter
alerts, together with their claim-specific verification account and Prometheus validation history.
`generic-assurance-observations` retains only the generic observation protocol, adapters,
synthetic fixtures and framework conclusions. It may cite this change as immutable field
provenance but does not own its claims or application evidence.

The accepted model, application sources, monitoring rules, rule tests, extracted manifests and
judgments are unchanged by the split.

## Scope

In scope:

- move the applied `analytics/trip-activity` intent delta and its claim-specific verification from
  `generic-assurance-observations` into this change;
- record the existing Prometheus realization, rule-test evidence and judgment results under the
  demo-local transition;
- revise the generic proposal, plan and outcome explicitly so their target is provider-neutral;
- retain an immutable provenance citation from the generic change to this domain validation; and
- record the generic change's resulting framework-only finalization dependency rather than
  inventing another product delta.

Out of scope:

- changing accepted intent, criticality, current verification requirements or judgments;
- editing Prometheus expressions, rule-test series, application code or extractor output;
- changing the generic observation schema, core, adapters or synthetic fixtures;
- accepting, archiving or transferring either active change; and
- moving the real-domain federation laboratory into the canonical repository.

## Affected claims

- `analytics/trip-activity#relay-backlog-raises-alert` remains standard.
- `analytics/trip-activity#dead-letter-presence-raises-alert` remains standard.

Both scenarios are already applied. This change alters their transition authority, not their
predicate or assurance obligation.

## Completion conditions

- This change contains the only active intent delta that adds the two alert scenarios.
- Claim-specific verification for the two scenarios is owned here; generic protocol validation
  remains under `generic-assurance-observations`.
- The generic change is marked as revised and contains no affected ride-hailing claim or
  application-evidence ownership. Its immutable provenance citation remains explicit.
- A before-and-after comparison shows no change to accepted model content, extracted source
  relations, Prometheus configuration, rule tests or judgments.
- The new change projects both scenarios as applied at standard criticality and passes
  `azimuth change check`.
- The generic change's zero-delta finalization limitation is recorded as a downstream dependency
  for canonical acceptance, not concealed by a duplicate or unrelated intent delta.
- The repository's focused alert evidence and complete check pass after the account split.
