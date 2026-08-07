# Change: market-aware-surge-quotes

Status: **accepted and complete**

## Problem

The current quote is calculated inside Trips from two caller-supplied numbers. That makes Pricing a
compile-time module rather than the runtime owner named by the specs, and it leaves no cross-service
artifact whose amount can be checked at ride admission and capture. It therefore cannot expose the
composition failures the framework is intended to find.

## Outcome

Pricing issues an immutable, signed quote from base, distance and market-pressure components.
Trips accepts that quote only while its signature and expiry are valid and stores the accepted
amount. Payments validates the same signed quote and derives the capture amount from its components
instead of trusting a forwarded total. The rider UI renders the returned breakdown without owning
price arithmetic.

Market pressure is reported through an internal Pricing endpoint. Producing that observation from
trip and driver event streams is outside this change; trusting the reporter is an explicit residual,
not an implied end-to-end demand model.

## Intent delta

In `pricing/quote`:

- split critical `quote-amount-integrity` without changing its scenario ids;
  - `money-representation` owns `total-in-minor-units`;
  - `quote-components-sum-to-total` owns `total-equals-components`;
- add critical `surge-policy-applied` with scenarios `current-pressure-selects-surge`,
  `stale-pressure-does-not-select-surge`, and `surge-is-a-quote-component`;
- keep `quote-issued` and `quote-valid-until-expiry` standard;
- keep `quote-breakdown-shown` routine and add no linkage for it.

In `trips/request`, the existing valid-, expired- and unknown-quote scenarios apply to signed quote
tokens instead of identifiers resolved from a Trips-owned quote table. Their identities and
criticalities do not change.

In `payments/capture`, `capture-equals-trip-fare` is strengthened: in the no-adjustment case the
capture equals both the accepted trip fare and the independently summed signed quote components.
Its id and criticality do not change.

## Scope

Included: the Pricing process and store, pressure snapshots, deterministic surge policy, signed
quote contract, Trips admission, Payments capture validation, rider transport and display, schema
migrations, component/contract/property/e2e evidence, and D20's intent-only routine behavior.

Excluded: changing criticality, a parsed changes/archive command, a demand-event aggregation
pipeline, configurable pricing policies, cancellation adjustments, provider reconciliation, and
enumerators for realization sites.

## Completion conditions

- a fresh high-pressure observation produces a non-zero `surge` component;
- no observation or only a stale observation produces a zero `surge` component;
- tampering with any signed field is refused by Trips and Payments;
- Pricing and Payments each establish `total = sum(components)` independently;
- a quote accepted by Trips is the quote used for capture;
- the rider sees base, distance and surge without performing price arithmetic;
- routine behavior needs neither `Realizes`, `Covers` nor `Untraced`;
- accepted deltas are applied to current facets and the change is archived with departures and
  measurements.
