# Verification: ride-hailing-delivery-alerts

## Claim: relay-backlog-raises-alert
Scope: unit
Quantification: example
Oracle: direct

The `promtool` rule test drives a synthetic Analytics backlog series through the declared `for`
interval and expects `TripEventAnalyticsBacklog` with its warning label and annotation.

## Claim: dead-letter-presence-raises-alert
Scope: unit
Quantification: example
Oracle: direct

The `promtool` rule test supplies a non-zero Analytics dead-letter series and expects
`TripEventDeadLetters`. Its zero-series case remains ordinary evidence against a permanently active
expression.

## Account-preservation evidence

Capture the accepted model export and extracted source relations before moving the existing change
artifacts, then compare them with the post-split account. The comparison must be exact after
normalizing only command-generated timestamps or output paths. No application or accepted-model
path is in scope for modification.

Run `azimuth change check` for both active changes. This change must report the two alert scenarios
as applied. The generic change may report no intent delta, but that limitation must remain explicit
until canonical acceptance provides a framework-only completion path.

## Residual

Prometheus configuration and rule tests establish declared evaluation behavior. They do not prove
that a deployment loaded the rules, scraped the required metrics, routed a notification or caused
an operator response. Those propositions still require expiring operational observations.
