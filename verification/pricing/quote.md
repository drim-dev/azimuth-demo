# Verification: pricing/quote

## Claim: total-in-minor-units
Strength: proof
Evidence: `Money` is integer-backed with no floating-point constructor or conversion, and quote
components and totals use `long` on every .NET boundary

Violation is unrepresentable within the services. This does not claim proof over JavaScript number
precision; that limit remains in the design residue.

## Claim: total-equals-components
Scope: component
Quantification: universal
Oracle: metamorphic

Generate distances, currencies and both pressure branches through HTTP; assert the serialized sum
and decode the signed token. Real serialization is part of the claim.

## Claim: current-pressure-selects-surge
Scope: component
Quantification: universal
Oracle: model-based

Compare boundary relations (`open = available`, one above, large values and zero supply) against an
independent integer policy expression rather than calling production policy code for the expected
answer.

## Claim: stale-pressure-does-not-select-surge
Scope: component
Quantification: universal

Move the injected clock to one tick before and exactly at the five-minute boundary against a stored
observation. A sample far beyond the boundary would pass against an unintended grace period.

## Claim: surge-is-a-quote-component
Scope: component
Quantification: universal
Oracle: contract

Across pressure branches and currencies, assert the exact ordered component labels and decode the
wire token independently. The real-process e2e adds one composition example through capture.

## Residual: trusted-pressure-reporter
Accepted: until a second behavior consumes market pressure, which is the first evidence that a
reusable authenticated observation pipeline is warranted

No evidence establishes that reported demand and supply match production reality.

## Residual: cross-language-money-boundary
Accepted: while fixture amounts remain within JavaScript's exact integer range; revisit before a
mobile client or materially larger monetary domain ships

The .NET proof does not cross into JavaScript's number representation.
