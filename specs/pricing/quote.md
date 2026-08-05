# Spec: pricing/quote

Fare quotation for a prospective trip. Owns what a quote contains, how long it stays valid, and
what it costs.

Does not own whether a ride request is accepted (`trip/request`), nor what is actually charged
at the end of a trip (`payments/capture`). A quote is an offer, not a commitment to a ledger.

## Requirement: quote-issued
Criticality: standard

The system SHALL issue a fare quote for a pickup and dropoff pair, containing a total amount, a
currency, and an expiry instant.

### Scenario: quote-returned
WHEN a rider requests a fare for a pickup and a dropoff
THEN a quote is returned carrying a total amount, a currency and an expiry instant
AND the quote carries an identifier that a later ride request can reference

### Scenario: unserviceable-area
GIVEN a pickup location outside every serviced market
WHEN a rider requests a fare for that pickup
THEN no quote is issued
AND the rider is told the area is not served

## Requirement: quote-validity-window
Criticality: standard

A quote SHALL be valid from issuance until its expiry instant, and SHALL NOT be extended,
reissued or revalidated under the same identifier.

### Scenario: quote-valid-before-expiry
GIVEN a quote whose expiry instant has not passed
WHEN the quote is looked up
THEN it is reported valid

### Scenario: quote-invalid-after-expiry
GIVEN a quote whose expiry instant has passed
WHEN the quote is looked up
THEN it is reported expired
AND its total is unchanged

### Scenario: expired-quote-is-never-revalidated
GIVEN a quote that has been reported expired
WHEN a fare is requested again for the same pickup and dropoff
THEN a new quote with a new identifier is issued
AND the expired quote remains expired

## Requirement: quote-amount-integrity
Criticality: critical

A quote's total SHALL be expressed in integer minor units of its currency, and SHALL equal the
sum of its components.

### Scenario: total-in-minor-units
WHEN a quote is issued
THEN its total is an integer count of minor units
AND its currency is stated explicitly

### Scenario: total-equals-components
WHEN a quote is issued with any set of fare components
THEN the total equals the sum of the component amounts

## Requirement: quote-breakdown-shown
Criticality: routine

A quote SHALL carry a human-readable breakdown of its components for display.

### Scenario: breakdown-accompanies-quote
WHEN a quote is returned to a rider
THEN each fare component is listed with a label and an amount
