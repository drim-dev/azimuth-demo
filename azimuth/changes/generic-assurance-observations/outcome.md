# Outcome: generic-assurance-observations

Status: implemented, pending acceptance

## Result

Azimuth now has one provider-neutral observation protocol. Evidence bindings project into ordinary
`Covers` relations; challenge bindings enter judgment freshness without creating coverage. One
observation can bind to several claims, while every evidence binding retains its own assertion,
outcome and verification form.

Stryker.NET and SARIF use the same challenge path. Provider-neutral load and chaos fixtures use the
same evidence path. Prometheus rules and rule tests can contribute operational `Realizes` and
`Covers` relations from a separate operations repository.

## Departures

None. Native k6 and chaos-platform adapters were deliberately outside scope; their neutral fixture
results validate the extension boundary without putting either provider into the core.

## Residual decisions

Azimuth validates an imported observation's structure, linkage and freshness, but it still trusts
the adapter to interpret the native report honestly. Signed attestations and independently
re-running external tools remain possible extensions rather than core responsibilities.

Checked-in Prometheus configuration and rule tests establish declared operational behaviour. They
do not prove that a production deployment loaded the rule, routed the notification, or elicited an
operator response. Those propositions still require live operational receipts with appropriate
expiry.

The opaque provider payload is intentionally agent-facing. The machine tier fingerprints it and
fails closed on broken bindings, but does not acquire tool-specific policy for mutation scores,
SARIF severities, performance thresholds or chaos-resource states.

## Measurements

- The Rust domain contains one `Observation` abstraction and no mutation-, SARIF-, load-, k6- or
  chaos-specific core type, parser collection, hole or fingerprint role.
- One synthetic load execution supplies two separately formed evidence bindings; one chaos
  execution supplies three. A single SARIF observation challenges all six fixture claims without
  covering any of them.
- The experiment model contains six claims and is hole-free after importing those three
  observations. The accepted application model contains 90 claims in 11 specs and is also
  hole-free with zero errors or warnings.
- The federation suite now contains 33 passing cases. The operations repository supplies both new
  Prometheus realizations and both rule-test evidence relations in the complete assembly.
- Federation testing found one real source-identity collision: a Prometheus alert rule and its rule
  test originally shared an untyped address. Giving them distinct `prometheus-alert` and
  `prometheus-rule-test` source kinds fixed the ambiguity rather than weakening duplicate-source
  detection.
- The complete repository gate passes the Rust core and federation suites, 19 .NET extractor tests,
  36 TypeScript extractor tests, all service/component and composed end-to-end tests, both web
  production builds, both Prometheus rule suites, and assurance-extension conformance.
