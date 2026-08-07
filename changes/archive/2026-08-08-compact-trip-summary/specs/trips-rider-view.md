# Intent delta: trips/rider-view

## Add requirement: compact-trip-summary
Criticality: routine

A rider MAY open a compact trip summary containing the trip identity, current state and quoted
fare without driver detail.

### Add scenario: summary-shows-state-and-fare
GIVEN a trip exists
WHEN the rider opens its compact summary
THEN the trip identity, current state and quoted fare are shown
AND no driver detail is shown
