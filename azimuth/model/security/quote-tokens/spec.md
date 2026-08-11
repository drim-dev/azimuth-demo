# Spec: security/quote-tokens

Authenticity and integrity of the quote token shared by Pricing, Trips and Payments. Owns the
portable token contract, not quote calculation, trip admission, capture amount or signing-key
distribution.

## Requirement: signed-quote-integrity
Criticality: critical

A quote token SHALL disclose its payload only when the configured signing authority issued it and
its encoded body and signature are unchanged.

### Scenario: issued-token-round-trips
GIVEN any valid quote payload
WHEN the configured signing authority issues and then decodes its token
THEN the exact payload is returned

### Scenario: altered-token-rejected
GIVEN a token issued by the configured signing authority
WHEN any encoded body or signature position is changed
THEN the token is rejected without disclosing its payload

### Scenario: foreign-signature-rejected
GIVEN a token issued by a different signing authority
WHEN the configured signing authority decodes that token
THEN the token is rejected without disclosing its payload
