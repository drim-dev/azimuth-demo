# Design: security/quote-tokens

## Requirement: signed-quote-integrity
Mechanism: quote-token-issuance
Enforcement: choke-point
Mechanism: quote-token-validation
Enforcement: guard

Issuance validates and serializes one payload before authenticating its exact encoded body with
HMAC-SHA256. Validation authenticates the body with a fixed-time comparison before deserializing or
returning it. The two implementation bindings are derived from their annotated methods rather than
copied here.

The verifier is a guard because application is opt-in: the library can reject a changed token only
where a consumer calls it. The issuer is a choke point for tokens produced by this codec; signing
key distribution remains outside that claim.

## Residue

Pricing, Trips and Payments share a symmetric key. Any process that can validate can also mint a
token, so the mechanism supplies integrity against callers without the key, not authority separation
between the three services. Rotation, revocation and managed key storage are not exercised.

The codebase has no independent enumerator for every place that ought to apply validation. Current
business evidence exercises Trips admission and Payments capture, but a future consumer could omit
the guard without creating a mechanism-linkage hole. The accepted evidence residual records the
condition under which that gap must be revisited.
