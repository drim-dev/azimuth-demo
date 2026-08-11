# Changes

Status: **experimental parsed contract for additions and criticality transitions**. Five completed
product changes and the explicit need to raise routine claims supplied the evidence for D21.4 and
D24. Unsupported delta operations fail; they are not interpreted as prose.

## Authority

Packages under `azimuth/model/` describe the accepted behaviour, mechanisms and assurance account
of the current codebase. A directory under `azimuth/changes/` describes a proposed target state. A
planned mechanism may be absent. The same statement in a current package's `design.md` asserts that
the mechanism exists now. Accepted codebase state does not assert that every production instance
or user cohort already runs it (D31).

The target projection is:

```
current model + change deltas = proposed target model
```

`azimuth check` still reads accepted state only. `azimuth change check <dir>` projects supported
additions and criticality transitions, reports planned versus applied state and derives their
criticality obligations without treating planned mechanisms as current facts.

## Change, branch and rollout

An Azimuth change is a semantic transition, not a Git branch or a release (D31). A small change may
fit one branch; a large change may use several work-package branches, merge requests and
repositories; a release may contain several accepted changes. Branch naming and merge topology are
project policy, not framework semantics.

Production receives an immutable artifact built from protected mainline or the project's
established release-candidate commit, never a mutable developer branch. The same artifact is
promoted through environments while flags, configuration or traffic routing govern exposure.

Archive normally follows engineering acceptance and pre-production evidence, before limited
production exposure. Delay archive for a canary only when the proposal declared a production
observation necessary for acceptance before implementation. In that exceptional case record the
artifact, observation window, oracle and failure action; importing the result changes the evidence
fingerprint and requires a current judgment. See `docs/change-process.md` for the composed
operating protocol.

## Shape

```
azimuth/changes/<change-id>/
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

## Parsed intent deltas

### Additive requirement

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
requirement statement and every scenario step—not merely their ids—are already present in the
current model.

### Criticality transition

```markdown
# Intent delta: trips/rider-view

## Change criticality: compact-trip-summary
From: routine
To: standard
Because: the summary now drives a support decision
```

The requirement and scenario ids do not change. `From` must match current intent before application;
after application `To` must match. A third value is an error, so a stale delta cannot be silently
reinterpreted. Lowering additionally requires:

```markdown
Revisit: raise again if the summary becomes an input to an automated decision
```

Replacement, removal and scenario movement are not parsed yet. Their appearance is an error, not
an invitation to guess.

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

1. Apply accepted intent deltas to the affected packages' `spec.md` files.
2. Distil mechanisms that actually exist into sibling `design.md`; do not copy planned mechanisms
   that were dropped.
3. Apply lasting evidence deviations and residuals to sibling `verification.md`.
4. Record departures from the proposal and why they occurred.
5. Set the proposal status to `accepted and complete` and complete every plan item.
6. Write `outcome.md` with `Status: accepted` plus `## Departures`, `## Residual decisions` and
   `## Measurements`. Empty-but-explicit sections are preferable to invented content.

Then run:

```text
azimuth change finalize azimuth/changes/<id> [model options]
azimuth change archive azimuth/changes/<id> --date YYYY-MM-DD [model options]
```

Finalization requires at least one applied supported delta and a hole-free accepted model, and
writes the SHA-256 model fingerprint and summary. Archive verifies that this file is fresh before
moving the directory to `azimuth/changes/archive/YYYY-MM-DD-<change-id>/`. The commands derive no
explanations and accept no risk.

Rejected and abandoned changes are archived too, with their outcome and reason, but update no
current facet. The command currently automates accepted changes only; other dispositions remain a
manual move until one is observed.

## What this experiment measures

This section evaluates Azimuth in this repository. It is not a proposed field for every production
change. The current experimental finalizer requires `## Measurements` so that framework costs and
findings cannot be omitted from the fixture outcomes; a production distribution should remove
that requirement unless it is deliberately running the same adoption experiment.

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
