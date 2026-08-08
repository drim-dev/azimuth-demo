# Design: payments/capture

## Requirement: captured-once
Enforcement: constraint
Binding: postgres-index:captures.ux_capture_trip
Expect: unique=true; columns=trip_id; predicate=NOT voided

The application-level idempotency key is a courtesy that makes the common path cheap and the error
message good. The index is what actually holds the line, and it holds it against paths that did not
exist when it was written — a support-issued charge, a reconciliation retry, a new cancellation-fee
flow. An advisory lock was rejected: it protects the writers that take it.

## Requirement: capture-on-completion
Enforcement: choke-point
Binding: dotnet-symbol:Trips.Features.Trips.TransitionTrip.RequestHandler.Handle
Enforcement: choke-point
Binding: dotnet-symbol:Payments.Features.Captures.CaptureSettlementWorker.ExecuteAsync
Enforcement: constraint
Binding: dotnet-symbol:Common.Messaging.TripEventTopology.DeclareAsync
Enforcement: choke-point
Binding: dotnet-symbol:Payments.Features.TripEvents.ConsumeTripStateChanged.RequestHandler.Handle

A transactional lifecycle-event outbox rather than a direct call or shared handoff table. Calling
the payment client inline from the completion handler is the single most-repeated mistake in the
concern catalog (C16): it charges riders for transactions that roll back, and no behavioural test
catches it because the failing case needs a rollback at one exact instant.

The relay publishes through a durable broker binding. Payments records the event id and highest trip
version before creating its local capture intent; the settlement worker drains that intent without
an operator calling the dispatch endpoint. Capture remains asynchronous, and overdue intents plus
broker backlog are exposed to external detectors rather than silently waiting.

## Requirement: capture-amount-matches-quote
Enforcement: choke-point
Binding: dotnet-symbol:Payments.Features.Captures.CaptureTrip.RequestHandler.Handle

The event carries the immutable token rather than a second amount/currency pair. Payments does not
trust a forwarded total and does not call Pricing, so the quote-to-capture relation survives either
service being unavailable after admission.

## Residue

**Adjustment authority is not modelled.** The dispatch endpoint accepts a delta and reason but the
fixture has no service authentication or role capable of authorizing it. The evidence establishes
arithmetic and attribution, not that an appropriate actor chose the adjustment.

**Voided captures are not deleted.** The unique index is partial for this reason. A voided capture
stays as a row so that the history of a disputed trip is legible; the cost is that every query
against `captures` must filter, and forgetting to is a real and recurring bug. If this ever becomes
a second table, the index above must move with it.

**The payment provider is treated as untrusted for outcomes but trusted for money.** A response we
never observe is assumed to have possibly succeeded, hence `retry-after-transport-failure`. But the
provider's own idempotency is relied upon rather than verified, because verifying it would require a
reconciliation we do not yet have. This is the seam where C8 will land.
