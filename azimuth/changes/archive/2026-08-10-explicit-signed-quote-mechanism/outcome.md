# Outcome: explicit-signed-quote-mechanism

Status: accepted

## Departures

- The implementation changed although the proposal began as an explicit account of an existing
  control. Position-complete signature mutation found that .NET accepted non-canonical base64url
  aliases: changing the final encoded character could decode to the original signature bytes and
  remain valid. Decoding now requires the input to equal the canonical re-encoding.
- The old single-payload tamper example became three generated properties rather than being retained
  beside them. Its useful relation is subsumed by the stronger position sweep.

## Residual decisions

- Mechanism implementation and mechanism evidence do not establish complete application. Trips and
  Payments demonstrably invoke validation, but no independent boundary enumerates every place that
  ought to invoke it. No hand-written consumer catalog was introduced.
- Pricing, Trips and Payments retain one symmetric key. A validating process can mint tokens;
  rotation, revocation, secret storage and authority separation remain outside the demonstrated
  mechanism.
- `Universal` ranges over generated payload dimensions, emitted token positions and distinct
  authorities. It does not mean exhaustive enumeration of all strings, integers or keys.

## Measurements

- One critical requirement added three claims. The accepted model moved from 68 claims in eight
  specs to 71 claims in nine specs and is hole-free.
- The integrated repository run executes 215 tests: 98 core, 42 extractor, 65 service/component
  and 10 composed-stack e2e tests, plus five Prometheus rule-test cases.
- The compiled manifest contains 132 realization sites, 61 claim-evidence sites, two mechanism
  implementation sites and four mechanism-evidence sites. The new code carries 13 claim/mechanism
  relations: four `Realizes`, three `Covers`, two `ImplementsMechanism` and four
  `CoversMechanism` declarations.
- One existing test became three tests. The new suite covers three explicit payload boundaries plus
  generated payloads, every non-delimiter token position, every alternative final-signature
  character and guaranteed-distinct authority keys.
- One product defect changed implementation: non-canonical signature encodings were accepted. This
  was observed as a failing test before the canonical decoder check was added; it was not an
  injected mutation.
- The agent tier added three sound judgments and re-read eleven older critical claims made stale by
  the shared codec/design change. No adverse judgment remains.
- Before outcome and derived finalization, the change added 125 lines of accepted intent, design,
  verification and judgment, plus 107 lines of proposal, design, plan and verification transition
  material. Authoring minutes were not measured, so the ceremony-cost falsifier remains open.
