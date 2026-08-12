# Azimuth demo

Development repo for the Azimuth framework and the ride-hailing fixture used to develop it.
Self-contained by design: tooling, skills, specs and app live here together so that a change to
the notation and a change to the corpus land in one commit. Extraction and release come after the
concepts stabilize.

The alpha at `drim-dev/azimuth` is **frozen**. All development happens here.

`AGENTS.md` is the single source for repository instructions. Codex reads it directly; Claude Code
loads it through the one-line `CLAUDE.md` import. Skills use only the shared Agent Skills fields
(`name` and `description`) and live canonically under `.agents/skills/`; Claude Code reaches the
same skills through symlinks under `.claude/skills/`.

## Orientation

| Path | Holds |
|---|---|
| `AGENTS.md` | canonical repository instructions for coding agents |
| `.agents/skills/` | canonical Agent Skills; `.claude/skills/` holds compatibility symlinks |
| `docs/framework.md` | what the framework *is*, as it currently stands. Derived; start here. |
| `docs/decisions.md` | decisions D1–D42 and their rationale. Authoritative. |
| `docs/concern-catalog.md` | 18 cross-cutting concerns from the domain; the evidence the design was drawn from |
| `docs/glossary.md` | bounded definitions. Authoritative for terminology. |
| `docs/status.md` | the decisions checked against their own falsifiers, including failures. |
| `docs/change-process.md` | operating guidance for change delivery, evidence work and rollout |
| `docs/assurance-extensions.md` | how external test and analysis tools bind to the core |
| `azimuth/README.md` | artifact layout and model-package discovery contract |
| `azimuth/model/` | accepted intent, mechanism, evidence and judgment packages |
| `azimuth/formats/` | parser contracts for the three facets and agent judgments |
| `azimuth/standards/verification.md` | evidence required by criticality |
| `azimuth/changes/` | experimental current-to-target change lifecycle and archive |
| `azimuth/explorations/` | non-normative research and decisions above one or more changes |
| `tools/azimuth/README.md` | what the tool checks today, and what it does not check yet |
| `experiments/multirepo/` | executable federation hypotheses, fault matrix and replication protocol |
| `experiments/polyglot/` | executable Go/JVM/Python/JavaScript/Rust/C++ extractor conformance |
| `services/assurance/` | optional Rust/PostgreSQL execution ledger and Next.js diagnostic client |

The three facets are sibling `spec.md`, `design.md` and `verification.md` files under a package in
`azimuth/model/`; `judgments.md` holds the agent tier. Criticality decides which files a claim owes
(D6.5, D20): a routine claim deliberately stops at intent and owes no tags. An untagged test is
ordinary project evidence outside Azimuth's model, not an exemption or a hole. The project-level
`azimuth/standards/verification.md` answers what evidence a standard or critical claim requires.

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
- **A local project check is not a complete check.** In a federated project, report missing workset
  inputs and never finalize from `--local` output. Repository, mount and path are locators; source
  identity is the declared area plus typed address (D33).
- **Model authority follows intent, not checkout location.** Do not copy a system-owned spec into a
  frontend model source merely because frontend code realizes it. A spec has one model-source
  authority; a change and its realization may span repositories.
- **Change authority is singular in a complete project account.** Repository observations enumerate
  tracked active and archived changes. Do not create a local proposal for a work package; complete
  assembly rejects a change id observed under more than one repository authority.
- **Exploration precedes commitment.** Put multi-change research and user-owned decisions under
  `azimuth/explorations/`; do not let an exploration assert current truth or hide inside its first
  change. Use `azimuth-explore` when the direction is materially uncertain.
- **Agent teams follow the work-package DAG.** Validate `work-packages.md` before delegation.
  Shared contracts and change artifacts stay coordinator-owned; workers edit only their declared
  non-overlapping paths and never finalize or archive.

## Commits

Subject in the imperative, scoped (`docs:`, `specs:`, `tools:`). The body says what changed and
why, and records findings the change surfaced — including inconvenient ones. Commit history is
part of the record here; a bare "update docs" throws away the reasoning.

## Open questions

Live in `docs/decisions.md`. They are open because they need evidence from the fixture, not
because they need more argument. Do not close one by reasoning alone.
