# The quantification field, reviewed

**Status: three observations. The proposal that was here is decided as D19** *(revised — the file
opened "one proposal and three observations; nothing here is decided", which stopped being true when
the rename landed)*. Prompted by the agent-tier pass over `specs/trips/request.md`, which returned
four `dishonest-tag` verdicts out of eight claims, every one of them on `Quantification`.

Sections 1 and 2 are the argument D19 rests on and name the *old* value throughout, deliberately:
they are about the word `invariant`, not written in it.

## 1. Prior art, conceded

The *distinction* `example` / `invariant` draws is thoroughly established. The *pair of words* is
not, and `docs/framework.md`'s prior-art section does not currently name the field that owns it.

**Established, and this is the same cut:**

- **Example-based vs property-based testing.** QuickCheck (Claessen & Hughes, ICFP 2000) and its
  descendants — Hypothesis, fast-check, FsCheck. The standard antonym pair is `example` / `property`
  (or `generative`), never `example` / `invariant`. Stated in almost the framework's own terms:
  example-based tests use a concrete scenario to suggest a general claim; property-based tests
  address the general claim directly.
- **Criticality → required evidence.** DO-178C maps DAL A–E to required verification objectives;
  assurance cases (GSN, ISO/IEC 15026-2) record structured argument from claim to evidence. This is
  `verification/standards.md`'s ancestry, and it is stronger prior art than anything supporting the
  quantification field itself.
- **Oracle taxonomies.** Barr et al.'s oracle survey (IEEE TSE 2015) gives specified / derived /
  pseudo / metamorphic oracles, which the `Oracle` field maps onto closely. The `Oracle` field is
  better anchored in the literature than `Quantification` is.

**Not established:** no standard uses a two-valued evidence field named `example | invariant`. ISTQB
and ISO/IEC/IEEE 29119 quantify thoroughness as *coverage* — "the degree to which specified coverage
items are exercised by a test suite, expressed as a percentage" ([ISTQB
glossary](https://glossary.istqb.org/en_US/term/coverage)) — which is continuous over enumerated
items and attaches to a suite, not a two-valued category attaching to a claim's evidence.

## 2. The borrowed term points at the wrong thing

A Floyd loop invariant or a [Meyer class
invariant](https://se.inf.ethz.ch/~meyer/publications/computer/contract.pdf) is a predicate that
holds of the **system** across states. This framework's `invariant` says nothing about the system:
it reports how widely the **evidence** ranged. Those are different categories, and the word points
at the first one.

`docs/glossary.md:82` already flags a collision, but only the internal one — the alpha's use of
"invariant" for cross-cutting rules. The external collision is unrecorded, and it is the one a
reader from the formal-methods literature hits first. It bites harder because D13 establishes that
every claim is ∀ over its domain, so "invariant" cannot be distinguishing anything on the claim side
at all.

This is the liability `AGENTS.md` names for borrowed terms: precision the mechanism does not back.

## 3. The rename — decided as D19 *(graduated out)*

The proposal that stood here is decided: the field is `Quantification: example | universal`, and
`invariant` is no longer a value. The argument, the rejected alternative (`property`), the recorded
objection and the falsifier live in [`docs/decisions.md`](../docs/decisions.md) under **D19**, which
is authoritative. Per `misc/README.md`, the copy is not kept here to disagree with it.

## 4. What the field buys, and what it does not

The practical case for having the field at all, since "test critical things harder" is a policy that
`standards.md` already states in one place.

**Buys:**

- **A baseline for a deviation to be recorded against.** `Residual:` needs something to be residual
  *to*. `verification/trips/dispatch.md` can write "checked at a single boundary instant, not across
  the range of clock skew" only because a stated form exists to fall short of. Without the field
  that gap is untyped prose, or nothing.
- **A checkable sentence form.** "This test isn't thorough enough" is an opinion. "The tag says
  `invariant`; the test fixes every input and asserts `Be(1500)`" is a fact a reader can confirm
  without agreeing with the judge about anything. All five outstanding `dishonest-tag` verdicts have
  that shape, including `trips/rider-view`'s, where the checkable fact is that the test names five
  URLs while the claim's membership is derived.
- **Gating in both directions.** `payments/capture#declined-capture-recorded` is tagged `Example`,
  judged `sound`, and the judgment approves the tag. The field is how a project says *this is
  enough*; without a floor, "more thorough" has no stopping point.

**Does not buy: detection.** The machine tier reported green on every dishonest tag in all three
passes. Each was found by an agent reading test bodies, and an agent with no field at all would have
reached the same verdicts on the same tests. The field makes the resulting judgment recordable,
comparable and machine-expirable. It does not find anything.

## 5. The uncomfortable ledger

Quantification is the field with the highest observed dishonesty rate in the repo.

What the passes *found*, not the state after fixing — the dishonesty rate is the number this section
is about, and repairing it does not unmake the observation.

| Spec | Claims judged | Tag failures | Other adverse | Since fixed |
|---|---|---|---|---|
| `trips/request` | 8 | 4 | 2 toothless | all 6, re-judged `sound` 2026-08-07 |
| `payments/capture` | 10 | 2 | 1 toothless | the 2 tag failures |
| `trips/rider-view` | 9 | 1 | 3 toothless · 2 spec-gap | none |
| **Total** | **27** | **7** | **6 toothless · 2 spec-gap · 12 sound** | **8 of 15** |

Every tag failure across all three passes was `Quantification`, or the `Oracle` beside it — the
pattern held for a third spec, on evidence written months apart. D18.1 already drew the general
form: **a standard that is expensive to satisfy honestly is cheap to satisfy dishonestly.**

The `trips/rider-view` pass is the first to produce `spec-gap` verdicts, and they came from a
different place: `design/` named a mechanism and a failure mode that no scenario describes, so no
evidence was ever required for it. That is the mechanism facet doing the job D3 claims for it — weak
evidence, one spec, but it is the first time the second facet has caught something the other two
could not.

That cuts two ways and the demo has not decided which. Either the field is doing its job — surfacing
a real, repeated, otherwise-invisible failure — or it is a tax whose principal product is its own
violations. `docs/framework.md:271` carries this as an open falsifier: *"artifact and annotation
cost exceeds what the defects justify → ceremony | never measured"*. Still never measured.

**The test that would settle it:** whether a `dishonest-tag` finding ever leads to a rewrite that
catches a defect the example-tagged version would have missed.

**Still not satisfied, and the second data point is more interesting than the first** *(updated
2026-08-07)*. `payments/capture#capture-equals-trip-fare` was rewritten to 36 generated amounts and
found nothing — the implementation was already correct. The `trips/request` rewrite went further:
three faults were *injected* and the old evidence survived all three while the new evidence killed
all three, including the grace window the judgment had named in advance.

That is fault-detection capability, measured, which is the mutation-testing currency and is worth
having. It is **not** a defect found, which is what this test asks for and what the ceremony
falsifier trades in. Nobody has yet been surprised by a bug because a tag was made honest. Until
that happens the field's demonstrated yield remains bookkeeping about evidence — better-founded
bookkeeping than a week ago, and still bookkeeping.

## 6. Observation — the field is one bit and evidence is multi-axis

Real evidence is universal along some axes and instantiated along others.
`A_completed_trip_is_captured_for_whatever_its_fare_is` ranges over amount and currency and fixes
trip shape, dispatch timing and storage state. It is tagged `Universal` and that is fair — but
*which* axis was ranged over is recorded nowhere except judgment prose, which the machine tier
cannot read.

### 6.1 Two questions that are not the same

An earlier draft of this section ran them together. They separate cleanly:

- **Which mechanism?** Derived enumeration, generation, or repeated contention — the three shapes
  D19 admits. A fact about *method*.
- **Which axis?** Which free variables of the WHEN were varied and which were fixed. A fact about
  *what the evidence establishes*.

Only the second is a gap in what the model records. Naming the method answers the lesser question:
evidence can be generated over ten thousand cases along an axis the claim does not quantify over,
which is the wrong-axis failure `azimuth-cover` predicts.

### 6.2 Why a field is the wrong shape for it

**The axis is already in the model.** It is the quantifier phrase in the WHEN — "any further ride
request", "has reached a terminal state", "delivered more than once" — and `specs/README.md` already
requires the WHEN to quantify rather than instantiate. A field on the evidence declaring the axis
would be a second copy of what the spec states, which is the drift `verification/README.md` refuses
for evidence lists (D4.5).

**What is missing is a comparison, not a fact.** Did the test's variation match the claim's
quantifier? That is the shape of `wrong-form`: computed from two artifacts, not declared in one.
Comparisons are checks.

### 6.3 An asymmetry that constrains any future proposal

For claims over an **enumerated** domain — the `## Invariant:` construct, domain a set of sites —
the model already has member granularity: `invariant-breach` reports a member that discharges
nothing. For claims over the **default** domain, executions cannot be enumerated at all, ever. What
is finite and nameable there is the set of free variables in the WHEN. So any notation would be
specific to the execution domain and would name variables, not cases — a narrow thing, not a general
field.

### 6.4 The option space, ranked

1. Nothing; it stays prose in the residue, which is what `AGENTS.md` prescribes for a singleton.
2. A rubric item in the agent tier — already present as `azimuth-cover`'s third self-check.
3. A descriptive, never-gated field, on the `Oracle` precedent. Answers 6.1's lesser question.
4. A new gated field. No: it would need a check to consume it, and the only candidate check is (2).

### 6.5 A prediction was made and did not hold *(recorded)*

This section predicted that `trips/rider-view` would supply the second structurally different
concern, on the grounds that it is the one spec whose claims range over sites. **It did not.** The
pass found the site-class claim's tag dishonest for a different reason — the test hand-lists five
URLs where membership is derived from what the code built — which is about *who enumerates the
domain* (D13.1, D13.2), not about which axis of a multi-axis space was ranged over. Adjacent, not
the same. See `site-class-evidence.md` for what it did find.

So the axis question stands at **one instance after three specs and 27 judged claims**, and stays
prose. Recorded because a prediction that fails is worth more than one that was never checkable, and
because the temptation after two judging passes is to promote an argument that has only got better.

## 7. Related work from the same session

`.agents/skills/azimuth-cover/` — an authoring skill that attacks the cost gradient directly: name
the axis the claim quantifies over, range over that one, and when the required form is unavailable,
tag what the test *is* and record the deviation in the plan. It carries its own falsifier and the
baseline above.

**Predicted failure mode, recorded as a prediction:** teaching the shapes of universal evidence may
produce generated-looking tests that range over the wrong axis — harder to catch than `Be(1500)`,
not easier. No judgment has recorded one yet.
