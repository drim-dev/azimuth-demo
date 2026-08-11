# Spec: trips/lifecycle

The trip state machine, from assignment through to a terminal state.

Owns which transitions are permitted and what each one means. Does not own how a driver comes to
be assigned (`trips/dispatch`) or what happens to money afterwards (`payments/capture`).

States: `requested` → `assigned` → `in-progress` → `completed`, with `cancelled` reachable from
`requested`, `assigned` and `in-progress`. `completed` and `cancelled` are terminal.

## Requirement: transitions-follow-state-machine
Criticality: critical

A trip SHALL move only along the permitted transitions of its state machine.

### Scenario: assigned-to-in-progress
GIVEN a trip in the assigned state
WHEN the assigned driver starts the trip
THEN the trip moves to in-progress

### Scenario: in-progress-to-completed
GIVEN a trip in the in-progress state
WHEN the assigned driver completes the trip
THEN the trip moves to completed

### Scenario: unpermitted-transition-rejected
GIVEN a trip in any state
WHEN a transition not permitted from that state is attempted
THEN the transition is rejected
AND the trip's state is unchanged

## Requirement: terminal-states-are-final
Criticality: critical

A trip in a terminal state SHALL NOT leave it, by any path.

### Scenario: no-transition-out-of-terminal
GIVEN a trip in a terminal state
WHEN any transition is attempted
THEN the transition is rejected
AND the trip's state is unchanged

### Scenario: replayed-transition-is-inert
GIVEN a trip that has reached a terminal state
WHEN a transition event that preceded that state is delivered again
THEN the trip's state is unchanged

## Requirement: transitions-are-attributed
Criticality: standard

Every state transition SHALL record who caused it and when.

### Scenario: transition-records-actor-and-instant
WHEN a trip transitions between states
THEN the transition records the actor that caused it
AND the instant at which it occurred

### Scenario: history-is-append-only
GIVEN a trip with a recorded transition history
WHEN any further transition occurs
THEN the earlier history is unchanged
AND the new transition is appended

## Requirement: cancellable-before-completion
Criticality: standard

A trip SHALL be cancellable by either party before completion, and the cancelling party SHALL be
recorded.

### Scenario: rider-cancels-before-start
GIVEN a trip in the requested or assigned state
WHEN the rider cancels the trip
THEN the trip moves to cancelled
AND the cancelling party is recorded as the rider

### Scenario: driver-cancels-after-assignment
GIVEN a trip in the assigned state
WHEN the assigned driver cancels the trip
THEN the trip moves to cancelled
AND the cancelling party is recorded as the driver

### Scenario: cancellation-after-completion-rejected
GIVEN a trip in the completed state
WHEN either party attempts to cancel the trip
THEN the cancellation is rejected
AND the trip remains completed
