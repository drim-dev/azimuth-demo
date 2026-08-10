# Specs

The format is fixed by `docs/decisions.md` (D6, D11, D13, D15). This file states the contract
the parser enforces. Anything not described here is a parse error — the parser is strict by
design and fails loudly rather than guessing.

## Shape

```markdown
# Spec: <spec-id>

Free prose. Non-normative. Says what this spec owns and, more usefully, what it does not.

## Requirement: <requirement-id>
Criticality: critical | standard | routine

A SHALL statement, in prose.

### Scenario: <scenario-id>
GIVEN <precondition>          (optional, repeatable with AND)
WHEN <trigger>
THEN <observable outcome>
AND <further outcome>         (optional, repeatable)
```

## Identity

- **Spec ids are declared, never derived from the path.** `specs/trips/dispatch.md` and
  `specs/backend/trips/dispatch.md` hold the same spec if both declare `# Spec: trips/dispatch`.
  Moving a file breaks nothing.
- **Spec ids may be hierarchical.** The `/` is part of the id string, not a filesystem fact. It
  gives namespacing (`trips/dispatch` and `driver/dispatch` coexist) and selection
  (`--only 'trips/**'`) without coupling identity to layout.
- **Folder layout is convention.** A divergence between folder and id prefix is a warning, never
  an error.
- **Scenario ids are unique per spec, not per requirement.** Tags reference the pair
  `(spec-id, scenario-id)`. This is what makes splitting or merging a requirement free: scenarios
  move between parents without touching a single tag.
- Ids live in headings. Everything else lives on labelled lines, so that a change of criticality
  is a one-line diff rather than something that reads as a rename.

## Criticality

Declared on every requirement. Absence is a hole, not a default (D6.2). The level gates which
artifacts are required at all (D6.5):

| Level | Spec | `realizes` | Evidence / `covers` | Current design |
|---|---|---|---|---|
| `critical` | required | required | required at the critical floor | required |
| `standard` | required | required | required at the standard floor | optional |
| `routine` | required | — | — | — |

Routine means intent only (D20), not weaker tracing. Production code and tests for a routine claim
need no Azimuth tags, and an untagged test needs no exemption. The verification standard supplies
the evidence floor for standard and critical claims; a plan file contains deviations and other
non-derivable evidence facts, so no file is needed when the standard applies unchanged.

Scenarios inherit criticality from their requirement. Moving a scenario between requirements can
therefore change its rigor — visibly, in the spec diff, which is where it belongs.

Criticality may also change in place without changing the requirement or scenario id. D21.1 makes
that a change delta: raising it derives new obligations; lowering it records a rationale and revisit
condition; archiving preserves the transition.

## What scenarios do not carry

- **No `Quantification`.** Every claim is universal, so on the claim side the field would be
  constant. `example` vs `universal` records how thoroughly the *evidence* ranges, and lives in the
  verification plan (D5, D13, D19).
- **No `Scope`.** Required scope is an evidence judgment and lives in the verification plan. The
  tag on a test declares what that test actually is; `wrong-form` compares the two.
- **No cross-cutting notation.** The steel thread is deliberately built without it, with the
  eighteen concerns in `docs/concern-catalog.md` held as prose. The holes the per-scenario
  matrix actually misses are the evidence for what notation to add.

## Boundaries

**Specs are organized by domain area, not by service.** `trips/dispatch`, not `trip-service` or
`rider-bff`. If specs mirror services, a scenario crossing five services gets duplicated five
times and the fan-out this demo exists to study disappears. One claim, many realizing sites.

A spec is always a single file. If one outgrows its file, split it into two specs with two ids
rather than inventing a multi-file spec — no id changes and no tag breaks, because ids are
declared.

### Site-domain invariants and `Over:`

An invariant may replace scenarios with a site domain:

```markdown
## Invariant: position-confined-to-live-phases
Criticality: critical
Over: trips/rider-view
```

The last line currently names one existing **spec id**, not an informal business domain. Its member
set is the union of tagged production sites realizing behavioral claims in that spec and
extractor-emitted `class_members` whose class is that same id. The check also requires a successful
enumerator witness; tags alone cannot prove the class complete. An invariant in one spec may range
over another spec this way, but one `Over:` cannot yet union several specs or name an independently
declared reusable domain. That extension remains open pending a real cross-cutting mechanism.

## Style

- **An id is a compressed proposition** — a subject plus what must hold of it. If an id cannot be
  read as an assertion that is either satisfied or not, it is naming a topic rather than a rule:
  `terminal-states-are-final`, not `termination`; `captured-once`, not `capture`. A topic-shaped
  id will quietly absorb unrelated rules over time, which is how one requirement becomes five
  wearing a single identity.
- **Ids are never imperative.** `quote-issued`, not `issue-quote`. A requirement states what must
  be true, not what the system does; verb-first ids drift toward one requirement per endpoint,
  which is the same mistake as organizing specs by service. Most requirements have no verb at
  all — an imperative convention would only fit the CRUD-shaped minority.
- **Scenario ids must stand alone.** They appear in tags far from any context
  (`Covers("trips/dispatch", "late-acceptance-rejected")`) and have to be self-explanatory at the
  call site.
- **Keep requirement and scenario ids visibly distinct.** They are separate namespaces, but a
  scenario repeating its requirement's id is unreadable in a diff.
- **SHALL statements are normative and singular.** One rule per requirement. If it needs an
  "and", it is probably two requirements.
- **Scenarios are declarative, not mechanical.** They say what must be true, never how it is
  checked. "THEN exactly one capture exists", not "THEN assert the captures table has one row".
- **Where the evidence must be universal, the WHEN must quantify rather than instantiate.**
  Write "WHEN a completion event is delivered more than once", not "delivered twice". Both
  parse; only the first means what a property test must satisfy.
- **Diagrams are non-normative and ignored by the parser.** A diagram either illustrates and
  claims nothing, or it is the source of claims and nothing restates it. The failure mode is a
  diagram that looks authoritative, is not parsed, and quietly disagrees with the prose beside
  it.

## The steel thread

These specs cover one path end to end — request → quote → dispatch → acceptance → completion →
capture — deliberately spanning rider client, rider BFF, trip service, driver BFF, driver client,
pricing and payments. Breadth comes later; the fan-out is the thing under test.
