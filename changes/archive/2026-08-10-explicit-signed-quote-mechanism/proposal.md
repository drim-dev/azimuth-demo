# Change: explicit-signed-quote-mechanism

Status: accepted and complete

## Problem

Pricing, Trips and Payments already share one signed-quote codec, but its reusable security
mechanism is implicit. Current business designs name the codec as feature-local prose or symbol
bindings, and its tamper test is ordinary project evidence. The new D27 relations have only
synthetic validation, so they have not shown that one mechanism can carry its own evidence without
duplicating that evidence across dependent business claims.

## Scope

Add the concern-oriented `security/quote-tokens` spec for the contract already implemented by
`QuoteTokenCodec`: a configured authority can issue a token that round-trips its exact payload;
changing an encoded body or signature position is rejected; and a different signing authority is
rejected.

Declare issuance and validation as separate atomic mechanisms, derive their bindings from code,
and attach reusable mechanism evidence to generated token tests. Preserve the existing business
claims and their evidence in Pricing, Trips and Payments.

## Completion

- the current design owns stable issuance and validation mechanism ids;
- deleting either implementation relation produces a machine-tier hole;
- evidence varies generated payloads, every encoded mutation position and different keys;
- mechanism evidence is not copied onto every dependent business scenario;
- the inability to enumerate every application site is recorded rather than replaced by a
  hand-written consumer list;
- the agent tier judges every new or stale claim, and the accepted model is hole-free.
