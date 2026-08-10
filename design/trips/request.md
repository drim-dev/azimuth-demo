# Design: trips/request

## Requirement: valid-quote-required
Mechanism: ride-request-handler
Enforcement: choke-point
Binding: dotnet-symbol:Trips.Features.Trips.RequestRide.RequestHandler.Handle
Mechanism: unique-quote-consumption
Enforcement: constraint
Binding: postgres-index:trips.ux_trip_quote
Expect: unique=true; columns=quote_id

One admission path rather than validation spread across the BFF and the service. Pricing is not
called at admission: token authenticity and internal consistency are local, so a Pricing outage
cannot turn a live quote into an unknown one. The reusable issuance and validation controls live in
`security/quote-tokens`; this handler is their current trip-admission application.

The token supplies quote identity across the process boundary. The index records consumption on the
trip and settles concurrent requests without writing Pricing's store.

## Requirement: one-active-trip-per-rider
Mechanism: unique-active-trip
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

The machine can resolve the admission handler and the reusable verifier independently, but it does
not derive the call between them. `security/quote-tokens` records the application-enumeration gap;
repeating a hand-written consumer list here would not close it.
