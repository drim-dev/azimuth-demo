# Design: payments/capture

## Requirement: captured-once
Enforcement: constraint
Site: `ux_capture_trip` — partial unique index on `captures(trip_id)` where the capture is not
voided

The application-level idempotency key is a courtesy that makes the common path cheap and the
error message good. The index is what actually holds the line, and it holds it against paths that
did not exist when it was written — a support-issued charge, a reconciliation retry, a new
cancellation-fee flow. An advisory lock was rejected: it protects the writers that take it.

## Requirement: capture-on-completion
Enforcement: choke-point
Site: `DispatchCaptures` is the only reader of `capture_intents` and the only constructor of a
capture

*(revised 2026-08-07)* The entry read "`CompleteTrip` writes a capture-intent row in the same
transaction as the state change". **It does not, and no such site exists.** The trip service has no
reference to payments, no intent table and no outbox; `WriteCaptureIntent` lives in payments, is
reachable from nothing, and has no endpoint. What is built is the *reading* half: an intent, however
it arrives, produces at most one capture.

The outbox argument below still holds and is why the shape was chosen. It describes a design that
was written down and not implemented, which is a different thing from a design that is wrong, and
the distinction is worth keeping visible.

A transactional outbox rather than a direct call. Calling the payment client inline from the
completion handler is the single most-repeated mistake in the concern catalog (C16): it charges
riders for transactions that roll back, and no behavioural test catches it because the failing
case needs a rollback at one exact instant.

The cost is that capture is asynchronous, so `no-capture-before-completion` is momentarily true
after completion. That is deliberate and is why the claim is worded about the completed state
rather than about elapsed time.

## Requirement: capture-amount-matches-quote
Enforcement: choke-point
Site: `CaptureDispatcher.Capture` recomputes the amount from the trip's stored fare and its
adjustments, and is the only constructor of a capture request

Recomputation at the point of capture rather than trusting the amount carried through the
pipeline. This is half of C9: the other half — that the quoting path and this path agree — is not
enforced here and cannot be, since it is a property of two implementations rather than of either.

## Residue

**Voided captures are not deleted.** The unique index is partial for this reason. A voided
capture stays as a row so that the history of a disputed trip is legible; the cost is that every
query against `captures` must filter, and forgetting to is a real and recurring bug. If this ever
becomes a second table, the index above must move with it.

**The payment provider is treated as untrusted for outcomes but trusted for money.** A response we
never observe is assumed to have possibly succeeded, hence `retry-after-transport-failure`. But
the provider's own idempotency is relied upon rather than verified, because verifying it would
require a reconciliation we do not yet have. This is the seam where C8 will land.
