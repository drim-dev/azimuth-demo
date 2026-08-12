# Azimuth federation trial

Status: **machine-tier trial implemented; repository discovery remediated after one failed cold
trial; organizational replication remains external evidence**.

This laboratory tests the multi-repository abstractions as falsifiable propositions. It is not a
sample directory layout presented as validation. The conformance suite creates independent Git
repositories, binds every observation to a revision and then attempts to manufacture false green
results.

## Run

```text
cargo test --manifest-path tools/azimuth/Cargo.toml --test federation
```

To inspect a physical split of the current fixture without changing this checkout:

```text
experiments/multirepo/materialize.sh
```

The script prints a temporary workset root and the five independent revisions. Pass an empty
destination as its first argument to retain it at a known location. The generated repositories are
experimental outputs and are never imported into this repository's history.

## Compared configurations

The tests exercise three configurations:

1. the current monorepo, whose routine package and ordinary CLI are the control;
2. a split topology with one centralized model source;
3. a split topology with model sources owned by backend, experience and operations repositories.

The centralized account assembles completely, but an experience-local check cannot see its routine
intent without the model repository. The federated account can. This distinguishes semantic
correctness from local operating cost: centralization is not rejected because it cannot work, but
because it makes otherwise local changes nonlocal.

## Repository topology

```text
azimuth-engine       CLI, extractors, formats and framework tests
rides-backend        services, system intent and verification policy
rides-experience     rider/driver applications and experience intent
rides-operations     monitoring artifacts and operations intent
rides-assurance      project catalog, composed evidence and accepted snapshots
```

The executable fixture uses smaller source files but the same boundaries. `materialize.sh` applies
the topology to the real ride-hailing fixture.

## What is executable

The 33-scenario suite establishes:

- complete and local assembly have different, explicit states;
- a local routine requirement has intent only and no linkage;
- one critical receipt claim fans out across backend, experience and E2E areas;
- a repository manifest, model source and execution receipt are content-addressed;
- execution evidence names the exact repository revisions it observed;
- missing repositories, areas, model sources and receipts fail closed;
- duplicate model authority and duplicate area ownership fail closed;
- the same typed address in two areas is not a collision;
- one area/address resolving inconsistently is a collision;
- the most-specific mount is rederived rather than trusted from a producer;
- repository-owned paths and symbolic-link model inputs cannot escape their checkout;
- untracked and ignored model inputs cannot enter a clean snapshot;
- receipt ids and subjects are exact closed-world sets;
- unsupported protocol versions fail explicitly;
- dirty checkouts may be inspected but cannot be finalized;
- ordinary flat extractor output can be enveloped as a typed repository observation;
- operational claim realization and rule-test evidence can originate in the operations repository;
- assurance observations survive repository enveloping and retain resolvable subjects;
- relocating a complete area to another real Git repository preserves judgment freshness;
- changing an area identity without updating its source identities fails as a semantic transition;
- a monorepo control and the federated fixture derive the same claims, relations and holes;
- fifty real repositories and 5,000 typed sources assemble within the 30-second conformance bound.
- active and archived change observations match the tracked checkout exactly;
- one change id cannot have two repository authorities; and
- project acceptance binds an unchanged archive move to complete evidence before and after it.

The product-like routine workload is also implemented in the rider application as
`experience/display-density`. It adds no `Realizes`, `Covers`, design, verification or judgment
declaration.

## What is not established internally

A test written by the framework author cannot establish organizational usability. The first cold
agent exposed a real repository-local discovery failure: it could infer routine semantics but not
name its area or model source. After a second run exposed path-base and executable gaps, the
corrected split carries `azimuth/project-reference.json`, an executable local locator, an
authoritative catalog in assurance, and intent plus its archive in the repository that owns them.
The six-task cold protocol then completed without a structural corrective prompt, duplicate
authority or false finalization. That is useful agent-tier replication in this fixture, not evidence
of adoption by independent teams. The protocol also exposed a non-mechanical archive choreography
and a missing repository-local `promtool`; D34 closes the former and the latter now has a pinned
container wrapper. This laboratory assumes repository manifests are produced by
trusted CI. Digests and tracked-file checks prevent substitution; they do not prove that a malicious
producer reported a symbol's semantics truthfully. Singular authority for active cross-repository
change proposals also remains a policy proposition rather than a machine invariant.

Measurements and trial reports live here. They are not fields in production Azimuth changes.
