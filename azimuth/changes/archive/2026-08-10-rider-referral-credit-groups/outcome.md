# Outcome: rider-referral-credit-groups

Status: accepted

## Departures

The first candidate filtered the summary by a query parameter. Impact review found that this would
contradict the existing standard `rider-sees-referral-state` claim: a filtered summary deliberately
hides credits while the accepted claim says the summary shows every credit. The implementation was
revised before acceptance to group the complete history under three state headings. This preserved
the scanability objective without weakening or rewriting the standard claim.

The first two composed-test runs matched raw server HTML across dynamic React text boundaries.
React inserted hydration comments between those text nodes, so the behavior rendered correctly but
the formatting-dependent assertions failed. The accepted test checks stable accessible structure
and removes React comment markers only when comparing visible text.

## Residual decisions

Grouping is performed over the complete in-memory summary because the fixture has no history
pagination. If the credit history becomes paginated, grouping must move to a contract that can state
whether counts and empty groups apply to the page or to the complete history.

The groups remain presentation only. If selecting or omitting a group starts to authorize
redemption, drive support decisions, or hide a state by default, the affected requirement should be
raised from routine before that behavior is accepted.

## Measurements

- The accepted model grew from 85 to 87 scenario claims: two routine requirements with one scenario
  each. Both are reported by `azimuth change check` as `applied · intent only`.
- The routine claims added zero `Realizes`, zero `Covers`, no design entry, no verification entry and
  no judgment. Their real-process rendering test is intentionally untagged ordinary project
  evidence.
- Before this outcome, the change record was 85 lines: 56 proposal, 21 intent delta and eight plan.
  Applying the delta added the same 21 normative lines to the accepted spec. The proposal is longer
  than an ordinary change because this run pre-registered a framework falsifier and retained the
  rejected filtering shape as experimental evidence.
- The page already realized one standard referral claim and one critical rider-surface invariant.
  Editing that site expired exactly those two judgments after the routine test was isolated in its
  own function. Both were re-read and refreshed. This is collateral assurance for existing claims,
  not an obligation attached to either routine claim.
- The first implementation placed routine assertions inside a tagged critical e2e function and
  expired six judgments. Moving them to a separate untagged function reduced the genuine impact set
  to two. Symbol-bound freshness therefore prevented a file-wide judgment cascade when used
  correctly.
- The agent tier changed the product once: it rejected filtering because it violated an accepted
  standard claim. The machine tier then confirmed the revised additions were intent-only and the
  87-claim model had no holes.
- Pre-registration through the first validated, hole-free revised implementation took 14 minutes of
  measured wall-clock time. That includes one rejected design, implementation, two full-suite test
  diagnostics and the final focused composed rerun; it is not an estimate of typing time.
- Final evidence includes rider type checking and production build, 12 real-process e2e tests, 84
  application component tests, 44 extractor tests, both monitoring rule suites and the 87-claim
  Azimuth check with zero errors or warnings.
