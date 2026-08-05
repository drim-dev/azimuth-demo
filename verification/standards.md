# Verification standards
Default scope: unit

The project-level mapping from criticality to required evidence, written once (D6.1). Applies to
every claim unless a plan entry deviates from it.

## Level: critical
Strength: demonstration
Quantification: invariant
Residual: required

## Level: standard
Strength: demonstration
Quantification: example
Residual: optional

## Level: routine
Strength: none
Residual: optional

## Ladders apply

Proof satisfies a demonstration requirement, and `invariant` satisfies an `example` requirement. A
required form is a floor, not a target.

`Strength: none` on `routine` is D6.5: the level requires a spec entry and nothing else, so no
evidence was ever demanded. A routine claim can still be `unrealized` — criticality gates evidence,
not implementation.

## Scope is not derived from criticality

`Default scope: unit` applies to every claim. It is raised per claim, in a plan, where the claim's
truth depends on something real.

The alternative — critical implies `component` — was rejected. It is the C5 mistake from the
concern catalog: an authorization rule is critical and honestly unit-checkable, and forcing it to
component scope buys nothing while making the requirement look arbitrary. Conversely a `standard`
claim about concurrent writes is vacuous at unit scope.

What determines scope is what the claim *is about*, not how much it matters. Raise to `component`
when truth depends on real persistence, real serialization, a storage constraint, or concurrency;
raise to `e2e` when it depends on composition across process boundaries — when the claim can hold
at every site and fail between them.

The consequence is that a plan entry raising scope marks something specific and reviewable: **a
claim whose truth cannot be established in isolation.** That is a judgment worth a human, and there
should not be many of them.

## Why defaults are legitimate here but not for criticality

D6.2 forbids a default criticality, on the grounds that if the level may be absent, absence becomes
the default and nobody thinks. That argument does not transfer, because the judgment is still made
— once, here, deliberately — rather than skipped.

The distinction: criticality is a per-claim judgment about consequence, which nothing else can
supply. Required evidence is a policy that follows from criticality, and a policy stated once and
applied uniformly is more reviewable than the same policy retyped 52 times.

What may never default is **the residual on a critical claim**. If a critical claim's evidence
falls short of this standard, the gap is recorded and accepted explicitly or it is a hole.

## Freshness

Test evidence is re-established every CI run and needs no declaration. Every non-test evidence item
declares its own cadence and how it dies silently (D4.2).

## Status

No detection-strength evidence exists yet. The concerns that require it — C3, C8, C18 — are outside
the steel thread. When the first monitor appears, D4.3's detector-test requirement applies to it
immediately, and the parser enforces it.
