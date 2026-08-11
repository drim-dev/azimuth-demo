# Design: market-aware-surge-quotes

## Decision

Pricing becomes a process boundary with its own store. `POST /market-pressure` records observations;
`POST /quotes` selects the newest observation for the pickup market within the policy window,
calculates base, distance and surge, persists the immutable quote and returns an opaque signed token
plus a display projection. `GET /quotes/{id}` reads the immutable record.

The policy is deliberately small and integer-only: policy `surge-v1` adds 20% of base plus distance
when open requests exceed available drivers, otherwise zero. Integer division rounds down. A fixed
rule gives the experiment a falsifiable oracle without introducing configuration machinery before a
second pricing concern exists.

The signed token contains quote id, locations, issue and expiry instants, policy and pressure ids,
currency, ordered components and total. HMAC-SHA256 authenticates a canonical binary encoding.
Trips and Payments share only the token codec and value contract; neither calls Pricing to decide
whether a carried quote is authentic. Each verifies the signature, expiry where relevant, currency
agreement and component sum locally.

Trips stores the accepted token, quote id, total and currency on the trip. A unique quote-id index
preserves consume-once without a Trips-owned quote row. Payments stores the token on its capture
intent and independently derives the provider amount from the verified components.

## Alternatives

Keeping pricing in Trips was rejected because it cannot validate a multi-process design artifact or
transport evidence. Asking Pricing to validate every use was rejected because an unavailable
Pricing process would block capture and because it would let both consumers trust one opaque answer.
Sending an unsigned JSON breakdown was rejected because transport could change the amount without a
detectable fault.

Deriving pressure directly from Trips and Drivers events was deferred. It would test event
aggregation and freshness at the same time as quote fidelity, making a failure hard to localize.
The internal observation boundary keeps freshness and policy selection real while naming the
trusted-producer residue.

## Failure modes

- unknown market or no fresh observation: quote with zero surge, not a failed quote;
- malformed or altered token: business-rule refusal before any trip/capture write;
- expired token: refused by Trips; Payments accepts an already-admitted expired token because quote
  expiry governs admission, not completion;
- mixed currencies, overflow or a total mismatch: invalid token, never repaired;
- pressure reporter replay: the newest observation wins; observations older than the freshness
  window have no pricing effect.

## Requirement: money-representation
Enforcement: type
Site: `Money` admits only integer minor units and an explicit currency

## Requirement: quote-components-sum-to-total
Enforcement: choke-point
Site: `QuoteTokenCodec` validates currency agreement and recomputes the total on every decode

## Requirement: surge-policy-applied
Enforcement: choke-point
Site: `IssueQuote` is the only quote constructor and selects pressure plus policy before signing

## Requirement: capture-amount-matches-quote
Enforcement: choke-point
Site: `CaptureTrip` verifies the signed quote and recomputes its component sum before provider IO

## Residue

The pressure reporter is trusted to describe the market honestly. Authentication and derivation
from demand/supply events are absent. Evidence can establish freshness and policy mapping, but not
that the observation represents the real world.

The signing key is shared by Pricing, Trips and Payments in this fixture. Key distribution,
rotation and compromise recovery are operational concerns not exercised here.
