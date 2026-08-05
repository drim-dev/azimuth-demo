# Verification plans

The evidence facet (D3). A plan records **what would be sufficient to believe a claim** — never
what currently exists. The evidence that exists is derived from `covers` tags and appears in the
export; hand-listing it would create a second copy that drifts (D4.5).

## What a plan contains

Only what is not derivable:

1. **Deviations from the project standard** — a claim needing stronger or different evidence than
   its criticality implies.
2. **Non-test evidence** — proof by construction, monitors, manual passes, attestations. Nothing
   in the code says "this unique index is the evidence for that claim".
3. **Residual risk** — what is knowingly not covered, and why that is acceptable.

**A claim with no entry is not unplanned.** It means the project standard applies unmodified.
Most claims should have no entry; if most have one, either the standard is wrong or the plan is
being used as an inventory.

## Files

```markdown
# Verification: <spec-id>
```

One plan per spec, declaring the spec id it covers. As with specs, the id is declared and the
path is convention (`verification/trip/dispatch.md`). A spec needing no deviations, carrying no
non-test evidence and accepting no residual needs **no plan file at all**.

## Entries

```markdown
## Claim: <scenario-id>
Scope: unit | component | e2e
Quantification: example | invariant
Oracle: direct | golden | metamorphic | model-based | contract

Prose stating why this claim needs what it needs. Required — an entry without a reason is a
number nobody can review.
```

Labels first, then a blank line, then prose. Values may wrap: inside the label block, a line that
begins no known label continues the previous one. Every field is optional; an entry states only
what it changes. `Oracle` is descriptive and never gated.

**Two field groups that are easy to confuse, and are therefore kept apart:**

- `Scope`, `Quantification` and `Oracle` state the **required** form, overriding the standard.
- `Evidence` and its `Strength` declare a **provided** non-test evidence item.

`Strength` without `Evidence` is an error, because on its own it reads as either.

### Ladders

`Scope` and `Quantification` are ladders: `unit < component < e2e`, `example < invariant`.
Strength is a ladder too: `detection < demonstration < proof`. **A stronger form on any axis
satisfies a requirement for a weaker one.** A required form is a floor, not a target.

Scope is defined by what must be *real* (D15) and applies to demonstration-strength evidence
only. Proof has no scope; detection has a target.

### Non-test evidence

```markdown
## Claim: <scenario-id>
Strength: proof
Evidence: partial unique index `ux_capture_trip` on `captures(trip_id)`

Violation is unrepresentable at the storage layer, so no execution can exhibit it.
```

For detection-strength items, two further fields are required (D4.3):

```markdown
Re-established: continuously | every release | quarterly
Dies silently: <how this stops being evidence without anyone noticing>
Detector test: <the test proving it fires on an injected violation>
```

A detection item without a detector test is a hole. A monitor that can no longer fire is worse
than no monitor, because it is carried on the books as evidence.

### Lowering a requirement

A plan may require *less* than the project standard, but only with an accepted residual:

```markdown
## Claim: <scenario-id>
Quantification: example
Residual: not checked across all currencies
Accepted: single-currency market until the second market launches; revisit then
```

Silent weakening is not available. This is D6.3's exemption principle applied to evidence: a
deliberate, attributable, reviewable opt-out is fine anywhere; an unrecorded absence is not.

### Spec-level residual

```markdown
## Residual: <short-id>
Accepted: <why, and under what condition it is revisited>

<what is not covered>
```

For risks that belong to no single claim — typically a cross-cutting concern held as prose while
the steel thread runs. Labels come first here too: the grammar is uniform, so prose above a label
is a parse error rather than something silently swallowed.

## What never appears here

- **Test names as evidence for a claim.** Derived from `covers` tags.
- **The actual scope or quantification of an existing test.** Declared by the tag; the plan states
  what is *required*. `wrong-form` is the comparison.
- **Restatement of the claim.** The spec owns the predicate.
- **Anything true of every claim.** That belongs in `standards.md`.
