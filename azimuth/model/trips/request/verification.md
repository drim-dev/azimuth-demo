# Verification: trips/request

## Claim: quote-consumed-once
Scope: component
Quantification: universal

Single consumption is a uniqueness claim over concurrent requests, settled by storage. The
sequential version of this test passes against an implementation with no constraint.

## Claim: second-request-rejected-while-active
Scope: component
Quantification: universal

"A rider holds at most one active trip" is the same shape as `captured-once`: two requests arriving
together are the case that matters, and they are only distinguishable against a real store.

## Claim: request-admitted-after-terminal
Scope: component

*(added 2026-08-07; the claim was carried at component scope by the harness before anyone recorded
why)*

The rule is `ux_trip_rider_active`, a partial unique index whose filter enumerates the terminal
states. At unit scope there is no index to break: the mutation that removes `'completed'` from the
filter is a migration edit, invisible to anything that is not a real store. Truth here depends on
real persistence, which is what `standards.md` says raises scope.

Worth recording because of how it was found. The mutation was run to test the *evidence*, and what
it also showed is the *required scope* — a fault that only a real store can catch is evidence that
the claim's truth depends on one. Scope is a human judgment in this framework; this is the first
time something in the corpus argued for a particular answer.

## Claim: request-rejected-with-unknown-quote
Scope: component
Quantification: universal
Oracle: contract

Exercise malformed encodings and byte alterations of an otherwise valid token. Pricing is
substituted; the compiled token contract, HTTP serialization and real trip store are not.
