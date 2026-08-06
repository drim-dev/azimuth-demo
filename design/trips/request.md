# Design: trips/request

## Requirement: valid-quote-required
Enforcement: choke-point
Site: `AdmitRideRequest` is the only constructor of a trip; it resolves the quote and rejects on
absence or expiry before any write

One admission path rather than validation spread across the BFF and the service. The BFF checks
expiry too, for a fast rejection and a better message, but that check is presentation — if it
were the only one, a direct call to the service would create a trip on an expired fare.

## Requirement: one-active-trip-per-rider
Enforcement: constraint
Site: `ux_trip_rider_active` — partial unique index on `trips(rider_id)` where state is not
terminal

The same shape as `captured-once` and for the same reason: two requests arriving together both
read "no active trip". A check in `AdmitRideRequest` handles the ordinary case and produces the
error the rider sees; the index handles the case that actually breaks.

Note the coupling: this index depends on the set of terminal states, which `trips/lifecycle` owns.
Adding a state and forgetting to classify it silently widens or narrows this rule. Nothing
currently catches that.

## Residue

**Quote consumption is recorded on the quote, not on the trip.** A quote carries a
`consumed_by_trip` reference, so `quote-consumed-once` is enforced in `pricing`'s table by a
service that does not own trips. This is a deliberate inversion — the alternative is a
distributed check across two services on the request path — and it means `pricing` has a column
whose only writer is `trip`. If those services are ever split across a real boundary, this is the
first thing that breaks.
