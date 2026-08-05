# Framework Decisions

Status: working document for the redesign phase. Everything here is decided **for this phase**
and may be reopened by evidence from the demo. Entries marked *open* are deliberately unresolved.

Companion: [`concern-catalog.md`](./concern-catalog.md) — the domain evidence these decisions were
drawn from. Concern references below (C1–C18) point there.

Date: 2026-08-05

---

## 0. What this phase is

The alpha framework's core ideas — enumerated specs with stable ids, code tagging, a derived
matrix — are sound. The mechanisms built around cross-cutting concerns are not; they were made
in haste and encode one enforcement strategy as if it were the only one.

This phase redesigns those mechanisms **from the application inward**: build a demo system,
meet real concerns, design artifacts that fit them. Notation is the last step, not the first.

---

## 0.1 The core, and what is layered over it

Five primitives carry the framework:

1. **Claims** — stable ids, criticality, and the domain they range over (D13)
2. **Tags** — `realizes` and `covers`, binding code and tests to claims
3. **Evidence** — with strength and freshness (D4)
4. **The derived model** over the above, exported (D10)
5. **Changes** — the unit in which all of it moves (D11)

Everything else in this document — three artifacts, roles, the enforcement ladder, the domain
taxonomy, criticality levels — is **structure layered over those five**, and must be presented,
adopted and implemented that way.

This is not presentation advice. It is the operational form of D8: if a layer cannot be removed
without breaking the core, the layering is wrong. A framework that reads as fifteen coordinate
concepts will not be adopted; the same framework as five primitives plus optional layers will.

---

## D1 — The demo is a fixture, not a product

**Decision.** Build a ride-hailing system (Uber-shaped) as the corpus for developing and
validating the framework.

**Why this domain.** It produces the hard cases organically rather than by contrivance:

- A genuine multi-hop fan-out — rider app → rider BFF → trip service → driver BFF → driver app.
  One scenario realized at five sites across three languages.
- Cross-cutting concerns whose surfaces grow on their own: location privacy, PII in the
  warehouse, authorization on every trip-scoped read, idempotency on every mutating route.
- Two questions a monolith cannot ask: what realizes a scenario across a message broker, and
  what `scope: component` means when a "component" may be a service, a service plus its BFF, or
  a contract between them.

**Scale is capped.** Shape diversity is the goal; size adds nothing.

- 3–4 services: trip, driver, pricing/payments, analytics consumer
- 2 BFFs — rider and driver (the asymmetry is where fan-out bugs live; non-negotiable)
- 1 web app, 1 mobile client
- 1 analytics pipeline

**Explicitly not built.** A second native mobile app; service-mesh/k8s/event-bus
infrastructure; service count beyond what the concerns need. These cost months and teach the
artifact model nothing. Per-language extractor coverage is obtained from small synthetic sample
projects, not from more app.

**Sequencing.** A steel thread first — request → match → accept → complete → charge, end to end
through every layer — before any breadth. The fan-out is the thing under test; it should exist
in week one.

**Feature selection rule.** Every feature must be justified by a concern or hard case it
instantiates. The failure mode of this phase is the fixture becoming the product.

**Deliberately included.** Features whose spec starts *wrong* and must be revised — surge
pricing is the chosen one. Requirement split/merge is where the id model is expected to crack,
and it needs to be exercised, not avoided.

---

## D2 — The repo is self-contained

**Decision.** Tooling, skills, scripts, specs and app all live in this repo. Extraction and
release happen after the concepts stabilize.

**Why.** The loop that matters is: change the notation → regenerate over real code → judge
whether the hole is real — in one commit. A separate tool repo inserts a publish/consume cycle
into every experiment. Additionally, the **agent tier is a skill**: keeping machine tier and
agent tier beside the fixture means a change to the verify-pass prompt and a change to the
matrix schema land together against one corpus.

**The rule that keeps extraction cheap.** Tooling never references the demo domain. No
ride-hailing vocabulary, no fixture paths, no special cases. The app depends on the annotations
package; nothing depends on the app. Enforced by a check in CI — and this rule is itself a
code-shape rule (C16's shape), so the repo dogfoods the catalog on itself. Failure to express it
in Azimuth's own terms is an early signal about the code-shape artifact.

**Corollary.** The tool's own tests use synthetic fixtures, never the demo specs. The moment
`azimuth`'s test suite asserts against real demo content, the two are welded together.

**Layout.**

```
tools/azimuth/     core: spec reader, model derivation, checks, CLI. No domain knowledge.
tools/extractors/  per-language tag extraction → common manifest JSON
packages/          annotations, per language, published-shaped from day one
app/               the demo — services, BFFs, web, mobile
specs/             specs, designs, verification plans
docs/              catalog, decisions, design notes
.claude/skills/    agent tier: verify pass, spec authoring, tagging
scripts/
```

**Core architecture.** Each ecosystem emits a manifest natively; the core only reads manifests.
This is the seam extraction will follow, and it makes adding a language a day's work rather
than a fork of the core. The alpha's `Azimuth.Manifest` already implies this shape — keep it.

### D2.1 — `drim/azimuth` is frozen

All development happens here. The alpha repo gets a README note pointing at this one. Avoids
merging two divergent designs later.

### D2.2 — Port selectively

Bring over: stable ids, scenario-as-unit, `covers`/`realizes`, matrix derivation, the hole
taxonomy for the per-scenario axes.

Deliberately **do not** port `invariant` / `class` / `guard`. Cross-cutting mechanisms come back
from the concern catalog. Porting them would make them the default by inertia.

### D2.3 — No backward compatibility this phase

No semver, no migrations. Schema changes are hand-edits across the whole repo, which is
affordable because there is exactly one consumer. Nobody should spend effort on compatibility
that has no users.

---

## D3 — Three artifacts, one per accountable role

**Decision.** The framework is built around three artifacts, each owned by a role that is
accountable for it. Any role may read and modify any artifact; exactly one is answerable for
each.

| Role | Artifact | Accountable for |
|---|---|---|
| Analyst | **Spec** | What must be true, and how much it matters |
| Developer | **Design** | How it is built and how rules are enforced |
| QA | **Verification plan** | Whether the evidence is sufficient to believe it |

**Why roles survive the agent era.** Not division of labour — agents genuinely erode that — but
**accountability**, which cannot be delegated to something that cannot be held to account. This
premise survives arbitrarily capable agents, which is the test of a load-bearing framework
assumption.

**Why this specific split.** It was reached twice from opposite directions. Bottom-up from the
concern catalog, the sharpest finding was that *enforcement and verification must be separate
fields*. Top-down from accountability, developer and QA are separate accountable parties. Same
seam, found independently.

**The rule that keeps three artifacts from becoming three rot sites.** Each artifact contains
**only what is not derivable from the other two plus the code**. For any line, ask whether a
machine could derive it; if yes, delete it. In particular the design artifact holds only the
*judgment half* of the code-map — decisions, rejected alternatives, enforcement strategy, danger
zones, deliberate debt. Everything structural is derived. Design docs rot as a rule; this is the
only known antidote.

**The contested case, stated in advance.** Spec was right, design was right, evidence was
collected per plan, production broke anyway → **QA's accountability**: the evidence standard was
insufficient. A role model that cannot assign a post-mortem is decoration.

**N=1 constraint.** On the demo, one person holds all three roles. The artifacts must separate
cleanly when accountability collapses into one person, or the framework cannot be dogfooded.

**Cross-artifact checks — the reason three beats one.** These pay for the ceremony:

- every requirement has a declared evidence standard (no requirement silently accepted on a
  happy path)
- every standard is met by actual evidence at that strength (today's RTM)
- every requirement has a realizing design decision; every design decision traces to a
  requirement (catches rogue complexity)
- the enforcement strategy claimed in design is the one found in code

The last is new and is the highest-value check of the four.

### D3.1 — Role ownership is process, not schema

**Decision.** The model does not encode role ownership. The artifacts and their fields exist in
the schema; who is accountable for each is org policy, recorded in project documentation.

**Why.** Accountability is the framework's *motivation*, but the mechanism does not depend on
it. The three artifacts earn their place because they hold structurally different information —
true whether or not an org has analysts or a QA function, and most have neither. Hard-coding
ownership breaks the tool for those teams and buys nothing for the rest. Keeping it out means
one model serves N=1 and a three-role team unchanged, which is also what D3's N=1 constraint
demands.

---

## D4 — The verification plan covers all means of assurance, not only tests

**Decision.** The verification plan records evidence of every kind — tests, static and
architectural rules, type and schema constraints, DB constraints, model checking, fault
injection and load, canary with guardrail metrics, production monitors and reconciliation,
manual exploratory passes, human review of judgment (accessibility, copy, UX), third-party
attestation.

### D4.1 — Evidence is classified by strength of claim

| Strength | Meaning | Examples |
|---|---|---|
| **Proof** | Holds over all executions | types, schema/DB constraints, architecture rules, exhaustive model checks (C10, C16, C12, C7) |
| **Demonstration** | Held for the executions sampled | all tests, including property tests — a wider ∃, still ∃ |
| **Detection** | We would find out if it stopped holding | monitors, reconciliation jobs, alerts, canary guardrails |

Teams routinely accept detection where proof was required — "we'll alert on it" is the most
common way a hard requirement is quietly downgraded. Making the three visibly distinct makes
that downgrade a signed decision rather than a drift. Detection is a claim about the *detector*,
not about the property; the artifact must not let those blur.

Proof-strength evidence and the top of the enforcement ladder are the same thing seen twice —
see D7.

### D4.2 — Every evidence item carries freshness

Tests are re-established every CI run, which is why nobody has had to think about this. Other
evidence decays. Each item records:

- **what re-establishes it, and at what cadence** — every commit / every release / quarterly /
  continuously
- **how it dies silently** — the attestation ages out; the exploratory pass was two releases
  ago; the monitor's query broke and it has fired zero times for six months

A monitor that can no longer fire is worse than no monitor, because it is carried on the books
as evidence. This failure mode has no analogue in tests and is where the artifact earns its
keep.

### D4.3 — Detection-strength evidence requires a detector test

You cannot test a production property before release, but you can test the detector: does the
reconciliation job flag an injected imbalance, does the deletion scan flag a planted record.

**Every detection-strength item must have a detector test.** This is machine-checkable and turns
the liveness concerns into a real chain of evidence instead of a hand-wave. C8 becomes: ledger
primitive property-tested + reconciliation job present + test proving the job catches a
synthetic break.

### D4.4 — Residual risk is a first-class field

With mixed evidence, "covered" stops being binary. QA's accountability is that the residual is
**explicit and accepted**, not that it is empty.

### D4.5 — The plan is mostly derived

Test evidence is derived from tags, never hand-listed. Only the non-derivable parts are
hand-written: monitors, manual passes, attestations, residual risk, and the strength and
freshness judgments. The failure mode is a compliance document nobody reads; a small
hand-maintained surface is the defence. Same discipline as D3.

---

## D5 — Required form moves from the spec to the verification plan

**Decision.** `scope × quantification` moves off the scenario and into the verification plan,
keyed by scenario id.

**Why.** Under D3 it is currently an analyst declaring a standard of evidence, which is not
their accountability. Analysts should not reason about test forms. This is a concrete,
falsifiable consequence of the role model.

**Refined by D13.** `quantification` is the claim's own quantifier over its domain and stays
with the claim; `scope` is a property of *evidence* for the behavioural domain and belongs
squarely in the plan. The split is cleaner than "required form moves wholesale", and the part
that moves is the part that was always an evidence judgment.

---

## D6 — Criticality attaches to requirements, not to code locations

**Decision.** Rigor is dialled by a small, closed set of named criticality levels declared on
requirements.

**Why not per-directory or per-module.** Criticality is a property of what a behaviour does —
money, safety, privacy — not of where code lives. A payment helper extracted into `utils/` is
still payment code and the directory's level does not follow it.

**Why this composes for free.** A site realizes a scenario; the scenario carries a level; the
site inherits it wherever it lives. No separate configuration for code at all. The proving case
is a critical requirement realized partly inside a low-rigor component: directory config
silently downgrades it, requirement-carried criticality does not.

### D6.1 — Who owns what

- **Analyst declares criticality** — business and regulatory impact. A requirements judgment.
- **The verification plan maps criticality → required evidence strength.** A QA judgment,
  written once per project rather than negotiated per requirement.

QA never argues about which features matter; analysts never reason about test forms.

### D6.2 — The declaration is never optional

An unclassified requirement is itself a hole. If the level may be absent, absence becomes the
default and the framework evaporates. One decision per requirement, on exactly the thing a human
should be thinking about.

### D6.3 — Exemptions are recorded, never silent

Generalizing the alpha's `Untraced`: a deliberate, attributable, reviewable exemption is fine
anywhere; a silent absence is not. This is what makes "let the team decide the degree of rigor"
honest rather than corrosive.

### D6.4 — Levels are not a configuration language

Few, named, defined by the framework. The only project-level choice is the mapping from level to
evidence strength. Twenty tunable knobs would make results incomparable across teams and reduce
compliance to "we turned off the checks we didn't like".

### D6.5 — The level gates which artifacts are required, not only how strong the evidence is

**Decision.** Criticality determines artifact *existence*, not merely evidence strength. A
low-criticality requirement needs a spec entry and nothing else — no design decision, no
evidence standard, no tags beyond the default.

**Why.** Otherwise every requirement costs four authored entries plus tags: correct for a
payment rule, absurd for a preference toggle. Dialling evidence strength alone leaves the
ceremony in place and only makes it cheaper to satisfy. This is the difference between a
framework a team can adopt and one they cannot, and it is what makes "the team decides the
degree of rigor" real rather than nominal.

### D6.6 — Criticality needs counter-pressure

**Decision.** Criticality is bounded. The mechanism — a declared cap on the share of
requirements at the top level, explicit review at the change boundary, or both — is chosen
during the steel thread; that there must be one is decided now.

**Why.** The analyst declares criticality; the developer and QA pay for it. With no feedback
loop everything drifts to critical, which is the most predictable failure of the whole
mechanism and would make D6 theatre. Cheap to add now, painful once a spec exists.

---

## D7 — Enforcement strength is recorded and ranked

**Decision.** The design artifact records *how* a rule is enforced, on a ranked ladder:

```
unrepresentable (type/schema)
  > structurally unbypassable (choke point, DB constraint)
    > centrally applied but opt-in (middleware)
      > guard at every site
```

**The bug this fixes.** The alpha's `guard` sits at the weakest rung and is the only rung
expressible. Worse, a concern solved at the strongest rung *looks like a violation*: one choke
point means N−1 members discharge no guard, reported as N−1 breaches. The model penalizes the
better design. This is a defect, not a matter of taste.

**The interaction that makes it valuable** *(revised)*. The first formulation was that
enforcement strength "earns test budget" — a trade negotiated between developer and QA. The
catalog shows it is an **identity**, not a bargain: C7's unique index is enforcement *and*
proof; C16's static rule enforces and verifies in one act; C10's type constraint likewise.

The top two rungs **are** proof-strength evidence in D4.1's sense. The bottom two are
enforcement that proves nothing on its own and still requires demonstration. Stating it as an
identity removes a negotiation and collapses two ladders into one, while leaving D3's split
intact: the developer owns the *mechanism*, QA owns the *sufficiency judgment*.

This is still the strongest argument that D3 is right — it is inexpressible with one artifact.

"The surface is empty because violation is unrepresentable" is the strongest possible result and
must be reported as such.

---

## D8 — Composability is a design constraint with tests

**Decision.** Three properties the design must satisfy, checked as the framework grows:

1. **Each mechanism is usable alone.** RTM without the design artifact. Code-shape rules without
   any spec. A verification plan without tags. If a mechanism requires the whole stack, adoption
   is all-or-nothing and will be nothing.
2. **Adding a mechanism enriches, never re-authors.** Introducing the design artifact must make
   existing checks smarter (D7) without touching a single existing spec.
3. **Mixed levels coexist without a coordinating center.** No global config that must know about
   every component.

**The integration test for all three:** point the tool at an existing codebase and adopt
incrementally — baseline current holes, forbid new ones. If a ratchet works, it composes. A
second corpus of real repos is available in `~/drim` and is cheap to try.

---

## D9 — The CLI is `azimuth`

**Decision.** Rename `rtm` → `azimuth`. `rtm` survives as the name of one *check*.

**Why it is not cosmetic.** `rtm` names one output as though it were the product. The matrix is
now one derivation among several over three artifacts; keeping the name would keep pulling
design discussion back toward the matrix.

**Not `azim`.** The binary is typed interactively far less than it looks — it lives mostly in CI
config, scripts and docs. `azim` reads like an abbreviation people cannot confidently spell and
is worse to search for. One word for repo, package, docs and binary. Ship `azim` as an alias
later if wanted; that direction is easy, the reverse is not.

### D9.1 — Check ids are a public interface

Once checks are plural and extensible, ids appear in the criticality→evidence mapping, in
exemption records, in CI config, and in whatever a team pins. They need stable ids on the same
footing as spec ids. Decide the id scheme before there are five checks, not after.

### D9.2 — Severity comes from criticality, not from the check

One non-zero exit for "something failed" stops being useful at ten checks. A hole on a critical
requirement fails the build; the same hole on a low-criticality one warns. This is D6 doing real
work at the CLI boundary.

### D9.3 — The check set is per-project

`azimuth check` runs the configured set; individual checks are addressable. This is D8.1 at the
CLI: adopt exactly one check on day one.

---

## D10 — The derived model is a first-class artifact

**Decision.** `azimuth export` emits the resolved model as JSON — requirements with criticality,
scenarios, realizing sites, tests and forms, evidence items with strength and freshness, design
decisions, exemptions, holes. Checks are consumers of it, not owners of it.

**Why.** Every mechanism so far produces a verdict. Dashboards, PR annotations, IDE hints and the
agent tier need the *data*, not the verdicts. If a consumer must re-parse specs or re-derive
links, the seam is in the wrong place — cheap to fix now, structural later.

**The extensibility test, stated falsifiably.** Can a dashboard be built with zero access to
Azimuth internals, reading only the export? Verify this early.

**Time.** The export carries a commit id and timestamp; history is a series of exports; the tool
stays stateless and grows no database. Freshness (D4.2) only gets teeth when something looks at
it over time.

**A test of whether D3 is real.** Each role wants a different view — analyst: unclassified
requirements, requirements with no evidence standard. Developer: unrealized scenarios,
enforcement claimed vs. found, design decisions tracing to nothing. QA: residual risk, freshness
decay, detection items without a detector test. **If the three views turn out to be
substantially the same view, the three-artifact split is decorative.** Costs nothing to check
once the export exists.

**Scope guard.** Dashboards are a scope magnet. This is a constraint satisfied now (ship the
export, keep checks on the consumer side) and a deliverable much later. The highest-value
operative tool first is a PR comment, not a web app — it lands where the decision is made.

### D10.1 — There are two classes of check, with different inputs

**Decision.** *Model-consuming* checks read the export. *Code-consuming* checks — everything in
the code-artifact domain (C16, C2, C10) — need AST, call-graph and schema access, and cannot run
off a spec/tag model at all.

**Consequence.** The plugin interface must admit both, and each extractor manifest must declare
which code facts it exposes. Designing a single check interface over the export would strand an
entire claim domain — the one whose violations no behavioural test catches reliably, which is
the domain that most needed a tool.

---

## D11 — Own the spec format; drop the OpenSpec dependency

**Decision.** No external tool constrains the format. Keep the ideas, own the implementation.

**Why now.** The repo is empty, so it is free today and a migration later. The forcing reason is
that the format must carry criticality levels, links to design and verification plan, exemption
records and whatever the catalog clustering produces — none of which OpenSpec has a place for.
Encoding them as conventions inside someone else's format is the standard signal to own it. It
also contradicts D2.3, which cannot be honoured while a third party owns the schema.

**Kept.**

- `spec → requirement → scenario`, scenario as the unit of coverage. Settled, and the good part.
- Markdown as the authoring surface. Agents write it, humans review it in a PR diff. Not a DSL;
  nobody hand-writes JSON. The machine-readable form is D10's export.
- **The change/delta concept**, which matters more here than it did. With three artifacts a
  change must move all three coherently — the analyst's requirement, the developer's design
  decision, QA's evidence standard. The change is where the three accountabilities meet and hand
  off, it is the natural review unit, and it is the natural unit for the CLI
  (`azimuth check --change …`) and for the adoption ratchet.

**Unblocked by owning it.** Id semantics under split and merge — designed for rather than worked
around. See open questions.

**Guardrail.** Not a format-design project. The format is exactly what the steel thread needs and
nothing more; the parser fails loudly on anything it does not recognize; it extends when a
concern demands it, never in anticipation.

---

## D12 — Liveness and production oracles are in scope *(revises an earlier call)*

**Decision.** Concerns with no pre-production oracle (C3 deletion propagation, C8 money
conservation, C18 retention purge) stay in scope.

**Why the reversal.** They were initially parked because production oracles had no owner among
the three roles. D4 resolves that: QA's artifact names monitors as evidence, so QA owns their
*adequacy* even when someone else operates them. No fourth role is needed. D4.3 makes them
checkable before release.

---

## D13 — One claim type, parameterized by domain *(supersedes the catalog's four-artifact reading)*

**Decision.** There is one kind of claim. What differs between an ordinary scenario and a
cross-cutting rule is **what the claim ranges over**.

```
claim    = (domain, quantifier, predicate)
evidence = (strength, freshness)
```

The catalog's six subjects become six **values of a field**, not six artifact types:
executions of a behaviour · a set of sites · the code artifact itself · paired derivations that
must agree · aggregate state over time · eventual absence.

**Why not four new artifacts.** That was the obvious reading of the catalog and it is the wrong
response to a right observation. The alpha already carries roughly fifteen coordinate concepts;
adding four more artifact types with four notations is how a framework becomes unlearnable, and
it violates 0.1 directly.

**What it subsumes.** The alpha's `quantification ∈ {example, invariant}` is exactly ∃/∀ over
*one* domain — executions matching a WHEN. `scope` is a property of evidence for that same
domain. Both are one domain's parameters promoted to universal status; under D13 they take their
proper place and cost nothing.

**What it buys.** Supporting a new kind of rule requires a domain value, a derived enumerator
for it, and its admissible evidence kinds — no new artifact, no new syntax, and every existing
check generalizes. This is D8.2 satisfied by construction rather than by intention, and it is
the concrete answer to whether the framework is extensible.

**Ergonomics preserved.** Behavioural scenarios take the default domain implicitly and never
mention it. Authoring a spec does not get harder.

### D13.1 — Domain enumerators are derived, never hand-listed

**Decision.** When a claim ranges over an enumerated set, the enumeration is derived from the
same source the system is built from — the route table, the DI container, the type graph, the
migration set.

**Why.** Something must produce the enumeration, and it can be wrong. An enumerator of "every
rider-reachable serializer" that misses one lets C1 leak in precisely the way the rule existed
to prevent — the mechanism reproducing the bug one level up, and reporting green. A hand-listed
surface is worse than no rule at all.

### D13.2 — "Enumerator unsound or underived" is a hole kind

Follows from D13.1 and does not exist in the alpha. It is the first thing a claim over a set
needs, before any check over its members means anything.

### D13.3 — The domain set is closed for this phase

Six values, framework-defined. Whether projects may add domains stays open (see below): an open
set is easy to grant later and impossible to withdraw, and comparability across teams is worth
more right now than extensibility nobody has asked for.

---

## D14 — Agent-tier judgments are evidence items

**Decision.** Verify-pass outputs are recorded as evidence: strength `demonstration`, freshness
re-established when the subgraph they judged changes. They appear in the export like any other
evidence.

**Why.** As things stand the agent tier silently absorbs everything hard — test toothiness, tag
honesty, spec completeness, per-member discharge correctness (C15) — with no design, no decay,
and no representation in the model. If it quietly stops working, the machine tier degrades to
the self-certification game the alpha README already names as the failure mode.

Folding it into D4 gives it ownership, a freshness clock, and visibility: "this claim's only
evidence is an agent judgment from forty commits ago" becomes a state the tool can report rather
than an invisible one.

**Consequence.** An agent judgment is never proof-strength, whatever its confidence.

---

## Method

**Concern catalog first, notation last.** No mechanism enters the model until **≥2 structurally
different concerns demand it**, and each is written as prose before it is written as syntax. This
is the guardrail against overfitting the notation to ride-hailing. Singletons stay prose in the
design artifact's judgment half.

**Build the steel thread with no cross-cutting notation at all.** Hold the eighteen concerns as
prose; see what the per-scenario matrix actually misses. The holes experienced are better
evidence than the holes predicted — the catalog is a prediction and should be treated as one.

**Validation is measured, not felt.** Define up front what counts: holes that were real defects
vs. false positives, annotation cost per change, how often the agent tier caught a dishonest tag
the machine passed. Otherwise the outcome is a nice app and an impression.

**Build the demo the way the framework assumes.** An agent writes most of the code and the diff
is not read. Carefully hand-writing the fixture destroys the conditions the framework exists to
address.

---

## What would falsify this

Written before the demo so the answer cannot be retrofitted afterwards.

- **The role split is decorative** if the three role views over the export (D10) turn out
  substantially identical. This is the sharpest test available and costs nothing once the export
  exists.
- **The level mechanism is theatre** if requirements keep landing at top criticality — say above
  ~40% — even with D6.6's counter-pressure in place.
- **The framework is ceremony** if authored-artifact and annotation cost per change exceeds what
  the defects it catches justify. Measured, per the method above, not felt.
- **The core claim fails** if the agent tier cannot reliably detect a dishonest tag. The machine
  tier is then self-certification, and no amount of structure repairs it.
- **The domain unification (D13) is wrong** if a real concern from the demo fits none of the six
  domains and cannot be given one without introducing a separate artifact type.

---

## Open questions

**Deliberately unresolved. Each needs evidence from the demo, not more argument.**

1. **Id semantics under split and merge.** What happens to a scenario's id and its accumulated
   tags when a requirement splits in two or two merge? Is there a supersedes relation? Does the
   matrix report the transition as a hole, and for how long? Expected to be where the model
   cracks; surge pricing (D1) is the chosen stress case.
2. **What realizes a scenario across a message broker.** Producer and consumer sites both carry
   honest tags; a misrouted topic leaves the matrix green with nothing in between. Is broker
   configuration a realization site? (C15, C16)
3. **What `scope: component` guarantees.** In C11 it must mean "against a real store"; in C5 it
   need not. One word currently names two different guarantees. Microservices may need a fourth
   rung, or a redefinition — where does a contract test sit?
4. **What `realizes` means for a rule with no site** (C8, conservation over global state).
5. **What is tagged when enforcement is a DB constraint** — the migration?
6. **Whether the domain set is right, and whether it should stay closed.** D13.3 fixes the six
   the catalog found and closes the set for this phase. The steel thread should try to break it.
   In current order of evidence: a set of sites (C4, C5, C14, C15 — four members from three
   unrelated areas), the code artifact (C16, C2, C10), paired derivations (C9, C17), aggregate
   state (C7, C8, C11).
7. **How a generated check judges a domain whose members discharge differently.** C15 is the
   stress case: each consumer's dedupe is correct in a different way, so a check over the set
   can assert only that a discharge *exists*, not that it is right. This is exactly the
   machine-tier / agent-tier seam, and D14 is the current answer — worth testing whether it
   holds.

---

## Explicit non-goals for this phase

- A second native mobile app; production-grade infrastructure; service count beyond the concerns
- Backward compatibility, migrations, semver
- Dashboards and operative tooling as deliverables (the export seam is the deliverable)
- A configuration language for rigor levels
- A general-purpose spec format beyond what the steel thread needs
- Separate artifact types per cross-cutting concern kind — superseded by D13
- Role ownership encoded in the schema — superseded by D3.1
