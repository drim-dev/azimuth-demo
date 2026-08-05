# Verification: pricing/quote

## Claim: total-in-minor-units
Strength: proof
Evidence: money is a distinct integer-backed type with no floating-point constructor or
conversion, in every language that carries an amount

Violation is unrepresentable rather than untested. This is the strongest available result and is
recorded so that the absence of a runtime test for it reads as a design outcome rather than an
oversight.

## Claim: total-equals-components
Quantification: invariant
Oracle: metamorphic

Already `invariant` by criticality; recorded for the oracle. The useful check generates component
sets and asserts the sum relation, rather than asserting one arithmetic result that a
reimplementation of the same bug would also produce.

## Residual: cross-language-money-boundary
The proof above holds within each language. It does not cover the serialization boundary between
them, where an amount can be parsed into a wider or lossier type before being sent back. Concern
C10.
Accepted: until a second language carries an amount. The mobile client is where this first
becomes real, and the evidence then is a contract test at each boundary, not a stronger type.
