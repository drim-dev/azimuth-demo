# Change: generic-assurance-observations

Status: implemented, pending acceptance

Exploration: composable-assurance-extensions
Carries decisions: E1, E2, E3, E4, E5

## Problem

The first mutation-testing integration proved that targeted hardeners can focus an agent judgment,
but it added mutation-specific records, parser branches, holes and fingerprint roles to the core.
Repeating that pattern for static analysis, load and chaos would make the core a registry of tools.
The current external-evidence shape also repeats one execution receipt per claim, hiding that one
run can legitimately establish several claim-specific assertions.

## Outcome

Azimuth imports an immutable assurance observation once and binds it explicitly to one or more
claims. An evidence binding becomes ordinary covering evidence with its own assertion, form and
outcome. A challenge binding becomes freshness-tracked judgment context over resolved realization,
evidence or mechanism subjects and never creates coverage.

The existing Stryker.NET path uses this protocol. A SARIF adapter proves that a second judgment
tool needs no core type. Provider-neutral k6 and chaos fixtures prove that one run can cover several
claims without a blanket result. The federation trial proves that Prometheus realization and
evidence can originate in the operations repository while intent remains singular elsewhere.

## Scope

In scope:

- the generic manifest, model export, linkage checks and judgment worklist;
- migration of the current mutation importer and checked-in assessment;
- a SARIF importer that derives claim challenge bindings from existing realization sites;
- provider-neutral load and chaos observation fixtures with claim-specific assertions;
- Prometheus rule and rule-test linkage emitted from the operations repository;
- documentation of additional testing techniques as extensions rather than core categories.

Out of scope:

- running k6, Chaos Mesh, CodeQL or another external service from `azimuth check`;
- treating dashboards as proof of deployment or alert delivery;
- a universal native-result adapter for every testing tool;
- automatic semantic judgment of static-analysis or mutation findings.

## Affected claims

- Add `analytics/trip-activity#relay-backlog-raises-alert` at standard criticality.
- Add `analytics/trip-activity#dead-letter-presence-raises-alert` at standard criticality.

## Completion conditions

- The Rust core contains no mutation-specific assessment type, parser collection, hole or
  fingerprint role.
- One observation can produce two or more independently formed `Covers` relations.
- A challenge whose subject no longer resolves produces a machine-tier error.
- A changed observation or configuration stales every judgment bound to it.
- Stryker.NET and SARIF both produce challenge observations through the same manifest contract.
- Load and chaos fixtures demonstrate shared execution metadata with per-claim assertions and no
  implicit blanket coverage.
- A complete federation assembly obtains the new operational `Realizes` and `Covers` relations
  from `rides-operations`, not from the backend repository.
- Focused importer, core and federation tests plus the repository check pass.
