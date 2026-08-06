# Verification: trips/rider-view

Every claim in this spec is realized in the rider client, the rider BFF and the trip service. The
entries below exist because that is exactly the shape where per-site evidence is misleading.

## Claim: no-driver-position-before-assignment
Scope: e2e

Each site can pass in isolation while the composition leaks: the service omits the field, the BFF
projects a different model, the client renders a cached value from an earlier poll. The claim is
about what the rider can observe, and only the assembled path observes that.

## Claim: no-position-after-completion
Scope: e2e

Same composition risk, plus a state boundary. The interesting failure is a stream that was
correct while the trip ran and is never torn down.

## Claim: driver-position-follows-driver
Scope: e2e

The claim is about propagation across three processes. Verifying it at any one of them verifies
something else.

## Residual: rider-reachable-surface
Accepted: deliberately, for now — the steel thread is built without cross-cutting notation so that
we learn whether the per-scenario matrix notices this class of leak; if it does not, and it should
not, that is the primary evidence for what notation to add next

These claims constrain three named surfaces. They do not constrain the *next* one — a receipt
endpoint, a support view, an analytics export, or a push payload that includes a position field
would satisfy every claim in this spec and violate the rule the spec exists to express. Concern C1,
whose domain is a set of sites rather than a behaviour.
