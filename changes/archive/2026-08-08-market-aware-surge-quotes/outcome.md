# Outcome: market-aware-surge-quotes

Completed: 2026-08-08

Base commit: `f4f6fda4f3d14483631242e3f4f86a3fb9a99cc6`

Final implementation commit: not available; this turn was not authorized to create one.

Model fingerprint: SHA-256
`0f66592c354ed84ab744c28d6115f7f1e0ecf06db996c91d0131b50827c0bc11`

Verification result: `scripts/check.sh` passed. The derived model contains 61 claims in seven specs
with zero errors and zero warnings; component suites passed 7 Pricing, 34 Trips and 11 Payments
tests, and all 10 real-process e2e tests passed.

## What shipped

- Pricing is a separately hosted vertical-slice service with persisted pressure observations and
  immutable quotes.
- `surge-v1` adds an integer 20% component for fresh demand above supply.
- HMAC-signed quote tokens carry identity, lifetime, policy, pressure, currency, ordered components
  and total.
- Trips verifies the token, stores the accepted quote and enforces consume-once with
  `ux_trip_quote`.
- Completion writes the quote token to `capture_intents` in the state transaction.
- Payments verifies and re-sums the token before provider I/O; adjustments now change the amount
  and require a recorded reason.
- The rider BFF talks to Pricing for quotes and the UI only renders returned components.
- D20 is implemented across core, both emitters and both annotation packages. Routine tests need no
  marker or exemption.

## Departures from the proposal

- The proposed verification file put `surge-is-a-quote-component` at e2e scope. The accepted plan
  uses universal component-contract evidence plus an e2e example. A universal e2e tag over one
  path would have been dishonest; the composition example is additional evidence, not the floor.
- The proposal called for a separate mutation run that omits surge. The implemented discriminating
  evidence carries a non-zero surge through capture and alters every signed byte in codec tests;
  omitting surge fails the amount relation. No mutation-runner mechanism was added for one concern.
- Completing the chain exposed an existing missing mechanism, so the change built the transactional
  capture outbox rather than leaving the earlier design fiction in place.
- `outcome.md` was not in the provisional five-file shape. Completion needed a natural place for
  measurements, departures and the model fingerprint; this is evidence that an outcome artifact is
  a missing lifecycle concept, not optional proposal prose.

## Findings that changed the result

The agent-tier pass changed four things before acceptance:

1. The serviceability rule accepted every non-empty pickup while the spec named serviced markets.
   Pricing now admits only the fixture's `downtown` market and e2e refuses `outside`.
2. Payments component tests claimed completion behavior after directly seeding an intent. Those
   tags were removed; completion and pre-completion now run through real Trips and Payments
   processes.
3. The no-fee cancellation test used a helper that did nothing. Its claim moved to an e2e that
   actually cancels and observes no capture.
4. An “adjustment” only recorded a reason and never adjusted money. Dispatch now carries an explicit
   delta with its reason, and evidence compares the captured amount with the unadjusted quote.

The first multi-service startup also found that both services could migrate the shared outbox first.
Both initial migrations now create it conditionally, while only Trips drops the Trips-owned table.

## Process measurements

Authoring time was not instrumented, so no retrospective minute count is invented.

| Level | Framework-only work | Manual linkage | Findings |
|---|---|---:|---|
| routine | existing spec plus change-history mention; no design, verification or judgment entry | 0 | D20 removed the need for `Untraced`; the breakdown rendering remains ordinary code/test behavior |
| standard | existing quote lifecycle plan and refreshed judgments moved to the Pricing process | included below | serviceability mismatch fixed during judgment |
| critical | intent delta, solution design, verification deviations/residuals and judgments | included below | three toothless/mis-scoped payment links and the inert adjustment changed code/evidence |

Across standard and critical claims, 14 new `Realizes` and 16 new `Covers` tags were added; three
old `Covers` tags were removed after judgment showed they claimed behavior their tests never ran.
Routine added none. The change introduced no domain field used only by Azimuth.

Files touched only for Azimuth were the active/archived change record, current spec/design/
verification/judgment updates, D20 core/emitter/annotation changes and their synthetic tests. The
service, schema, UI and behavioral-test files are product or product evidence rather than framework
ceremony.

Information intentionally duplicated: claim ids at linkage sites and the accepted critical design
mechanisms. Amount formulas, routes, evidence inventories and current implementation file lists were
not copied into a machine-parsed change format.

## Lifecycle observations

Manual steps that are derivable and should be automation candidates after a second change:

- project accepted deltas from the change into current facets;
- detect that every plan item and completion condition is settled;
- compute the model fingerprint and final verification summary;
- move an accepted completed change to a date-prefixed archive path.

Not yet shown to be derivable: explaining departures, classifying a finding, accepting residual
risk, or deciding that a universal e2e tag would be dishonest.
