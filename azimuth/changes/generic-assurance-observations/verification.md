# Verification: generic-assurance-observations

## Claim: relay-backlog-raises-alert
Scope: unit
Quantification: example
Oracle: direct

The operations repository's `promtool` rule test drives a synthetic backlog series past the
declared `for` interval and expects the named alert with its labels and annotation.

## Claim: dead-letter-presence-raises-alert
Scope: unit
Quantification: example
Oracle: direct

The operations repository's `promtool` rule test supplies a non-zero dead-letter series and expects
the named alert. A zero-series case remains ordinary rule-suite evidence against a permanently-on
expression.

## Protocol validation

Synthetic framework fixtures use claims unrelated to the ride-hailing model. A load observation
binds one execution to latency and error-rate assertions. A chaos observation binds one execution
to degraded-service, recovery and alert assertions. SARIF and mutation observations bind only as
challenges and are checked for target resolution and judgment staleness.
