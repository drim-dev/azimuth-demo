# Concern Catalog — ride-hailing demo

Input to the clustering step. Each entry is a cross-cutting concern taken from the demo
domain, described **before** any notation exists for it. Notation is designed afterward, from
the clusters, and only where a cluster has ≥2 structurally different members.

Nothing here should be read as a proposal for Azimuth syntax. The point of the exercise is to
find out how many *different things* "cross-cutting concern" is currently naming.

## Template

Each entry records:

- **Statement** — the rule, in prose, as a domain person would say it.
- **Surface** — the set of places it applies to, and **how that set grows**. A concern whose
  surface is closed is not really cross-cutting; the ones that hurt are the ones where merging
  an unrelated feature enlarges the surface.
- **Enforcement** — where the code makes it true. Distinguish: *guard at each site*,
  *choke point* (one place violation must pass through), *type/schema* (violation
  unrepresentable), *storage constraint*, *out-of-band job*.
- **Verification** — how you'd show it holds. Where the existing vocabulary
  (`scope ∈ {unit, component, e2e}`, `quantification ∈ {example, universal}`) fits, it's used;
  where it doesn't, that's noted as a **gap**, which is the payload of this document.
- **Silent failure** — how it breaks with every existing check green. If there isn't a good
  answer here, the concern doesn't belong in the catalog.
- **Forgotten by** — the realistic change that violates it without anyone noticing.
- **Shape** — provisional tag, revised in the clustering section.

---

## Privacy and exposure

### C1 — Precise driver location is not exposed to a rider before accept

- **Statement.** Before a driver accepts, a rider may see coarse supply density only. Between
  accept and dropoff, the rider sees the driver's position. After dropoff, neither party sees
  the other's position, ever again.
- **Surface.** Every response body, push payload, websocket frame, analytics export, support
  screen, and debug endpoint reachable by a rider principal. **Grows** with: every new rider
  endpoint, every added field on an existing DTO, every new push notification type, every
  support tool. This surface has never in the history of software stopped growing.
- **Enforcement.** Realistically a choke point: driver position exists in the domain as a type
  that cannot be serialized without passing through a redaction function parameterized by trip
  phase. The guard-at-every-site version is the design that leaks.
- **Verification.** Per-site `component`/`example` tests are the obvious thing and are the weak
  answer — they verify the sites that exist. The strong answer is a rule over the *class*: no
  serializer reachable from a rider-authenticated route may emit the precise-location type
  unredacted. That is one check quantified over all members — closer to an architecture test
  than to anything in the current scope ladder.
- **Silent failure.** A new "trip receipt" endpoint returns the full GPS trace. Its own tests
  pass; the location scenario is realized elsewhere so the matrix is green.
- **Forgotten by.** Anyone adding a field to a shared DTO.
- **Shape.** Prohibition over an open surface.

### C2 — Rider and driver never see each other's real phone number

- **Statement.** In-app contact is routed through proxy numbers, which expire at trip end.
- **Surface.** Contact flows in both apps, both BFFs, SMS/push templates, support tooling, CSV
  exports, and the analytics warehouse. **Grows** with new notification templates.
- **Enforcement.** Choke point at the communications service — but *also* a data-modelling
  rule: the raw number must not be denormalized into trip records. Two mechanisms for one rule.
- **Verification.** Component tests on the proxy path, plus a schema-level rule that no
  trip-scoped table or event carries a raw MSISDN. The second is a **static rule over
  artifacts, not a test of behavior** — no rung on the current ladder describes it.
- **Silent failure.** A notification template interpolates the raw number for "convenience."
- **Forgotten by.** Anyone writing a new template or an export.
- **Shape.** Prohibition over an open surface + representation constraint.

### C3 — A deleted user's PII does not survive in the warehouse or exports

- **Statement.** After a deletion request is honored, no PII for that user is retrievable from
  any store, including analytics, backups within policy window, and generated exports.
- **Surface.** Every store, every pipeline stage, every materialized view, every file the
  export job has ever written. **Grows** with every new pipeline stage or derived table.
- **Enforcement.** Out-of-band propagation job plus a discipline that derived tables carry the
  user key. Cannot be a guard; cannot be a choke point.
- **Verification.** Not verifiable by a pre-production test in any meaningful sense — the real
  check is a periodic production scan asserting absence. **Gap: this concern has no
  pre-production oracle at all.**
- **Silent failure.** A new derived table joins on a hashed id the deletion job doesn't know
  about. Everything is green forever.
- **Forgotten by.** Anyone adding a pipeline stage.
- **Shape.** Liveness (eventual absence), verified only in production.

### C4 — Support access to PII is purpose-bound and audited

- **Statement.** A support agent reads rider/driver PII only with a stated reason attached to a
  ticket, and every such read is recorded append-only.
- **Surface.** Every support-facing read path. **Grows** with each support tool feature.
- **Enforcement.** Choke point: support reads go through one gateway that requires a reason
  token. Weak if any service is reachable directly.
- **Verification.** Component tests that the gateway rejects reason-less reads, plus a rule
  that no support principal can reach a service except via the gateway — again architectural,
  not behavioral.
- **Silent failure.** A new internal admin endpoint bypasses the gateway.
- **Forgotten by.** Anyone building an internal tool under time pressure.
- **Shape.** Obligation on every member + topology constraint.

---

## Authorization

### C5 — Every trip-scoped read is authorized to a participant of that trip

- **Statement.** Only the rider, the assigned driver, or an audited support agent may read a
  given trip.
- **Surface.** Every endpoint taking a trip id, in both BFFs and every service. **Grows**
  monotonically with the API.
- **Enforcement.** Ideally a type: a trip is only obtainable from a repository call that takes
  a principal, so an unauthorized trip object cannot be constructed. Realistically, a guard
  per handler, which is why this class of bug is eternal.
- **Verification.** Per-endpoint `component`/`example` is what everyone does. The honest form
  is universal quantification over the endpoint set: *for every* route taking a trip id, a
  non-participant gets 404. That is generated, not hand-written — one test that enumerates the
  surface.
- **Silent failure.** New endpoint, no authorization check, positive-path tests pass.
- **Forgotten by.** Every new endpoint, forever.
- **Shape.** Obligation on every member of an enumerable surface.

### C6 — Market isolation

- **Statement.** Data belonging to one regulatory market is not readable from another, and
  cross-market queries fail closed.
- **Surface.** Every query, every cache key, every index, every analytics join.
- **Enforcement.** Storage-level: row-level security or physically separate stores. Guards in
  application code are known to be insufficient.
- **Verification.** Component tests against a real store, plus a rule that no query is issued
  without a market predicate. Detecting the latter statically is genuinely hard.
- **Silent failure.** A cache key omits the market; two markets share an entry under load.
- **Forgotten by.** Anyone adding a cache.
- **Shape.** Prohibition, enforced below the application.

---

## Money

### C7 — A trip is charged exactly once

- **Statement.** Retries, duplicate events, and concurrent completion paths result in one
  capture.
- **Surface.** Every path that can trigger a capture: trip completion, cancellation fee,
  reconciliation retry, support-issued charge, consumer redelivery.
- **Enforcement.** Idempotency key at a choke point in payments, backed by a unique constraint.
  The constraint is the real enforcement; the application code is a courtesy.
- **Verification.** `component`/`universal` under concurrent and duplicated input — a property
  test, not an example. Plus the DB constraint itself, which is enforcement *and* proof.
- **Silent failure.** A new cancellation flow generates its own key format; duplicates slip.
- **Forgotten by.** Anyone adding a new charge trigger.
- **Shape.** Mutual exclusion / uniqueness.

### C8 — Money is conserved

- **Statement.** For every completed trip, rider charges = driver payout + platform fee +
  taxes + adjustments. Across the system, nothing is created or destroyed.
- **Surface.** Not a site. This is a property of the aggregate ledger state.
- **Enforcement.** Double-entry ledger; balanced postings are structurally required.
- **Verification.** A reconciliation job in production. Pre-production you can property-test
  the ledger primitive, but the concern as stated is about real data over time. **Gap: no site
  realizes this and no test covers it, yet it is arguably the most important rule in the
  system.**
- **Silent failure.** A new promo type posts a credit with no counter-posting. Unit tests of
  the promo pass.
- **Forgotten by.** Anyone adding a discount, refund, or incentive.
- **Shape.** Conservation over global state.

### C9 — The quoted fare is the charged fare

- **Statement.** The amount shown at booking is the amount captured, unless an adjustment with
  a recorded, enumerated reason applies.
- **Surface.** Quote path (pricing service → rider BFF → clients) and capture path (trip →
  payments). Two independent implementations that must agree.
- **Enforcement.** A signed quote token carried from quote to capture; capture recomputes and
  compares.
- **Verification.** The interesting form is **differential**: observe both paths for the same case
  and assert agreement — a relational oracle, unless an independent reference implementation
  computes an exact expected result and makes it model-based. Per-path example tests can both pass
  while the paths disagree.
- **Silent failure.** A surge rule is added to pricing but not to the recompute path.
- **Forgotten by.** Anyone changing pricing rules.
- **Shape.** Coherence between independent implementations. **New shape — not in the earlier
  six.**

### C10 — No money is represented in floating point, and rounding never invents or loses value

- **Statement.** Currency is integer minor units; splits round deterministically and the
  remainder is allocated, not dropped.
- **Surface.** Every type, DTO, serializer, and analytics column touching an amount, in every
  language in the stack.
- **Enforcement.** Type-level, per language. The cross-language boundary is the weak point.
- **Verification.** Property test on the split primitive, plus a static rule banning float in
  money positions, plus contract tests at each language boundary.
- **Silent failure.** A mobile client parses an amount as a double for display, then sends it
  back.
- **Forgotten by.** Anyone adding a field in a client language with weaker typing.
- **Shape.** Representation constraint spanning languages.

---

## Concurrency and lifecycle

### C11 — A driver is on at most one active trip

- **Statement.** Concurrent dispatch, manual assignment, and support reassignment cannot
  produce two active trips for one driver.
- **Surface.** Every path that can create or reassign an active trip.
- **Enforcement.** Serialization at one point — a partial unique index, or a per-driver
  single-writer. Guards at each site are the failure mode.
- **Verification.** `component`/`universal` under concurrency against the real store. An
  in-memory fake proves nothing here, which is an interesting constraint on what `component`
  is allowed to mean.
- **Silent failure.** A new support "force assign" tool writes directly.
- **Forgotten by.** Anyone adding a write path.
- **Shape.** Mutual exclusion.

### C12 — Trip lifecycle is monotone

- **Statement.** A trip never returns to an earlier state. `completed` and `cancelled` are
  terminal.
- **Surface.** Every transition site, every consumer that reacts to out-of-order events.
- **Enforcement.** A state machine with a guarded transition function, plus a conditional write
  on the current state.
- **Verification.** Model-based: the property is over the transition *relation*, checkable
  exhaustively against a model. This is where `quantification: universal` fits most naturally
  of anything in the catalog.
- **Silent failure.** An out-of-order event from a redelivered message rewinds a trip.
- **Forgotten by.** Anyone adding a state or a consumer.
- **Shape.** Lifecycle / temporal safety.

### C13 — A dispatch offer is accepted by exactly one driver

- **Statement.** When N drivers are offered the same ride, exactly one accept succeeds; the
  rest get a defined losing response.
- **Surface.** The dispatch accept path and any retry of it.
- **Enforcement.** Compare-and-set on the offer record.
- **Verification.** Concurrency property test; also a good chaos/soak candidate.
- **Silent failure.** Two accepts under partition, both told they won.
- **Forgotten by.** Anyone "optimizing" the accept path.
- **Shape.** Mutual exclusion.

---

## Protocol and reliability

### C14 — Every state-mutating endpoint is idempotent under client retry

- **Statement.** A client retrying with the same idempotency key gets the original result, not
  a second effect.
- **Surface.** Every mutating route in both BFFs and every service. **Grows** with the API.
- **Enforcement.** Middleware at a choke point — but only if every route opts in, which
  reintroduces the per-site obligation through the back door.
- **Verification.** One generated test quantified over the route table: for every mutating
  route, replay produces one effect. This is the cleanest example in the catalog of a check
  whose subject is *the set of members*, not a behavior.
- **Silent failure.** A new route registered outside the middleware chain.
- **Forgotten by.** Anyone adding a route, especially in a new service.
- **Shape.** Obligation on every member of an enumerable surface. Same shape as C5, different
  domain — which is evidence the shape is real.

### C15 — Every event consumer tolerates at-least-once and out-of-order delivery

- **Statement.** Redelivery causes no duplicate effect; an older event does not overwrite newer
  state.
- **Surface.** Every consumer, in every service, plus the analytics pipeline.
- **Enforcement.** Per-consumer: dedupe on event id and a version/sequence check. Genuinely
  hard to centralize, because the *correct* dedupe key is domain-specific.
- **Verification.** Per-consumer property test with a redelivering, reordering harness.
- **Silent failure.** A new consumer processes fine in order, and production is mostly in
  order, so it fails rarely and looks like a mystery.
- **Forgotten by.** Anyone adding a consumer.
- **Shape.** Obligation on every member, but *not* uniformly dischargeable — each member needs
  a different implementation. Distinguishes it from C5/C14.

### C16 — Every external side effect crosses the transaction boundary exactly once

- **Statement.** A charge, push, or SMS is never emitted for a transaction that rolled back,
  and never lost for one that committed.
- **Surface.** Every place a domain transaction triggers an outbound effect.
- **Enforcement.** Transactional outbox. Structural — direct calls from inside a transaction
  are the violation.
- **Verification.** A static rule: no outbound client is invoked inside a transaction scope.
  This is a **code-shape rule with no behavioral test that would catch it reliably** — the
  failing case requires a rollback at the wrong instant.
- **Silent failure.** A new feature calls the push client inline. Works in every test.
- **Forgotten by.** Everyone. This is the single most-repeated mistake in the catalog.
- **Shape.** Prohibition on code shape, not on behavior. **New shape.**

---

## Cross-boundary agreement

### C17 — Error codes are stable and identical across clients

- **Statement.** A given failure produces one stable machine-readable code, identically in web
  and mobile, and every code has a localized message in every supported locale.
- **Surface.** Every error path, every client, every locale file. **Grows** with every feature
  and every new locale.
- **Enforcement.** A single generated source of truth for codes, consumed by all clients.
- **Verification.** A completeness check across artifacts: every code emitted by any service
  has an entry in every locale file and a handler in every client. Not a test of behavior — a
  check over the cross-product of artifacts.
- **Silent failure.** A new code ships; mobile shows a raw string in one locale.
- **Forgotten by.** Anyone adding an error case.
- **Shape.** Coherence / completeness across artifacts. Related to C9 but the subject is
  artifacts, not executions.

### C18 — Raw location traces are purged after the retention window

- **Statement.** Raw GPS points older than N days do not exist in any store.
- **Surface.** Every store that has ever received a location point, including caches and
  warehouse partitions.
- **Enforcement.** Retention policy per store plus a purge job.
- **Verification.** Production scan. Same structural situation as C3.
- **Silent failure.** A new store receives location data and is not on the purge job's list.
- **Forgotten by.** Anyone introducing a store or a cache.
- **Shape.** Liveness (eventual absence), production-only.

---

## Clustering — what the catalog says

### Every concern is a claim; they differ only in what the claim ranges over

The current model splits on *scenario vs. cross-cutting*, as though cross-cutting were one
thing. It isn't — but neither is it six things needing six notations. Every entry in this
catalog, and every ordinary scenario, has the same shape:

```
claim    = (domain, predicate)      — what it ranges over, and what must hold of it
evidence = (strength, freshness)    — how well we know it, and for how long
```

What the catalog found is **six domains**:

| Domain — what the claim ranges over | Concerns | Quant | Evidence that fits |
|---|---|---|---|
| Executions of a behaviour (inputs matching WHEN) | ordinary scenarios; C1, C6 | ∃ / ∀ | tests |
| A set of sites | C4, C5, C14, C15 | ∀ | one check generated over enumerated members |
| The code artifact itself | C16, C2, C10 | ∀¬ | static analysis, types, schema — *proof* |
| Paired derivations that must agree | C9, C17 | ∀ | differential test, cross-product completeness |
| Aggregate state over time | C7, C8, C11 | ∀ | property test on the primitive + reconciliation |
| Eventual absence | C3, C18 | ∀ eventually | detection only |

The alpha's notation (`invariant` over a `class`, discharged by `guard`s at member sites)
expresses row 2 and, partially, rows 1 and 5. It cannot express rows 3, 4 or 6 at all. The
cross-cutting design isn't wrong; it's **one row of a six-row table**.

Note the quantifier column: it is constant. Every claim in this catalog is universal, and the
only existential claims are marginal capability statements. That constancy is why the claim
carries no quantifier field — the *domain* does the work the quantifier appeared to do, and
`example` vs `invariant` belongs to evidence, not to the claim (decisions D13, D5).

### One artifact with a domain field, not six artifact types

The obvious reading of that table is "four new artifacts are needed." That is the wrong
response to the right observation, and it is how frameworks become unlearnable — the alpha
already carries roughly fifteen coordinate concepts before any of this lands.

Six domains are six *values of a field*, not six notations. The gain is not tidiness:

- A new kind of rule needs a domain value, a way to enumerate that domain, and its admissible
  evidence kinds. No new artifact, no new syntax, and existing checks generalize to it.
- The alpha exposed a `quantification` field where it should have exposed a domain: `example`
  vs `invariant` is how thoroughly the *evidence* ranges, and the domain is what the *claim*
  ranges over. `scope` is likewise an evidence property — and only for demonstration-strength
  evidence at that, since a static rule executes nothing.
- Behavioural scenarios are unaffected: they take the first domain by default and never mention
  it.

### Domain enumeration is where this can silently fail

If a claim ranges over an enumerated set, something must produce that enumeration — and the
producer can be wrong. An enumerator of "every rider-reachable serializer" that misses one lets
C1 leak in exactly the way a surface rule was invented to prevent. The mechanism reproduces the
bug one level up, and reports green.

Two consequences, both load-bearing:

- **Enumerators are derived from the same source the system is built from** — the route table,
  the DI container, the type graph, the migration set. Never hand-listed. A hand-listed surface
  is worse than no surface rule, because it is green and wrong.
- **"Enumerator unsound or underived" is a hole kind** in its own right. It does not exist in
  the alpha, and it is the first thing a claim over a set needs.

### Safety versus liveness — a boundary, not an exclusion *(revised)*

C3, C8 and C18 cannot be discharged before production. The first reading was to declare them
out of scope, because a production oracle has no owner among the roles and makes the tool
depend on runtime infrastructure.

That is now resolved rather than parked. Once evidence is classified by strength, a monitor is
*detection*-strength evidence, owned by whoever is accountable for sufficiency; and the claim
becomes checkable before release by shifting the subject — **test the detector**. Does the
reconciliation job flag an injected imbalance; does the deletion scan flag a planted record.

The liveness domain stays in the model, under one constraint: detection-strength evidence
without a detector test is a hole.

### Enforcement strength is evidence strength at the top of the ladder *(revised)*

Across the catalog, the same rule is enforceable at very different strengths:

```
unrepresentable (type/schema)  >  structurally unbypassable (choke point, DB constraint)
                               >  centrally applied but opt-in (middleware)
                               >  guard at every site
```

The alpha's `guard` sits at the weakest rung and is the *only* rung expressible. Worse, a
concern solved at the strongest rung *looks like a violation*: one choke point means N−1
members discharge no guard, reported as N−1 `invariant-breach` rows. **This is a concrete bug,
not a matter of taste** — it penalizes the better design.

The catalog also shows why the fix is not an "enforcement budget" that trades against tests.
C7's unique index is enforcement *and* proof. C16's structural prohibition is enforced and
verified by the same static rule. C10's type constraint likewise. The top two rungs **are**
proof-strength evidence — strong enforcement is self-evidencing. Stating it as an identity
rather than a bargain is cleaner and removes one of the two ladders.

"The surface is empty because violation is unrepresentable" is the strongest possible result
and must be reported as such.

### Enforcement and verification are still separate fields

The identity above holds only at the top of the ladder. C2 is enforced two ways (choke point
*and* a representation constraint) and verified two ways (component test *and* a schema rule).
A middleware or a per-site guard is enforcement that proves nothing on its own; C16 is enforced
structurally and verified statically with no behavioural test at all. One `guard` field
conflates all of this.

### Domains in order of evidence

1. **A set of sites** — C4, C5, C14, C15. Strongest cluster: four members from three unrelated
   areas. Needs a derived enumerator above all else.
2. **The code artifact itself** — C16, C2, C10. C16 alone justifies it; no behavioural test
   catches it reliably.
3. **Paired derivations** — C9, C17. Each side individually correct, jointly wrong.
4. **Aggregate state over time** — C7, C8, C11. No site owns the claim, which strains
   `realizes`.

C15 is the stress case for (1): a per-member obligation where each member's discharge is
*different*, so a generated check can assert only that a discharge exists, not that it is
correct. That is precisely the machine-tier / agent-tier seam.

### Open questions for the steel thread

- Does a `realizes` tag mean anything for a claim over aggregate state, which has no site (C8)?
- What realizes a scenario across a message broker — is broker configuration a site? (C15, C16)
- If a claim is enforced by a DB constraint, what is tagged: the migration?
- `scope: component` in C11 must mean "against a real store," and in C5 need not. One word
  currently names two different guarantees.
- Are the six domains a closed set, or may a project add one? Extensibility argues open;
  comparability across teams argues closed.

### Method note

None of the above should become syntax until the steel thread is built **without** it, with
these eighteen concerns held as prose. The holes the per-scenario matrix actually misses are
better evidence than this document, which is a prediction.
