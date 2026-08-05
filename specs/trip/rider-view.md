# Spec: trip/rider-view

What a rider is shown about their trip and its driver, at each stage.

This spec exists to exercise the fan-out: every claim here is realized in the rider client, the
rider BFF and the trip service, and some of them in the driver client too. One claim, several
sites, three languages.

Owns rider-facing visibility. Does not own what the driver sees (`trip/driver-view`, later) or
the underlying states (`trip/lifecycle`).

> The behavioural claims below constrain the surfaces that existed when they were written. The
> general rule — that *no* rider-reachable surface may carry a driver's precise position outside
> the live phases — is stated as an invariant, because slice 2 demonstrated that the per-scenario
> claims cannot carry it: a receipt endpoint satisfied every one of them and leaked anyway, and the
> matrix reported no new hole. Concern C1 in `docs/concern-catalog.md`.

## Invariant: position-confined-to-live-phases
Criticality: critical
Over: trip/rider-view

No site that carries trip information to a rider SHALL expose a driver's precise position outside
the assigned and in-progress phases.

This claim ranges over a *set of sites*, not over executions, and its class is every site realizing
a claim in this spec. Membership is derived from what the code built: a new rider-facing site joins
the class by being written, without anyone remembering to add it. A member discharges the invariant
by realizing it — which is a statement that the site's author considered the rule and routed the
position through the projection, or does not touch a position at all.

## Requirement: driver-hidden-before-assignment
Criticality: critical

Before a trip is assigned, the rider SHALL NOT be shown any identity or position of any
individual driver.

### Scenario: no-driver-identity-before-assignment
GIVEN a trip that is not yet assigned
WHEN the rider views the trip
THEN no driver identity is shown

### Scenario: no-driver-position-before-assignment
GIVEN a trip that is not yet assigned
WHEN the rider views the trip
THEN no position of any individual driver is shown

### Scenario: supply-density-shown-before-assignment
GIVEN a trip that is not yet assigned
WHEN the rider views the trip
THEN an indication of nearby supply may be shown that identifies no individual driver

## Requirement: assigned-driver-visible
Criticality: standard

Once a trip is assigned and until it reaches a terminal state, the rider SHALL be shown the
assigned driver's display identity and current position.

### Scenario: driver-shown-after-assignment
GIVEN a trip that has just been assigned
WHEN the rider views the trip
THEN the assigned driver's display name and vehicle are shown
AND the driver's current position is shown

### Scenario: driver-position-follows-driver
GIVEN a trip in the assigned or in-progress state
WHEN the assigned driver's position changes
THEN the position shown to the rider follows it

## Requirement: driver-hidden-after-terminal
Criticality: critical

Once a trip reaches a terminal state, the rider SHALL NOT be shown the driver's position again.

### Scenario: no-position-after-completion
GIVEN a trip in the completed state
WHEN the rider views the trip
THEN no driver position is shown

### Scenario: no-position-after-cancellation
GIVEN a trip in the cancelled state
WHEN the rider views the trip
THEN no driver position is shown

### Scenario: driver-identity-remains-on-receipt
GIVEN a trip in the completed state
WHEN the rider views the trip
THEN the driver's display name remains visible for the receipt
