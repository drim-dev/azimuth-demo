# Languages that force precision, and what none of them force

**Status: reference notes, plus one proposal for `docs/framework.md`.** Citations are from model
knowledge and were not verified; check before any of this moves into `docs/`.

## The tiers

Ordered by how much the language compels.

| Tier | Instances | What it forces |
|---|---|---|
| Contracts as syntax | Eiffel, Ada 2012 (`Pre`/`Post`/`Type_Invariant`), D, Clojure `:pre`/`:post` | State the precondition, postcondition and invariant. Checked at runtime; nothing forces them to be non-trivial. |
| Verification-aware | Dafny, SPARK, Why3, Frama-C/ACSL, Verus and Creusot for Rust | It does not compile unless a solver discharges the contracts, loop invariants and termination measures. |
| Proof assistants | Rocq (Coq), Lean 4, Agda, Idris, F\* | The proposition *is* the type. No gap between claim and evidence, because they are the same object. |
| Specification languages | TLA+, Alloy | State the invariant; the checker returns a counterexample. The closest thing in tooling to "say what would falsify it". |

## Two connections to this framework

**Rust's borrow checker is `proof` strength in the glossary's exact sense** — not "someone proved
it" but *violation is unrepresentable*. Same category as the partial unique index in
`app/services/Trips/Database/Configurations/TripConfiguration.cs`. Typestate and session types
generalize it: make the illegal state unconstructable and no execution remains to sample. This is
D7's "strong enforcement is self-evidencing" arriving from the type-system side.

**Property-based testing forces the `example` → `universal` shift by construction.** There is no
expected value to hard-code, because the input is not known when the test is written. That is the
same mechanic `.claude/skills/azimuth-cover/SKILL.md` states as its first self-check.

## What none of them force

They compel *precision*, not *falsifiability*. `ensures true` verifies. An Alloy predicate can be
satisfied trivially. A TLA+ invariant can hold because the modelled state space is smaller than the
deployed one.

Model checking has a name and a literature for this — **vacuity detection** (Beer, Ben-David, Eisner
& Rodeh, CAV 1997; Kupferman & Vardi thereafter). The failure it describes is `toothless` one level
up: a specification that passes because it asserts nothing.

### Proposal — concede vacuity detection as prior art *(proposed, not decided)*

`docs/framework.md:277` concedes traceability matrices, assurance cases, architecture conformance
checking and mutation testing. **Vacuity detection belongs on that list**, and it is the closest
prior art to the `toothless` verdict specifically — closer than mutation testing, which measures
whether a test suite discriminates rather than whether a specification asserts anything.

The finding it supports is worth stating in its own right: the formal-methods world already ran this
experiment. A machine tier that checks whether claims are *consistent* cannot check whether they are
*worth making*, and that gap does not close with a stronger solver. D18's argument for the agent
tier arrives at the same place from a different direction, and conceding the overlap costs nothing
that the argument needs.

## Where the falsifier discipline actually comes from

Not from programming languages. No compiler asks what would change your mind. The habits — state the
falsifier before the evidence exists, mark predictions as predictions, concede prior art narrowly,
distinguish decided from proposed — come from experimental science and from safety-case practice
(GSN, DO-178C, ISO 26262 hazard analysis). See `reading-path.md`.

## Relevance to verifying agent-written code

The tier that matters is the second. An agent writing Dafny or SPARK cannot misdescribe whether the
postcondition holds — only whether the postcondition is worth anything. That is exactly the machine
tier / agent tier split, enforced by a solver rather than by a self-declared tag, and it leaves the
identical residue for a judge.

Which suggests the sharpest available strengthening of the machine tier is not more checks over
tags, but moving claims into positions where violation is unrepresentable. That is D7's rung ladder,
and it is already the framework's stated preference. Recorded here as an observation, not a
proposal: nothing in the fixture currently tests whether that preference survives contact with a
domain where the top rungs are unavailable.
