# Intent delta: security/quote-tokens

## Add requirement: signed-quote-integrity
Criticality: critical

A quote token SHALL disclose its payload only when the configured signing authority issued it and
its encoded body and signature are unchanged.

### Add scenario: issued-token-round-trips
GIVEN any valid quote payload
WHEN the configured signing authority issues and then decodes its token
THEN the exact payload is returned

### Add scenario: altered-token-rejected
GIVEN a token issued by the configured signing authority
WHEN any encoded body or signature position is changed
THEN the token is rejected without disclosing its payload

### Add scenario: foreign-signature-rejected
GIVEN a token issued by a different signing authority
WHEN the configured signing authority decodes that token
THEN the token is rejected without disclosing its payload
