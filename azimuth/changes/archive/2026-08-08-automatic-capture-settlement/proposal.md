# Change: automatic-capture-settlement

Status: accepted and complete

## Problem

The critical capture evidence invoked `POST /dispatch` itself. The running Payments process had no
worker or scheduler, so completion did not result in capture unless an external actor supplied the
missing mechanism. The rider receipt also exposed no payment state.

## Scope

Automatically drain the transactional capture outbox, expose settlement metrics and Prometheus
rules, and add a rider-visible payment state to completed-trip receipts. Add standard requirement
`rider-sees-payment-status` with scenario `receipt-explains-payment-state`.

External notification delivery and a human execution of the manual charter remain explicit
residuals; neither will be inferred from a metric test.

## Completion

- a completed trip is captured without a direct dispatch call;
- pending, captured and declined states have distinct service projections;
- the receipt names its payment state without color-only communication;
- fresh and overdue intents produce different metrics;
- Prometheus alert rules pass their rule tests;
- the agent tier re-judges every stale payment claim.
