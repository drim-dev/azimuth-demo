# Design

The mechanism facet (D3): **what makes a claim true, and how strongly.**

Design documents rot as a rule. Two things are meant to prevent it here, and they are the whole
justification for the format:

1. **Nothing structural is written.** "The trip service calls the payments client" is derivable
   from the code and the `realizes` tags. If a machine could produce a line, the line does not
   belong here.
2. **Every entry is a falsifiable assertion about a named artifact.** An entry claims that a
   specific index, type, or function is what holds a claim up. When the code stops matching, that
   is a **hole**, not stale prose. This is what design documents have never had, and it is why
   this one can be checked instead of believed.

Required for `critical` requirements, optional for `standard`, absent for `routine` (D6.5).

This directory describes **accepted current mechanisms**. Proposed architecture, alternatives and
implementation sequencing live in `changes/<id>/design.md` (D21.2), where a planned mechanism may
honestly be absent. Archiving distils only what was built into this directory; copying an abandoned
plan here would turn change history into design fiction.

## Files

```markdown
# Design: <spec-id>
```

One file per spec, declaring the spec id. Path is convention, id is identity.

## Entries key on the requirement, not the scenario

Verification entries key on scenarios because the scenario is the unit of coverage. Mechanism
entries key on requirements because that is usually where the mechanism operates: one unique
index makes all three `captured-once` scenarios true, and recording it three times would be the
duplication this framework exists to prevent.

The claim tree has two levels, and **a facet attaches at the coarsest level where its statement is
true.** An entry may key on a scenario where the mechanism genuinely differs per scenario.

## Entries

```markdown
## Requirement: <requirement-id>
Enforcement: <kind>
Binding: <machine-addressable artifact id emitted by a compiler or schema extractor>
Expect: <optional derived properties that must match, such as uniqueness, columns or predicate>

Prose: why this mechanism, what was rejected and why, and what breaks if it changes. Required.
An entry that states a mechanism without a reason records a fact the code already knows.
```

A requirement may carry several `Enforcement`/`Binding` pairs, in order, where more than one
mechanism holds it up. C2 in the concern catalog is the worked example: a choke point *and* a
representation constraint, for one rule.

### Enforcement kinds

The closed set, ordered by D7's ladder:

| Rung | Kind | Violation is | Derived strength |
|---|---|---|---|
| 1 | `type` | unrepresentable in the type system | proof |
| 1 | `schema` | unrepresentable in the data schema | proof |
| 2 | `constraint` | rejected by storage — unique index, FK, check, RLS | proof |
| 2 | `choke-point` | only possible through one place | proof |
| 3 | `middleware` | prevented where applied, and application is opt-in | demonstration required |
| 4 | `guard` | checked at each site | demonstration required |

**Strength is never written.** It is derived from the kind (D7): the top two rungs *are*
proof-strength evidence, which is why a claim enforced at rung 1 or 2 can carry a weaker evidence
requirement without that being a bargain. Writing it would duplicate a derivable fact.

### What `Binding` must be

`Binding:` is structural and exact; the paragraph below it carries the human explanation. A
symbol binding establishes that the symbol exists, not that words such as “only” or “every” are
true. A schema binding may additionally carry an `Expect:` line because uniqueness, columns and a
predicate are derivable from migration metadata and can therefore be compared exactly.

```markdown
Enforcement: constraint
Binding: postgres-index:trips.ux_trip_quote
Expect: unique=true; columns=quote_id
```

Precise enough for the check to be mechanical:

| Kind | `Binding` names | The check asks |
|---|---|---|
| `type` | the type claimed to make a value impossible | does the compiled type exist and is the binding a type rather than a method |
| `schema` | the schema element | was that exact schema artifact emitted |
| `constraint` | the constraint by name and table | does it exist in the migrations, with that uniqueness or predicate |
| `choke-point` | the function or module claimed to be singular | does the compiled operation exist and is the binding an operation rather than a type |
| `middleware` | the registration claimed to cover a surface | does the named artifact exist |
| `guard` | the operation claimed to check a rule | does the named artifact exist |

Only migration-derived index properties are checked semantically in the current machine tier.
Symbol existence prevents fiction and some category errors; it does not establish “only caller,”
absence of an escape hatch, complete middleware coverage or correct guard logic. Those assertions
remain part of the agent judgment until a purpose-built analyzer can derive them. Middleware and
guard coverage also need a sound enumerator (D13.1); a hand-listed set is worse than no check.

## Residue

```markdown
## Residue
Free prose. Never parsed. Never derived.
```

Orientation, danger zones, deliberately broken corners, what is absent and why. This attaches to
no claim, participates in no check, and is the durable half — no reflection recovers "this lost
update is intentional, not a bug."

**Not the same as a verification residual.** A residual in `verification/` records evidence that
is missing. Residue here records judgment that cannot be derived. The first is a gap; the second
is knowledge.

Marking it explicitly is what stops the design file becoming a dumping ground: anything that is
neither a checkable mechanism claim nor deliberate judgment does not belong in either section.
