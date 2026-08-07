# Spec: pricing/quote

Fare quotation for a prospective trip. Owns what a quote contains, how long it stays valid, and
what it costs.

Does not own whether a ride request is accepted (`trips/request`), nor what is actually charged
at the end of a trip (`payments/capture`). A quote is an offer, not a commitment to a ledger.

## Requirement: quote-issued
Criticality: standard

The system SHALL issue a fare quote for a pickup and dropoff pair, containing a total amount, a
currency, and an expiry instant.

### Scenario: quote-returned
WHEN a rider requests a fare for a pickup and a dropoff
THEN a quote is returned carrying a total amount, a currency and an expiry instant
AND the quote carries a signed representation that a later ride request can present

### Scenario: unserviceable-area
GIVEN a pickup location outside every serviced market
WHEN a rider requests a fare for that pickup
THEN no quote is issued
AND the rider is told the area is not served

## Requirement: quote-valid-until-expiry
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

## Requirement: money-representation
Criticality: critical

A quote's total and every component SHALL be expressed as integer minor units in one stated
currency.

### Scenario: total-in-minor-units
WHEN a quote is issued
THEN its total is an integer count of minor units
AND its currency is stated explicitly

## Requirement: quote-components-sum-to-total
Criticality: critical

A quote's total SHALL equal the sum of its components.

### Scenario: total-equals-components
WHEN a quote is issued with any set of fare components
THEN the total equals the sum of the component amounts

## Requirement: surge-policy-applied
Criticality: critical

Pricing SHALL apply the versioned surge policy from the latest non-stale pressure observation for
the pickup market, and SHALL carry the result as a signed quote component.

### Scenario: current-pressure-selects-surge
GIVEN a current pressure observation whose open requests exceed available drivers
WHEN a quote is issued for that market
THEN the versioned surge policy contributes a positive surge amount

### Scenario: stale-pressure-does-not-select-surge
GIVEN no current pressure observation for the pickup market
WHEN a quote is issued
THEN the surge amount is zero

### Scenario: surge-is-a-quote-component
WHEN a quote is issued
THEN surge is one of the signed components
AND the signed total equals base plus distance plus surge

## Requirement: quote-breakdown-shown
Criticality: routine

A quote SHALL carry a human-readable breakdown of its components for display.

### Scenario: breakdown-accompanies-quote
WHEN a quote is returned to a rider
THEN each fare component is listed with a label and an amount
