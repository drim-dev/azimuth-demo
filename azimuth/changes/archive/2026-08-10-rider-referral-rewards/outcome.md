# Outcome: rider-referral-rewards

Status: accepted

## Departures

Referral ownership stayed inside Trips rather than becoming a new service. Research showed that a
standalone boundary would first need another complete rider/trip projection or a synchronous
eligibility dependency, neither of which tested the intended reward and payment risks. The module
still communicates with Payments only through signed lifecycle facts and brokered capture facts.

The original admission sketch used a prior-trip query. Team review rejected it before coding
because two concurrent first requests can both observe absence. The implementation added a
permanent `RiderAdmission` ledger with conflict-settled insertion in the trip transaction.

The existing generic dispatch adjustment was removed, not retained beside referral credits. It
would have left a second unauthorised path to the same money predicate and made the new authority
claim false. Payment capture now accepts only its per-intent signed credit authority.

`PaymentCaptured` means a capture recorded by this system of record. The fixture still treats an
unobserved provider result as possibly captured and has no processor reconciliation. Calling the
event externally confirmed would have overstated the existing provider seam.

A server-rendered referral summary page was added during integration. The client card was
functionally complete, but the repository's HTTP e2e harness could not execute hydration and
therefore could not honestly observe its named credit states. The page makes the same public
projection executable evidence without adding a browser dependency.

One Trips migration disappeared during parallel integration. Isolated component suites still
passed against their fixture lifecycle, while the fresh composed stack failed before Trips opened
its port. Visible startup reproduction identified pending model changes; the complete feature
migration was regenerated and the composed suite then passed. This is a positive finding for the
separate composed rung and a coordination defect the work-package DAG alone did not prevent.

## Residual decisions

Rider identity remains a caller-controlled, session-stable fixture string and is explicitly labeled
as unauthenticated. Production referral codes and balances require authenticated account identity.

Credits are fixed at 500 minor units in the qualifying capture's currency. They do not expire, split,
transfer, restore after a void/refund, post to a general ledger, or model tax. A credit greater than
a later fare is refused rather than partially consumed.

Trips and Payments share a symmetric referral signing key. The mechanism protects against callers
without the key; it does not separate minting authority between the two services. Key storage,
rotation and revocation remain deployment concerns.

Outbox confirmation, backlog and dead-letter rules establish repository-owned detection inputs.
They do not prove a deployed RabbitMQ permission model, Prometheus scrape, Alertmanager route or
operator response. External provider reconciliation remains the payment design's existing residue.

## Measurements

- The accepted model grew from 71 to 85 claims: four referral requirements with 12 scenarios and
  one payment-publication requirement with two scenarios.
- The implementation used one coordinator and three package agents. Research ran in parallel;
  shared contracts were frozen; Trips, Payments and rider-surface packages then ran concurrently;
  integration and judgment formed the join wave.
- New target claims have 28 realization relations, 19 covering relations and three derived
  mechanism-implementation relations. Critical evidence includes universal direct, relational and
  metamorphic forms plus one accepted composed example residual.
- New change/current documentation selected for this feature totals 759 lines before this outcome;
  lasting edits also refreshed six existing judgment files and payment design/verification. This is
  materially more ceremony than a routine OpenSpec-like change, but it accompanies 14 cross-service
  claims, two money/concurrency mechanisms, broker topology, monitoring and agent judgments—not a
  routine requirement.
- Authoring minutes were not instrumented reliably across concurrent agents; this remains a missing
  measurement rather than an invented number.
- Findings that changed the result: concurrency-safe first admission; removal of caller adjustment
  authority; explicit system-of-record capture semantics; executable rendered-summary evidence;
  regenerated composed migration; two new rider surfaces forced through the existing position
  invariant; and three critical verification forms corrected before acceptance.
- Full validation passed 84 application component tests, 44 extractor tests, 11 real-process e2e
  tests, both Prometheus rule suites, all core tests, both production web builds and the final
  85-claim Azimuth check with zero holes or warnings.
- Finalization and archive remain mechanical CLI operations. The substantive decisions, departures
  and residuals could not be derived and were authored here.
