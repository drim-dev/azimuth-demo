# Judgments: referrals/rewards

First pass, 2026-08-10. Every claim, realization source, covering body and relevant design or
verification section was read after the composed referral journey passed. The pass found one
framework-visible integration defect before judgment: the Trips migration had disappeared during
parallel integration, and fresh composed startup failed until it was regenerated. It also caused
the derived rider-surface class to demand explicit position-invariant discharge from both new
referral surfaces.

## Claim: known-code-is-attributed
Verdict: sound
Fingerprint: 0eef5567bb821b99
Judged: 2026-08-10
Judge: codex

The BFF relation forwards the optional code without interpreting it. `RequestRide` resolves the
code owner inside the admission transaction and inserts attribution beside the first trip. The
component test enters through HTTP, then independently observes the stored referrer and public
`pending` summary. Seeding only the expected attribution or accepting an arbitrary code would fail
the setup-independent lookup and owner assertions.

## Claim: unknown-code-is-rejected
Verdict: sound
Fingerprint: 720d77418fbdf1ac
Judged: 2026-08-10
Judge: codex

`RequestRide` rejects a missing code row before trip persistence. Component evidence observes the
stable ProblemDetails code and then proves neither trip nor attribution exists. The raw
`RiderAdmission` insert shares the transaction, so the subsequent self-referral in the same test
also demonstrates that rejection rolled the eligibility marker back. The BFF site preserves the
backend status/body rather than manufacturing acceptance.

## Claim: self-referral-is-rejected
Verdict: sound
Fingerprint: fa1ad10216f774ad
Judged: 2026-08-10
Judge: codex

The test first obtains the rider's real stable code, then submits that code through HTTP. The
handler compares the resolved account owner—not code syntax—to the requesting rider and rolls back
without a trip or attribution. Replacing the owner comparison with a non-empty-code check makes
this case pass admission and fail both absence assertions.

## Claim: attribution-cannot-be-replaced
Verdict: sound
Fingerprint: c37443b6f2564c5b
Judged: 2026-08-10
Judge: codex

Evidence exercises three closure causes: an attributed first trip, an unattributed first trip, and
eight concurrent first requests carrying different valid codes. The permanent `RiderAdmission`
insert uses `ON CONFLICT DO NOTHING` in the trip transaction, and attribution has independent unique
referred-rider/first-trip indexes. Each ordering leaves one admission and at most one attribution;
an `AnyAsync` pre-check without the ledger fails the contention relation.

## Claim: no-reward-before-capture
Verdict: sound
Fingerprint: bd36ce2c1626cfdc
Judged: 2026-08-10
Judge: codex

The component test traverses every trip lifecycle state with an attributed rider and no payment
fact, resetting real Postgres between states, and observes zero participant credits. The composed
test repeats absence after admission. `ConsumePaymentCaptured` is the only repository path that
constructs source credits and is invoked only by the payment queue, so its choke-point realization
is honest; trip completion itself carries no reward write.

## Claim: first-capture-awards-pair
Verdict: sound
Fingerprint: 0969adb11319081f
Judged: 2026-08-10
Judge: codex

The e2e moves an attributed trip across two transactional outboxes, real RabbitMQ and both services,
then observes one available credit in each public summary and qualification on the invitee. The
component handler test independently proves the two beneficiary identities and fixed amount. The
accepted example residual is honest: identity/currency repetition would not widen the uncertain
cross-process handoff, while multiplicity is covered by its own universal claim.

## Claim: capture-redelivery-does-not-duplicate-reward
Verdict: sound
Fingerprint: 5064f81e01507d8e
Judged: 2026-08-10
Judge: codex

Evidence sends one capture seven times through the real broker, then calls independent handler
scopes concurrently with repeated logical capture identity. It asserts exactly two source credits
and the two expected beneficiaries. The event inbox settles exact redelivery, the trip row lock
serializes the logical capture, and `(attribution, beneficiary)` uniqueness is the final guard.
Counting deliveries or generating a new source on retry increases the asserted rows.

## Claim: owned-credit-reduces-capture
Verdict: sound
Fingerprint: 30cc173010de95c6
Judged: 2026-08-11
Judge: codex

`RequestRide` locks and validates ownership, state, currency and fare before signing the reservation.
`CaptureTrip` authenticates the exact trip/credit/amount/currency and derives the only supported
adjustment. Component evidence ranges fare and credit across EUR, USD and JPY, relating provider,
capture, typed status and outbox values. The BFF/view/receipt relations preserve those typed facts;
the e2e independently satisfies `captured = original - credit` and later observes the same credit
as used.

Mutation review found that generated inputs had not actually exercised the spec's “no greater
than” equality boundary: rejecting a credit equal to the fare survived. The component test now
forces that case for every currency and the refreshed run kills the boundary mutation. The two
remaining survivors change post-capture dispatch bookkeeping and rejection of a zero credit; zero
is not an available referral credit under this scenario. The no-coverage contention paths are
covered by the separate at-most-once and unavailable-credit claims.

## Claim: unavailable-credit-is-rejected
Verdict: sound
Fingerprint: 4497841915ec144d
Judged: 2026-08-10
Judge: codex

The HTTP component test ranges unknown encoded ids, foreign ownership and each stored credit state.
The row is selected `FOR UPDATE`, and every invalid shape produces a distinct stable refusal before
trip persistence. The handler is the authority; the rider UI merely filters selectable currency for
convenience. Removing the owner/state checks or moving reservation outside the transaction creates
a trip or changed row and fails the evidence.

## Claim: forged-credit-authority-is-rejected
Verdict: sound
Fingerprint: 0cea786d3efb35fb
Judged: 2026-08-10
Judge: codex

Starting from one valid authority, the test mutates encoded body, signer, trip, currency and amount.
Every transformed intent is quarantined with the stable invalid-authority code, creates no capture,
and makes zero provider calls. `CaptureTrip` verifies the HMAC before returning a payload and then
checks every quote/trip binding; the realization therefore owns rejection rather than merely token
parsing. The metamorphic tag matches the body.

## Claim: capture-redelivery-does-not-redeem-twice
Verdict: sound
Fingerprint: 09fa7cb88017393b
Judged: 2026-08-10
Judge: codex

Payments evidence runs eight concurrent dispatchers and retains one capture/outbox for the credit.
Trips evidence releases a reservation on cancellation, reserves it on a later trip, then processes
eight distinct event ids concurrently and retains one `Used` row bound to that trip and capture.
The two realization handlers honestly own the two-store halves. No distributed transaction is
claimed; redelivery repairs the asynchronous boundary.

## Claim: referral-summary-explains-state
Verdict: sound
Fingerprint: 882fbf044e4cd4b5
Judged: 2026-08-10
Judge: codex

The Trips projection ensures one stable account under concurrent summary calls, derives named
attribution state, and lists every credit with encoded id, amount, currency and textual state. The
BFF forwards that contract; the request form and server-rendered page render the same bounded fields.
The composed test opens the production page after redemption and observes its currency and `used`
text. The page now groups the complete list through the three closed credit states; unlike the
rejected filter proposal, it omits no nonmatching credit. Removing a group or filtering the source
list globally would violate the realization even though the accepted example evidence might not
observe every state. Color is styling only, so every realization relation establishes an
identifiable part of the scenario.
