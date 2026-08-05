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
Site: <the specific artifact, named precisely enough to check>

Prose: why this mechanism, what was rejected and why, and what breaks if it changes. Required.
An entry that states a mechanism without a reason records a fact the code already knows.
```

A requirement may carry several `Enforcement`/`Site` pairs, in order, where more than one
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

### What `Site` must be

Precise enough for the check to be mechanical:

| Kind | `Site` names | The check asks |
|---|---|---|
| `type` | the type and the property it makes impossible | does the type exist, and does it lack the escape hatch |
| `schema` | the schema element | is the field absent, or the shape enforced |
| `constraint` | the constraint by name and table | does it exist in the migrations, with that uniqueness or predicate |
| `choke-point` | the single function or module | is it the only caller of the mutation |
| `middleware` | the registration and what it covers | is every member of the covered set actually registered |
| `guard` | the sites | do all of them discharge it |

The last two rows are where the machine tier gets weakest and the enumerator problem (D13.1)
appears: checking `middleware` or `guard` means enumerating a set, and a hand-listed set is worse
than no check.

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
