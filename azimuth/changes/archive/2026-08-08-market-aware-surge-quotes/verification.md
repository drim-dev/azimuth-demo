# Verification: market-aware-surge-quotes

## Claim: total-equals-components
Scope: component
Quantification: universal
Oracle: metamorphic

Generate component values through the Pricing HTTP boundary and assert the sum relation on the
serialized response and decoded token. Real serialization is part of the claim.

## Claim: current-pressure-selects-surge
Scope: component
Quantification: universal
Oracle: model-based

Compare the service response against an independent integer policy model over demand, supply and
component values. Include boundary values and overflow rejection.

## Claim: stale-pressure-does-not-select-surge
Scope: component
Oracle: direct

Move the injected clock across the freshness boundary against real stored observations.

## Claim: surge-is-a-quote-component
Scope: e2e
Oracle: contract

Carry one quote through Pricing, rider BFF, Trips and Payments. The process boundaries and wire
formats are the content of the claim.

## Claim: request-admitted-with-valid-quote
Scope: component
Quantification: universal
Oracle: contract

Trips component evidence varies every signed field and proves that altered or expired tokens admit
no trip. Pricing itself is substituted; the signed wire contract is real.

## Claim: capture-equals-trip-fare
Scope: component
Quantification: universal
Oracle: model-based

Payments independently sums signed components and passes that value to a recording provider. A
mutation check removes the surge component from the capture calculation; the evidence must fail.

## Routine observation

The rider breakdown rendering test remains an ordinary untagged test. D20 is falsified if the
change needs a framework exemption or linkage tag for it.

## Residual: trusted-pressure-reporter
Accepted: for this process experiment; revisit when the second behavior depending on market pressure
is proposed, because that is the first evidence that a reusable observation pipeline is warranted

No test in this change establishes that reported demand and supply match production reality.
