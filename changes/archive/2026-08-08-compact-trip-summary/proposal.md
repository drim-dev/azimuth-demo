# Change: compact-trip-summary

Status: accepted and complete

## Problem

The rider has no compact view containing only trip identity, state and quoted fare. The full live
view includes assignment detail, while the receipt is meaningful only after completion.

## Scope

Add a rider page at `/trips/<id>/summary` using the existing rider projection. The page exposes
trip identity, state and fare, and no driver identity or position.

Affected intent: add routine requirement `compact-trip-summary` and scenario
`summary-shows-state-and-fare` to `trips/rider-view`. No existing claim changes criticality.

The new route also joins the existing critical `position-confined-to-live-phases` site domain. Its
membership must come from the built Next route manifest rather than from a linkage tag.

## Completion

- the summary route renders identity, state and fare;
- the routine claim acquires no Azimuth linkage or assurance artifacts;
- the route enumerator reports the untagged surface before it is discharged;
- a missing or incomplete route manifest fails closed;
- the completed change is the second manual lifecycle observation.
