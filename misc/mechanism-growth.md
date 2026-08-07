# Mechanism growth — what the fixture still has to instantiate

**Status: proposed.** Nothing here is decided. It is a selection list, ordered by what the
*framework* would learn, not by what the application would gain. Every item is subject to D1's
feature-selection rule: a feature enters only if it instantiates a concern or a hard case.

## The gap this answers

Three measurements, taken 2026-08-07:

- **Two of the six claim domains are exercised** (`tools/azimuth/README.md`): executions of a
  behaviour, and a set of sites. The code artifact itself, paired derivations, aggregate state
  over time and eventual absence have no instance in the corpus. D13.3 closed the set at six on
  the strength of the catalog; four of the six have never been written down as data.
- **Two of the four enforcement rungs are exercised.** All 16 design entries are rung 1 or 2 —
  6 `type`, 6 `choke-point`, 4 `constraint`. Zero `middleware`, zero `guard`. The fixture was
  written by one author who reached for the strongest available rung every time, so D7's named
  defect (a choke point reported as N−1 `invariant-breach` rows) and the entire
  demonstration-required half of the ladder have never fired.
- **Four open questions have no fixture that can ask them** — 2 (realization across a broker),
  4 (`realizes` for a rule with no site), 5 (what is tagged when a DB constraint enforces), and
  7 (a domain whose members discharge differently).

The application is a synchronous request/response system over one Postgres. Every mechanism
below exists to close one of those gaps. **None of them makes the demo a better ride-hailing
product, and that is the point.**

## The list

Each row names the concern it instantiates (C-numbers from `docs/concern-catalog.md`), the claim
domain it forces into the model, the enforcement rung it introduces, and the open question or
falsifier it moves. `Prediction` is what should happen if the framework is right; recorded before
the evidence exists, per the method.

---

### Queues and asynchronous delivery

#### M1 — A message broker with at least two consumers in different services

- **Concerns.** C15 (at-least-once and out-of-order delivery), C12 (out-of-order events rewind a
  trip).
- **Domain.** A set of sites — but the stress case of it. Each consumer's dedupe key is
  domain-specific, so a generated check over the consumer set can assert only that *a* discharge
  exists, not that it is correct.
- **Enforcement rung.** 4 (`guard`) per consumer, deliberately. This is the corpus's first
  weak-rung mechanism and it is chosen for that reason.
- **Moves.** Open question 2 (is broker configuration a realization site — producer and consumer
  both carry honest tags while a misrouted topic leaves nothing in between), open question 7 (the
  machine-tier / agent-tier seam), D14/D18.
- **Prediction.** The machine tier can report that every consumer discharges *something* and
  cannot report whether the dedupe key is right. If the agent tier does not catch a plausible
  wrong key, D14 is the wrong answer to open question 7.
- **Cost.** One broker in compose, a consumer host, a redelivering/reordering test harness. The
  harness is the expensive part and is reusable.

#### M2 — Transactional outbox

- **Concerns.** C16 — named in the catalog as the single most-repeated mistake, and the concern
  that alone justifies the code-artifact domain.
- **Domain.** The code artifact itself (∀¬). First instance in the corpus.
- **Enforcement rung.** 2 (`choke-point`), verified by a static rule: no outbound client is
  invoked inside a transaction scope. Enforcement and verification are the same artifact, which
  is D7's identity at the top of the ladder made concrete a second time.
- **Moves.** The code-artifact domain from decided to instantiated; open question 5 by adjacency
  (what is tagged — the analyzer, the migration, or the outbox table).
- **Prediction.** No behavioural test in the corpus will catch a direct call from inside a
  transaction. If one does, C16's claim that no behavioural test catches it reliably is wrong and
  the catalog needs amending.

#### M3 — At-least-once redelivery on the completion → capture path

- **Concerns.** C7 (charged exactly once) under a real transport rather than a simulated retry.
- **Domain.** Aggregate state over time.
- **Enforcement rung.** 2 (`constraint`) — the existing unique index, now genuinely load-bearing.
- **Moves.** `payments/capture`'s `retry-after-transport-failure` currently claims a transport
  failure the fixture cannot produce. This makes the scenario honest.
- **Prediction.** At least one existing capture judgment goes `stale-judgment` or `toothless`
  when the evidence moves from a simulated duplicate to a redelivered message.

---

### Schedulers and eventual absence

#### M4 — A retention purge job over raw location points

- **Concerns.** C18.
- **Domain.** Eventual absence. First instance in the corpus.
- **Enforcement rung.** Not on the ladder — a job, plus a per-store retention policy.
- **Moves.** D12 (liveness and production oracles are in scope) and D4.3 (detection-strength
  evidence requires a detector test). D4.3 is currently a decision with no instance: **no
  detector has ever been tested in this repo.**
- **Prediction.** The claim becomes checkable before release only by shifting the subject —
  plant a record older than the window, assert the scan flags it. If that test reads as testing
  the job rather than the claim, D4.3's framing is wrong.

#### M5 — A reconciliation job over a double-entry ledger

- **Concerns.** C8 (money is conserved) — the catalog's own candidate for the most important rule
  in the system, with no site and no test today.
- **Domain.** Aggregate state over time, in its hardest form: no site realizes the claim.
- **Enforcement rung.** 1 (`schema`) for balanced postings, plus detection for the aggregate.
- **Moves.** **Open question 4 directly** — what does a `realizes` tag mean for a rule with no
  site. This is the only proposed item that answers an open question rather than approaching it.
- **Prediction.** `realizes` will not fit, and the answer will be either that the ledger
  primitive is the site (which is a substitution of a weaker claim, and should be reported as
  one) or that the domain admits no `realizes` at all.

#### M6 — Offer and quote expiry as swept state, not lazily-evaluated state

- **Concerns.** C12; `trips/dispatch`'s `expired-offer-withdrawn` and `pricing/quote`'s
  `expired-quote-is-never-revalidated`, both of which are currently true by evaluating a
  timestamp at read time — the cheapest possible discharge of a scenario that reads as a
  scheduled action.
- **Domain.** Executions, then eventual absence once withdrawal is an event.
- **Moves.** Nothing structural. Included because two existing scenarios are worded as if a
  sweeper exists, and the mismatch between what a scenario implies and what the mechanism does is
  precisely what the design facet is meant to expose. Low cost, and it corrects the record.

---

### Caches

#### M7 — A read cache in front of supply density and driver position

- **Concerns.** C1 (precise position not exposed before accept, and never after terminal), C18
  ("a new store receives location data and is not on the purge job's list"), C6 ("a cache key
  omits the market; two markets share an entry under load").
- **Domain.** A set of sites — the cache is a *store*, so it joins the surface of every
  location-privacy and retention rule at once. This is the catalog's "the surface has never in
  the history of software stopped growing" made mechanical.
- **Enforcement rung.** 1 (`type`) for the key — a key type that cannot be constructed without a
  market and a trip phase.
- **Moves.** Tests whether a derived enumerator of "every store that has received a location
  point" can be produced at all (D13.1). If stores must be hand-listed, D13.2's hole kind fires
  for the first time.
- **Prediction.** Adding the cache breaks at least one existing `rider-view` claim's evidence
  without breaking any existing test. If nothing goes red and nothing goes stale, the
  location-privacy claims are weaker than they read.

#### M8 — Market partitioning enforced by row-level security

- **Concerns.** C6.
- **Domain.** A set of sites (every query), enforced below the application.
- **Enforcement rung.** 2 (`constraint`), via Postgres RLS.
- **Moves.** **Open question 5** — what is tagged when enforcement is a DB constraint. RLS is the
  sharpest form of the question because the policy is in a migration, applies to every query, and
  no application code mentions it. A `realizes` tag has nowhere obvious to go.
- **Cost.** A market column on every table and a session variable in the connection path. Cheap
  in the app, expensive in the migration set — which is the honest shape of the concern.

---

### Enumerated surfaces at the weak rungs

#### M9 — Idempotency middleware over the mutating route table

- **Concerns.** C14 — the catalog's cleanest example of a check whose subject is the set of
  members rather than a behaviour.
- **Domain.** A set of sites, with the enumerator derived from the route table (D13.1).
- **Enforcement rung.** **3 (`middleware`)** — first instance. The interesting property is that
  it is opt-in, which reintroduces the per-site obligation through the back door.
- **Moves.** D7's ladder below rung 2; D13.2's enumerator hole kind.
- **Prediction.** A route registered outside the middleware chain is caught by the derived
  enumerator and by nothing else. If a hand-written test would have caught it just as well, the
  enumerator's value is unproven.

#### M10 — Authorization on every trip-scoped read

- **Concerns.** C5. Same shape as C14 from an unrelated area, which is the catalog's own evidence
  that the shape is real; two instances test that claim.
- **Domain.** A set of sites, enumerator = routes taking a trip id.
- **Enforcement rung.** 4 (`guard`) per handler, deliberately — this is the eternal-bug shape,
  and building it at rung 1 would be the fixture flattering itself again.
- **Moves.** D7's `invariant-breach` at the rung it was designed for; the N−1-breaches defect
  becomes observable by contrast with M2's choke point.

---

### Paired derivations

#### M11 — Surge pricing, specified wrong first

- **Concerns.** C9; D1 names surge as the chosen deliberate-wrong-start.
- **Domain.** Executions, then paired derivations once the recompute path exists.
- **Moves.** **Open question 1** — id semantics under requirement split and merge, expected to be
  where the model cracks. Requires a windowed demand aggregation, which is a fourth mechanism
  class (streaming counters) the fixture lacks.
- **Prediction.** Splitting `quote-amount-integrity` when surge lands will orphan tags, and the
  matrix will report the transition as a hole for some interval. The question is whether that
  interval is tolerable or whether a supersedes relation is needed.

#### M12 — A differential oracle between the quote path and the capture recompute

- **Concerns.** C9 — two independent implementations that must agree; per-path example tests can
  both pass while the paths disagree.
- **Domain.** Paired derivations. First instance.
- **Enforcement rung.** 2 (`choke-point`, the signed quote token).
- **Moves.** Whether the evidence vocabulary can express a metamorphic oracle at all. `Scope` and
  `Quantification` describe how thoroughly evidence ranges; neither says the oracle is
  *comparative*, and the corpus has no field for it.
- **Prediction.** A new field is needed, or `Strength` absorbs it. If neither, D4's classification
  is incomplete.

#### M13 — A generated error-code catalog consumed by both clients

- **Concerns.** C17; overlaps `misc/unclaimed-outcomes.md`.
- **Domain.** Paired derivations, with artifacts rather than executions as the subject — a
  completeness check over a cross-product (code × client × locale).
- **Moves.** Whether "coherence across artifacts" is genuinely the same domain as C9's coherence
  across executions, or whether D13.3's six are five plus one miscounted.
- **Prediction.** This is the likeliest single item to falsify D13.3's closed set.

---

### Batch processing

#### M14 — An analytics pipeline with a deletion-propagation job

- **Concerns.** C3 (deleted PII does not survive), C2 (no raw MSISDN in any trip-scoped table or
  event), C10 (money representation across a language boundary).
- **Domain.** Eventual absence, plus a schema rule over pipeline artifacts.
- **Enforcement rung.** 1 (`schema`) for the MSISDN prohibition; a job for the propagation.
- **Moves.** D1 already budgets an analytics consumer and a pipeline, so this is inside the
  agreed scale cap. It is the only place C10's cross-language boundary becomes real.
- **Prediction.** A derived table that joins on a hashed id the deletion job does not know about
  is invisible to every check the tool has. If the schema rule catches it, the code-artifact
  domain is stronger than the catalog credits it.

---

### Two that need a scale decision first

#### M15 — Proxy contact numbers through a communications choke point

- **Concerns.** C2 — the worked example in `design/README.md` for one rule held up by *two*
  mechanisms (a choke point and a representation constraint). The corpus has no instance of a
  multi-mechanism entry, so the format's own illustrating case is unexercised.
- **Scale.** A communications service exceeds D1's 3–4 service cap. Build it as a module inside
  trips, or amend D1. **Do not quietly exceed the cap.**

#### M16 — A support gateway with an append-only audit log

- **Concerns.** C4 — obligation on every member *plus* a topology constraint (no support
  principal reaches a service except via the gateway).
- **Domain.** A set of sites, with a topology predicate that no current check expresses.
- **Scale.** Same problem as M15, and it also enlarges C1's surface (support screens), which is
  arguably its main value.

---

## Sequence

Ordered by evidence per unit of cost, not by dependency.

| Phase | Items | Unlocks |
|---|---|---|
| A | M2, M1, M3 | code-artifact domain; open questions 2 and 7; first weak-rung mechanism |
| B | M5, M4, M6 | aggregate-state and eventual-absence domains; open question 4; first detector test |
| C | M7, M8 | derived enumerators over stores; open question 5 |
| D | M9, M10 | rungs 3 and 4; the N−1-breaches defect made observable |
| E | M12, M13, M11 | paired-derivations domain; open question 1; the closed-set test |
| F | M14 | cross-language boundary; the second eventual-absence instance |

M2 is first because it is the cheapest instance of an unexercised domain and because C16 is the
concern the catalog is most confident about. M5 is second in value and first in difficulty; it is
the only item that answers an open question outright.

## Not proposed, and why

- **Kubernetes, service mesh, multi-region.** Named as explicit non-goals in D1 and the non-goals
  list. Months of cost, nothing about the artifact model.
- **Event sourcing as the general persistence model.** It would make M2 and M3 trivially true by
  construction, which removes the hard case rather than instantiating it.
- **GraphQL, a second mobile app, a service count beyond four.** No concern demands them.
- **Rate limiting, circuit breakers, bulkheads.** Real mechanisms, but every claim they support
  has the same shape as one already listed (obligation over an enumerated surface). Shape
  diversity is the goal; D1 caps scale for exactly this reason.
- **A second database engine.** Tempting for C6, unnecessary — RLS asks the question at a
  fraction of the cost.

## The cost falsifier this list threatens

One falsifier in `docs/status.md` has never been measured: *artifact and annotation cost exceeds
what the defects justify → ceremony*. Sixteen mechanisms multiply the annotation surface several
times over against a defect count that currently stands at zero application defects attributed to
the agent tier.

Two consequences, and they are conditions on doing any of this:

1. **Each phase records its own cost and defect numbers before the next begins** — artifacts
   written, annotations added, tests written, and defects found that no pre-existing check would
   have caught. The table in `docs/status.md` has the columns; they have been filled in twice.
2. **A phase that produces no adverse verdict and no defect is evidence against the framework,
   not a phase that went well.** Recorded here in advance so that outcome cannot be reread later
   as success.

The failure mode of this phase is the fixture becoming the product. A list of sixteen mechanisms
is exactly what that failure looks like from the inside, and the only defence is that each item
names the domain, rung or open question it exists to instantiate — and is dropped the moment that
one is answered by something cheaper.
