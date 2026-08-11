# Judgments: trips/rider-view

Revalidated 2026-08-10 for rider referral rewards. The derived rider-surface class gained a referral
BFF route and server-rendered referral page; both bodies were inspected and carry no driver field or
position source. The receipt gained payment breakdown rows while retaining its driver name and
continuing to omit position. The invariant enumerator, all realization sites and both stale evidence
bodies were re-read before refreshing these verdicts.

Revalidated 2026-08-10 for routine referral-credit grouping. The edited referral page still obtains
only code, attribution and credit fields from the referral projection; grouping introduces no trip,
driver or position source. All other enumerated rider surfaces, both bound mechanisms and the e2e
body were re-read and are unchanged.

Re-judged 2026-08-10 after D28 exposed realization sources. `RiderProjection` owns phase-dependent
identity and position disclosure; service/BFF view models preserve that decision; pages render only
their bounded models. The site-quantified invariant also includes every built rider surface: a
surface that carries no position discharges the rule explicitly, while position-bearing surfaces
route through the projection.

Revalidated 2026-08-10 after D27 added stable mechanism ids. The semantic diff changed no
enforcement, binding, expectation, rationale, claim, evidence form or source; the prior verdict
rationales therefore remain applicable and only their freshness fingerprints moved.

Rebased 2026-08-08 after criticality entered claim freshness. No level, evidence site, required
form or verdict changed.

Re-judged 2026-08-08 after design bindings and their source entered the freshness fingerprint. The
re-read rejected the earlier proof argument: `DriverPosition.Reveal()` is assembly-internal, so an
unredacted position is representable. The plan now records example evidence plus an accepted
derived-egress residual; the projection guard and every new route discharge were re-read.

Fingerprints refreshed 2026-08-08 after the shared e2e file gained pricing and payment assertions.
Every rider assertion and projection was re-read and the full e2e suite passed. The cancellation
test became stronger by also checking absence of capture; its rider assertions are unchanged.

**Re-judged 2026-08-07 after the evidence, the design and the class enumerator were all corrected.**
The superseded verdicts were one `dishonest-tag`, three `toothless` and two `spec-gap`; each entry
quotes what it replaces rather than deleting it.

**Two of the first pass's verdicts were the judge's error, not the corpus's**, and both had the same
cause: **design prose was taken as fact without checking it against the code.** `azimuth-verify`
says to read test bodies rather than test names; it does not say to read the code rather than the
design, and this pass is the argument that it should.

**Conflict of interest, unchanged and now larger.** The same judge wrote the first verdicts, the
fixes, the design correction and these verdicts. Where a verdict rests on mutation it says so; where
it rests on reading, it says that too.

**Fingerprints refreshed 2026-08-07.** Every verdict below was re-affirmed rather than re-derived:
the evidence files changed for reasons belonging to other specs — tag corrections in shared test
files — and no test body carrying these claims was touched. The fingerprint expired because it
hashes whole files, which D19.1 records.

## Claim: position-confined-to-live-phases
Verdict: sound
Fingerprint: 5d66dd5975bbc940
Judged: 2026-08-11
Judge: codex

*(supersedes `dishonest-tag` — "declares `universal` over the site class while the test hand-lists
five URLs")*

Both halves moved, and the tag was the smaller one.

**The class is now derived.** `invariant-breach` takes membership from Next.js's built route table
via `--next-app`, so a route is a member because it exists, not because someone tagged it. Run
against the corpus it produced eight breaches — five rider surfaces and three driver ones — none of
which any tag-derived class could reach. All eight are now discharged, each with a stated reason:
two forward the trip service's projection unchanged, and the rest carry no driver at all.

**The e2e test is tagged `example`**, which is what named surfaces are. The plan now says the same
thing and records the missing universal predicate analysis as a residual. The class enumeration is
universal over built routes; the truth of each discharge is still an agent judgment. This is sound
because the evidence tag and plan no longer claim the type proof that the code cannot supply.

## Claim: no-driver-identity-before-assignment
Verdict: sound
Fingerprint: accaf4092e2093f1
Judged: 2026-08-11
Judge: codex

Unchanged from the first pass and re-judged only because the file moved. `Before_assignment_no
_individual_driver_is_shown` asserts display, position and vehicle are null at the one phase that is
"not yet assigned", and the e2e adds a substring check that no coordinate appears anywhere in the
payload. Against a projection with `DriverDisplay` unconditional, both fail.

The hard-coded phase remains the weakness: the axis has one member today and the test names it
rather than deriving it, so the tag stops being true silently if a pre-assignment state is added.
The doc comment above it, which claimed derivation this body does not do, is still wrong.

## Claim: no-driver-position-before-assignment
Verdict: sound
Fingerprint: d07777c4068e2071
Judged: 2026-08-11
Judge: codex

Same two tests, same reasoning, and the plan's `e2e` scope is met by evidence that exercises the
assembled path rather than the projection alone. Against `DriverPosition` unconditional the unit
test fails on the null assertion and the e2e on both the field and the substring check.

## Claim: supply-density-shown-before-assignment
Verdict: sound
Fingerprint: 669232e6f7501904
Judged: 2026-08-11
Judge: codex

*(supersedes `toothless` — and part of that verdict was simply wrong)*

The first pass said the substring check on `52.37` "is tagged to the two sibling claims, not to this
one". That is false. The three `covers` calls sit at the top of one e2e test and the whole body is
the evidence for each, so this claim has always had the assertion that no coordinate appears in the
payload. Against a projection that computed density as a driver's position, that fails.

What the first pass got right is that identifiability in a sparse market is never constructed, and
Sibling `design.md` records that density is computed from real positions and is not
differentially private. That is now a plan residual with an accepted reason rather than a verdict:
the claim's "identifies no individual driver" is held by an argument about market density, and the
argument is written down where a reader can disagree with it.

The unit test remains a round-trip and adds nothing. It is not the evidence carrying this claim.

## Claim: driver-shown-after-assignment
Verdict: sound
Fingerprint: b22eb386ffc80c5a
Judged: 2026-08-11
Judge: codex

*(supersedes `toothless` — "no test anywhere checks the vehicle in the positive case")*

`Between_assignment_and_a_terminal_state_the_driver_is_shown` now asserts the vehicle alongside the
display name and position, and the e2e asserts it through the assembled path.

Verified by mutation: `Vehicle: null` unconditional in `RiderProjection.For` was applied and the
test failed. Before the fix the same mutation passed every test in the corpus — the system could
have stopped returning the vehicle entirely and nothing would have said so.

The hand-listed `{Assigned, InProgress}` is unchanged and still the weak point: "once assigned and
until terminal" is derivable as the complement of the terminal set, and a new live phase would
escape.

## Claim: driver-position-follows-driver
Verdict: sound
Fingerprint: 1038fdf624b30233
Judged: 2026-08-11
Judge: codex

*(supersedes `toothless` — "the covering test never changes a position")*

It does now. The e2e moves the seeded driver twice — once after assignment and once after start —
and asserts the rider's view follows each time. The move goes through `psql`, the same way the
driver row is seeded, because a driver's position is fixture data and inventing a backend endpoint
no claim asks for is the failure the driver app's own home page warns about.

Against a projection that cached the first value or returned the seeded literal, the second
assertion fails. The first pass called this structurally blocked, on the reasoning that no fixture
path moves a driver; that was wrong in an interesting way — no *application* path moves a driver,
and the claim does not say the driver moves via an endpoint.

## Claim: no-position-after-completion
Verdict: sound
Fingerprint: 25c4f0fa59aa6371
Judged: 2026-08-11
Judge: codex

*(supersedes `spec-gap` — which was the judge's error)*

The first pass argued the spec was silent about a pushed observation mode, citing
Sibling `design.md`'s second mechanism: `RiderTripStream.Close`, "invoked by the state
machine's terminal transition", covering "what an already-open connection keeps pushing".

**No such type exists.** The fixture has no streaming at all: the rider page polls with
`router.refresh()`, which re-runs the same server-side projection that produced the first render.
The design named a mechanism for a failure mode this system cannot have, and the judgment cited it
as evidence of a gap without checking. The design entry is now corrected, marked.

With that gone, there is one observation mode and the evidence covers it.
`After_a_terminal_state_the_name_remains_and_the_position_does_not` iterates
`TripStateMachine.States.Where(IsTerminal)` — a derived enumeration, so `universal` is honest and a
third terminal state arrives covered — and the e2e completes a trip through the assembled path and
asserts the position is gone.

## Claim: no-position-after-cancellation
Verdict: sound
Fingerprint: b3303c3334b22f5a
Judged: 2026-08-11
Judge: codex

*(supersedes `spec-gap`, for the same reason as its sibling: the gap was in the design, not the
spec, and the design was fiction)*

The scenario-level evidence stands. The unit test constructs `View(Cancelled)` *with* a position and
asserts it is not projected, which is the case that matters. The e2e cancels a trip that was never
assigned, so its null assertion is weak on its own — recorded again because if the unit test were
removed this claim would have no evidence that constructs the failure case.

## Claim: driver-identity-remains-on-receipt
Verdict: sound
Fingerprint: 75c0cbb7f2ca26a6
Judged: 2026-08-11
Judge: codex

Unchanged. The positive half of the terminal rule, and what stops the requirement being satisfiable
by returning nothing. Against `DriverDisplay: assigned ? … : null` — the over-redaction a
privacy-motivated change would produce — the unit test fails on both terminal states and the e2e
fails on the rendered receipt page.
