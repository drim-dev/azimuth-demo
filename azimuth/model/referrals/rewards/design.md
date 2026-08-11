# Design: referrals/rewards

## Requirement: reward-follows-first-capture
Mechanism: payment-capture-consumer
Enforcement: choke-point
Binding: dotnet-symbol:Trips.Features.Referrals.ConsumePaymentCaptured.RequestHandler.Handle
Mechanism: reward-source-uniqueness
Enforcement: constraint
Binding: postgres-index:referral_credits.ux_referral_credit_source_beneficiary

Payments publishes a system-of-record capture fact after recording a capture. Trips resolves its
local trip and attribution, and only that consumer may qualify the attribution and create the two
fixed 500-minor-unit credits. The payment event inbox makes exact redelivery cheap. A trip row lock
serializes different event ids for the same logical capture, while source-and-beneficiary
uniqueness is the final guard against duplicate value.

No trip lifecycle transition grants a reward. A decline creates no capture fact, so completion
without successful local capture cannot qualify the attribution.

## Requirement: credit-redemption-is-authorized-once
Mechanism: referral-credit-authority-issuance
Enforcement: choke-point
Mechanism: referral-credit-authority-validation
Enforcement: guard
Mechanism: reserved-credit-row
Enforcement: constraint
Binding: postgres-index:referral_credits.ux_referral_credit_reserved_trip
Mechanism: captured-credit-uniqueness
Enforcement: constraint
Binding: postgres-index:captures.ux_capture_referral_credit
Mechanism: credit-capture-reconciliation
Enforcement: choke-point
Binding: dotnet-symbol:Trips.Features.Referrals.ConsumePaymentCaptured.RequestHandler.Handle

Trips locks an available credit during admission, verifies owner, state, currency and fare, then
signs credit id, trip id, amount and currency. The authority travels with every lifecycle event.
Payments authenticates and rechecks all bindings before deriving the negative adjustment; neither
the rider nor the dispatch endpoint supplies an amount or reason. The capture retains original fare,
credit identity and value, and final amount.

Cancellation releases a reservation. Decline leaves it reserved for a later payment-method retry.
A recorded capture changes it to used through the payment fact. The Trips row state and Payments'
unconditional unique credit index defend their respective stores; neither is presented as a
distributed transaction.

## Requirement: rider-sees-referral-state
Mechanism: referral-state-projection
Enforcement: choke-point
Binding: dotnet-symbol:Trips.Features.Referrals.GetReferralSummary.RequestHandler.Handle

The rider BFF obtains the Trips-owned summary. A production server-rendered summary page names the
code, attribution state, and every credit's amount, currency and textual state. The request form
also exposes the same state and requires explicit credit selection. The generated rider identity is
session-stable fixture state, not authentication.

## Residue

The fixed value has no expiry, tax treatment, ledger posting or refund restoration. The symmetric
signing key gives caller-facing integrity but not authority separation between Trips and Payments.
The provider's unobserved outcome is still treated as a local system-of-record capture; external
processor reconciliation remains the boundary described in `payments/capture`.

Broker declaration establishes requested topology, not deployed permissions or notification
routing. Referral identity is a caller-supplied fixture string. Any production interpretation must
replace it with authenticated account identity before codes or summaries become sensitive value.
