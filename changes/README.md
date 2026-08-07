# Changes

Status: **experimental contract, not parsed**. D21 decides the lifecycle; this file is the manual
protocol for the first feature that tests it. Syntax that survives that feature may later become a
parser contract. Nothing here is current product truth.

## Authority

`specs/`, `design/` and `verification/` describe the accepted current state. A directory under
`changes/` describes a proposed target state. A planned mechanism may be absent. The same statement
in current `design/` asserts that the mechanism exists now.

The target projection is:

```
current model + change deltas = proposed target model
```

Until change-aware tooling exists, reviewers perform that projection explicitly. Current checks
must not read active changes as current facets.

## Provisional shape

```
changes/<change-id>/
├── proposal.md
├── specs/               # intent deltas, only where behaviour changes
├── design.md            # optional solution design
├── verification.md      # deviations and obligations introduced by the design
└── plan.md              # transient implementation order and work
```

`proposal.md` states the problem, scope, affected claim ids, criticality changes and completion
conditions. `design.md` is required only when alternatives, boundaries or failure modes make a
reviewable solution decision necessary. A routine claim does not acquire assurance artifacts merely
because its change contains a solution design.

This is a working shape, not a required five-file ceremony. Omit a file that carries no
non-derivable information and record whether its absence caused a problem.

## Criticality

Changing criticality preserves requirement and scenario identity. Record the old and new value,
why consequence changed, and—when lowering—the condition that would raise it again.

The manual target review derives the new obligations:

| Level | Linkage | Current mechanism design | Evidence | Agent judgment |
|---|---|---|---|---|
| `routine` | none | none | none | none |
| `standard` | `realizes`, `covers` | optional | required | optional |
| `critical` | `realizes`, `covers` | required | critical floor | required* |

An untagged test is outside Azimuth's evidence model. Do not add `Untraced` to a routine test.
`*` Agent judgment is required when the agent tier is in use.

## Completion and archive

The first archive is manual. Before archiving:

1. Apply accepted intent deltas to `specs/`.
2. Distil mechanisms that actually exist into `design/`; do not copy planned mechanisms that were
   dropped.
3. Apply lasting evidence deviations and residuals to `verification/`.
4. Record departures from the proposal and why they occurred.
5. Record the final commit and model fingerprint when available.
6. Move the whole directory to `changes/archive/YYYY-MM-DD-<change-id>/` without rewriting its
   history.

Rejected and abandoned changes are archived too, with their outcome and reason, but update no
current facet.

## What the experiment measures

Record separately for routine, standard and critical claims:

- authoring minutes and framework-only lines;
- files touched only for Azimuth;
- manual tags added;
- information duplicated from code or another artifact;
- findings that changed implementation or verification;
- unused fields and missing concepts;
- manual archive steps that are derivable.

The routine path is falsified if it costs materially more than an equivalent OpenSpec change. Do
not automate the format until this record exists.
