# Design: ride-hailing-delivery-alerts

## Authority boundary

The demo-local change owns domain intent and the interpretation of application evidence. The
generic change owns the observation envelope, binding roles, parser behavior, adapters and
synthetic validation. This follows D33 and D34: source location does not grant model authority, and
one active transition cannot be observed under two repository authorities.

The generic outcome may cite this change as field provenance. That citation is immutable evidence
that the generic protocol survived a structurally different domain case; it does not let a
canonical build, test, release or acceptance step read the demo checkout (CAR17).

## Migration order

1. Establish this proposal with the exact already-applied intent delta.
2. After approval, move rather than copy the claim-specific verification and transition account.
3. Mark revisions in the generic proposal, plan and outcome; do not silently rewrite its history.
4. Compare accepted model and extracted relations before and after the move.
5. Leave both changes active. Acceptance and repository transfer belong to later graph nodes.

The temporary duplicate delta exists only while this proposal is under review. Completion requires
that the generic copy be removed.

## Framework-only finalization dependency

After the split, `generic-assurance-observations` has no product intent delta. The current local
finalizer requires at least one applied parsed delta, so that change cannot be finalized merely by
removing the ride-hailing delta. This proposal records the limitation but does not weaken the
finalizer or invent unrelated intent. Canonical acceptance must supply an honest framework-change
completion path.

## Failure boundaries

- A model, rule, rule-test, judgment or source-manifest change means the split altered behavior and
  fails this proposal.
- Leaving claim-specific verification or affected-claim declarations in the generic change leaves
  mixed authority and fails CAR16.
- Removing all field-validation provenance from the generic outcome loses a finding that justified
  D39; an immutable citation is required.
- Retaining the same intent delta in both changes after implementation makes transition ownership
  ambiguous and fails completion.
