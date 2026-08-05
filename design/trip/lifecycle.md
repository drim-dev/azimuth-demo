# Design: trip/lifecycle

## Requirement: transitions-follow-state-machine
Enforcement: choke-point
Site: `TripStateMachine.Transition` — the only function that writes `trips.state`

A total function from (state, event) to an outcome that is either a new state or a rejection.
Encoding the machine in the type system so that illegal pairs are unrepresentable was considered
and rejected: it is expressible in C# but not in TypeScript or the mobile client, and a rule that
holds in one language of three gives false confidence at the boundaries where trips actually
move.

The consequence is that the machine is enforced at one place per service rather than by the
compiler, so `unpermitted-transition-rejected` carries a model-based oracle rather than being
vacuous.

## Requirement: terminal-states-are-final
Enforcement: choke-point
Site: `TripStateMachine.Transition` rejects every transition out of a terminal state
Enforcement: constraint
Site: `trips` conditional update predicated on the current state, so a stale writer cannot
overwrite a terminal one

Two mechanisms for one rule, because the choke point alone does not survive concurrency: two
in-flight handlers can both read `in-progress`, both pass the machine, and the later write wins.
The conditional update is what makes `replayed-transition-is-inert` true rather than merely
likely.

## Residue

**Cancellation is a transition, not a deletion.** A cancelled trip keeps its history, its
assignment, and any fee. Every query that means "real trips" must exclude cancelled explicitly,
and forgetting to is the most common bug in reporting against this table.

**The machine is duplicated across services rather than shared.** The trip service owns the
authoritative machine; the BFFs carry a reduced copy to decide what to show. They are expected to
drift, and the BFF copy is deliberately permissive — it never blocks an action the service would
allow. If it is ever made strict, it becomes a second place where a legal transition can be
refused, with no claim covering it.
