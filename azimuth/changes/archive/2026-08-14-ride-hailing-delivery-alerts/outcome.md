# Outcome: ride-hailing-delivery-alerts

Status: accepted

## Result

`ride-hailing-delivery-alerts` is now the only active change containing the applied intent delta
for `trip-delivery-health-is-alerted` and its two scenarios. It also owns their claim-specific
verification account. `generic-assurance-observations` now contains only the provider-neutral
protocol, adapters, synthetic fixtures and framework conclusions.

The generic outcome retains field provenance through the exact pre-split demo revision
`68a2eb5d46daf01ba087ec94b6a1ea7901c63bfd`. Canonical execution does not require that checkout.

The split changed no accepted model package, application source, Prometheus expression, rule-test
series, extracted relation or judgment.

## Departures

None. The approved proposal predicted the generic change's framework-only finalization limitation,
and implementation did not weaken that gate or introduce an unrelated intent delta.

## Residual decisions

`generic-assurance-observations` now has no parsed intent delta. The current finalizer requires at
least one applied delta, so a later canonical change must establish an honest completion path for
framework-only transitions before that change can be accepted. This change neither decides nor
implements that mechanism.

The Prometheus rule tests establish declared rule evaluation, not deployed scrape configuration,
notification routing or operator response. Those propositions still require expiring operational
observations.

Neither active change was finalized, archived or transferred.

## Measurements

- The accepted export compared byte-for-byte equal before and after the split, including after the
  complete gate regenerated both language manifests.
- SHA-256 digests remained equal for four affected model files, two Prometheus files and three
  manifest or observation inputs.
- The demo-local change projects two applied standard scenarios: 90 current claims to 90 target
  claims, with zero accepted-state errors or warnings.
- The generic change projects no intent delta: 90 current claims to 90 target claims, with zero
  accepted-state errors or warnings.
- The focused `promtool` run passed `trip-events.rules.test.yml`.
- The agent-tier worklist reported both rehomed claims as current `sound` judgments at fingerprints
  `ae7574a94c696259` and `8ee0e7053bb70a13`; direct review found no adverse verdict.
- `scripts/check.sh` exited zero. It passed the Rust core and 33-case federation suite, assurance
  service tests and web build, 19 .NET and 50 TypeScript extractor tests, 84 application component
  tests, both Prometheus rule suites, both application web builds, 12 composed E2E scenarios,
  polyglot conformance and assurance-extension conformance.
- The complete application model contains 90 claims in 11 specs with no holes, errors or warnings.
  The polyglot model contains seven claims and the assurance-extension model contains six; both are
  also hole-free.
