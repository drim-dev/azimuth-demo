# Change: rider-referral-credit-groups

Status: **accepted and complete**

## Problem

The referral summary presents every credit in one list. Once a rider has credits in different
states, finding the credits that remain available or confirming which were used requires scanning
the complete history.

## Scope

Group the complete referral-credit history under available, reserved and used headings. Name empty
groups so the rider can distinguish “none in this state” from a missing or still-loading section.

This change does not alter credit state, counts, ordering within a state, redemption eligibility,
the summary API, or the request form's credit selector.

## Revision after preflight

The first proposal used status filters. Agent-tier review rejected that shape before acceptance:
the existing standard `rider-sees-referral-state` requirement says the summary shows every credit,
while a filtered summary deliberately hides nonmatching credits. Grouping retains every credit and
still improves scanning. This revision changes both routine claim ids rather than silently changing
their meaning.

## Criticality rationale

Both requirements are routine because they organize and explain an already-authorized public
projection. Incorrect grouping can inconvenience the rider, but it cannot create, reserve, redeem,
omit from the underlying response, or change the value of a credit.

Raise `credit-history-is-grouped-by-state` if grouping becomes an input to redemption or hides any
state by default. Raise `empty-credit-groups-are-named` if absence begins to drive a business or
support decision outside presentation.

## Completion

- available, reserved and used groups are always named;
- every credit appears in exactly one group matching its state;
- an empty group explicitly says that it has no credits;
- the presentation does not rely on color;
- implementation evidence remains ordinary project evidence with no Azimuth linkage;
- accepted intent is applied and the change is archived.

## Process prediction

The Azimuth-specific path should require only this proposal, one intent delta, the lasting spec
addition, a short plan/outcome and mechanical finalization/archive. It should require zero design,
verification, judgment, `Realizes` or `Covers` additions for the routine claims. The routine-path
claim is falsified if those claims directly require any of those artifacts or materially complicate
the product implementation.

The edited page already realizes standard and critical claims. Their existing judgments may expire
and require bounded impact review; that work is attributable to preserving existing assurance, not
to new routine-claim obligations, and will be measured separately.
