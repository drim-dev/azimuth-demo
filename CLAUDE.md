# Azimuth demo

Development repo for the Azimuth framework and the ride-hailing fixture used to develop it.
Self-contained by design: tooling, skills, specs and app live here together so that a change to
the notation and a change to the corpus land in one commit. Extraction and release come after the
concepts stabilize.

The alpha at `drim-dev/azimuth` is **frozen**. All development happens here.

## Orientation

| Path | Holds |
|---|---|
| `docs/framework.md` | what the framework *is*, as it currently stands. Derived; start here. |
| `docs/decisions.md` | numbered decisions D1–D19, with rationale and consequences. Authoritative. |
| `docs/concern-catalog.md` | 18 cross-cutting concerns from the domain; the evidence the design was drawn from |
| `docs/glossary.md` | bounded definitions. Authoritative for terminology. |
| `docs/status.md` | the decisions checked against their own falsifiers. Two have fired. |
| `specs/` | the intent facet. `specs/README.md` is the parser contract. |
| `design/` | the mechanism facet (D3). `design/README.md` is the format and the enforcement ladder. |
| `verification/` | the evidence facet (D3). `verification/standards.md` maps criticality to required evidence. |
| `tools/azimuth/README.md` | what the tool checks today, and what it does not check yet |

The three facets are `specs/`, `design/` and `verification/` — a claim is incompletely described
until you have looked at all three (D3). `verification/standards.md` in particular answers what
evidence a claim's criticality already requires, which is what decides whether a new test needs a
`covers` tag or an exemption.

Read `docs/decisions.md` before proposing anything structural. Most questions that look open have
been decided, and several have been decided *and revised* — the revision history is deliberate and
visible. `docs/framework.md` is derived from it and never overrides it.

## Writing

Documents here are written to be argued with. The register is scientific, and that is a working
constraint, not a preference.

- **State claims as propositions, not aspirations.** "Criticality attaches to requirements, not to
  code locations" — something that could be wrong. Not "we care deeply about rigor".
- **Every assertion is derived, cited, or marked as a prediction.** If it rests on a decision, name
  it (D7, D13.1). If it is a guess, say so; the concern catalog says outright that it is a
  prediction and should be treated as one.
- **Say what would falsify it.** Claims that cannot fail are not claims. Falsifiers are recorded
  *before* the evidence exists, which is the point.
- **Prefer "X, because Y" to "X is important."** If the because is missing, the claim is not ready.
- **Quantities over adjectives** wherever a quantity exists. "11 of 21 requirements at top
  criticality, against a 40% falsifier" beats "quite a lot".
- **No marketing register.** No powerful, seamless, robust, best-in-class, game-changing.
- **Distinguish decided / proposed / open explicitly.** Never let a proposal read as settled.
- **Mark revisions; do not silently rewrite.** Use `*(revised)*`, `*(supersedes …)*`,
  `*(closes …)*`, and keep enough of the earlier reasoning that a reader can see why it changed.
- **`azimuth` is the tool; `rtm` is one check.** Never "run rtm", never the matrix as the product.
  Commands are `azimuth check`, `azimuth check rtm`, `azimuth export`. The old name pulls design
  discussion back toward the matrix, which is precisely why D9 renamed it — and it pulls hard
  enough to catch someone who has read D9.
- **Terminology is bounded by the glossary.** Where a borrowed term is used more narrowly than its
  origin — `proof` above all — say so at the point of use. Precision that the mechanism does not
  back is a liability: the first rigorous reader who pushes on it costs more credibility than the
  term ever bought.
- **Concede prior art.** Traceability matrices, assurance cases, architecture conformance checking
  and mutation testing all overlap this work. Claim novelty only where it survives contact with
  that literature, and narrowly.
- Wrap prose at 100 columns. Diffs are a review surface.

## Working rules

These are the ones that are easy to get wrong.

- **Evidence before notation.** No mechanism enters the model until ≥2 structurally different
  concerns demand it, written as prose first. Singletons stay prose in the residue.
- **The steel thread carries no cross-cutting notation at all.** The eighteen concerns are held as
  prose deliberately. What the per-scenario matrix *misses* is the evidence for what to add next.
- **Tooling never references the demo domain.** No ride-hailing vocabulary, no fixture paths, no
  special cases in `tools/`. The tool's own tests use synthetic fixtures, never the demo specs.
- **Specs are organized by domain area, not by service.** Mirroring services duplicates every
  cross-component claim and destroys the fan-out the demo exists to study.
- **Ids are declared, never derived from paths.** Moving a file breaks nothing. Scenario ids are
  unique per spec, so splitting a requirement touches no tags.
- **Derive the derivable.** Anything a machine could produce from the other artifacts plus the code
  does not get hand-written. This is what keeps three artifacts from becoming three rot sites.
- **No backward compatibility this phase.** Schema changes are hand-edits across the repo. There is
  one consumer. Do not build migrations.
- **Every feature of the fixture must be justified by a concern or hard case it instantiates.** The
  failure mode of this phase is the fixture becoming the product.

## Commits

Subject in the imperative, scoped (`docs:`, `specs:`, `tools:`). The body says what changed and
why, and records findings the change surfaced — including inconvenient ones. Commit history is
part of the record here; a bare "update docs" throws away the reasoning.

## Open questions

Live in `docs/decisions.md`. They are open because they need evidence from the fixture, not
because they need more argument. Do not close one by reasoning alone.
