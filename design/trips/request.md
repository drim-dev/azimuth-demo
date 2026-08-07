# Design: trips/request

## Requirement: valid-quote-required
Enforcement: choke-point
Binding: dotnet-symbol:Trips.Features.Trips.RequestRide.RequestHandler.Handle
Enforcement: guard
Binding: dotnet-symbol:Pricing.QuoteTokenCodec.Decode
Enforcement: constraint
Binding: postgres-index:trips.ux_trip_quote
Expect: unique=true; columns=quote_id

One admission path rather than validation spread across the BFF and the service. Pricing is not
called at admission: token authenticity and internal consistency are local, so a Pricing outage
cannot turn a live quote into an unknown one.

The token supplies quote identity across the process boundary. The index records consumption on the
trip and settles concurrent requests without writing Pricing's store.

## Requirement: one-active-trip-per-rider
Enforcement: constraint
Binding: postgres-index:trips.ux_trip_rider_active
Expect: unique=true; columns=rider_id; predicate=state NOT IN ('completed', 'cancelled')

The same shape as `captured-once` and for the same reason: two requests arriving together both
read "no active trip". A check in `AdmitRideRequest` handles the ordinary case and produces the
error the rider sees; the index handles the case that actually breaks.

Note the coupling: this index depends on the set of terminal states, which `trips/lifecycle` owns.
Adding a state and forgetting to classify it silently widens or narrows this rule. Nothing
currently catches that.

## Residue

Trips and Pricing share a signing key. A compromised consumer can therefore mint quotes; asymmetric
signing would narrow that authority but adds key lifecycle work this fixture does not exercise.
