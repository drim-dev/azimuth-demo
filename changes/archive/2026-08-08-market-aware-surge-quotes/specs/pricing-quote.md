# Spec delta: pricing/quote

## Split: quote-amount-integrity

Replace the parent requirement with two critical requirements while retaining both scenario ids.

### Requirement: money-representation
Criticality: critical

A quote total and every component SHALL be expressed as integer minor units in one stated currency.

Moves: `total-in-minor-units`.

### Requirement: quote-components-sum-to-total
Criticality: critical

A quote total SHALL equal the sum of all of its components.

Moves: `total-equals-components`.

## Add: surge-policy-applied
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
