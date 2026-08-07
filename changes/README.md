# Changes

Status: **experimental parsed contract for additive intent changes**. Two completed changes supplied
the evidence for D21.4. Unsupported delta operations fail; they are not interpreted as prose.

## Authority

`specs/`, `design/` and `verification/` describe the accepted current state. A directory under
`changes/` describes a proposed target state. A planned mechanism may be absent. The same statement
in current `design/` asserts that the mechanism exists now.

The target projection is:

```
current model + change deltas = proposed target model
```

`azimuth check` still reads accepted state only. `azimuth change check <dir>` projects supported
additions, reports planned versus applied state and derives their criticality obligations without
treating planned mechanisms as current facts.

## Shape

```
changes/<change-id>/
├── proposal.md
├── specs/               # intent deltas, only where behaviour changes
├── design.md            # optional solution design
├── verification.md      # deviations and obligations introduced by the design
├── plan.md              # transient implementation order and work
├── outcome.md           # authored departures, residual decisions and measurements
└── finalization.json    # derived; written by `azimuth change finalize`
```

`proposal.md` states the problem, scope, affected claim ids, criticality changes and completion
conditions. `design.md` is required only when alternatives, boundaries or failure modes make a
reviewable solution decision necessary. A routine claim does not acquire assurance artifacts merely
because its change contains a solution design.

This is not a required seven-file ceremony. Omit a file that carries no
non-derivable information and record whether its absence caused a problem.

## Parsed additive delta

An additive intent file uses the ordinary requirement content with operation-shaped headings:

```markdown
# Intent delta: trips/rider-view

## Add requirement: compact-trip-summary
Criticality: routine

The statement.

### Add scenario: summary-shows-state-and-fare
GIVEN ...
WHEN ...
THEN ...
```

The machine derives target claim count and criticality. It marks an addition applied only when its
requirement statement and every scenario step—not merely their ids—are already present in current
specs. Replacement, removal, scenario movement and criticality transitions are not parsed yet.
Their appearance is an error, not an invitation to guess.

## Criticality

Changing criticality preserves requirement and scenario identity. Record the old and new value,
why consequence changed, and—when lowering—the condition that would raise it again.

The target projection derives the new obligations:

| Level | Linkage | Current mechanism design | Evidence | Agent judgment |
|---|---|---|---|---|
| `routine` | none | none | none | none |
| `standard` | `realizes`, `covers` | optional | required | optional |
| `critical` | `realizes`, `covers` | required | critical floor | required* |

An untagged test is outside Azimuth's evidence model. Do not add `Untraced` to a routine test.
`*` Agent judgment is required when the agent tier is in use.

## Completion and archive

Before accepting a change:

1. Apply accepted intent deltas to `specs/`.
2. Distil mechanisms that actually exist into `design/`; do not copy planned mechanisms that were
   dropped.
3. Apply lasting evidence deviations and residuals to `verification/`.
4. Record departures from the proposal and why they occurred.
5. Set the proposal status to `accepted and complete` and complete every plan item.
6. Write `outcome.md` with `Status: accepted` plus `## Departures`, `## Residual decisions` and
   `## Measurements`. Empty-but-explicit sections are preferable to invented content.

Then run:

```text
azimuth change finalize changes/<id> [model options]
azimuth change archive changes/<id> --date YYYY-MM-DD [model options]
```

Finalization requires an applied delta and a hole-free accepted model, and writes the SHA-256 model
fingerprint and summary. Archive verifies that this file is fresh before moving the directory to
`changes/archive/YYYY-MM-DD-<change-id>/`. The commands derive no explanations and accept no risk.

Rejected and abandoned changes are archived too, with their outcome and reason, but update no
current facet. The command currently automates accepted changes only; other dispositions remain a
manual move until one is observed.

## What the experiment measures

Record separately for routine, standard and critical claims:

- authoring minutes and framework-only lines;
- files touched only for Azimuth;
- manual tags added;
- information duplicated from code or another artifact;
- findings that changed implementation or verification;
- unused fields and missing concepts;
- manual archive steps that are derivable.

The routine path remains falsified if it costs materially more than an equivalent OpenSpec change.
The compact-summary run added one routine scenario with zero linkage of its own; its five tags
belonged to pre-existing critical privacy claims over the new surface.
