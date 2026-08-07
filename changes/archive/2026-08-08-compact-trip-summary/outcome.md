# Outcome: compact-trip-summary

Status: accepted

## Result

The rider application now has a compact trip-summary page showing trip identity, current state and
quoted fare. The routine scenario added no `Realizes`, `Covers`, design, verification or judgment
entry of its own.

The page is nevertheless a member of the existing critical rider-visibility domain. Built without
any linkage, the Next route enumerator produced:

```text
app/web/rider/src/app/trips/[id]/summary/page.tsx: error invariant-breach
    `/trips/[id]/summary` is in the class and discharges nothing
```

Adding the invariant discharge closed that hole. Four additional `Realizes` declarations record
the page's semantic relationship to existing behavioural privacy claims; the enumerator could not
derive those relationships.

## Departures

No application behavior departed from the proposal. The implementation added four behavioural
privacy links beyond the one site-class discharge forced by the machine. They were not obligations
of the routine summary claim; they attach to existing critical claims whose predicates also hold at
the new page.

The framework work surrounding this feature also introduced typed design bindings and lifecycle
commands. Those are framework mechanisms requested alongside the validating feature, not product
scope silently attributed to the routine requirement.

## Residual decisions

The route enumerator establishes membership in a mechanically closed Next route surface. It does
not establish that the selected app is the complete rider-reachable estate, nor infer which
behavioral requirements a route realizes. Those remain project selection and agent judgment.

A .NET symbol binding proves that a named symbol exists. It does not prove “only caller,” complete
authentication, or transactional semantics. Migration-derived index bindings additionally compare
uniqueness, ordered columns and predicates because those properties are mechanically available.

Change projection supports additive intent deltas. An unsupported operation fails parsing instead
of being approximated. Replacement, removal, scenario movement and criticality transition syntax
remain absent until a third change requires one of them.

## Measurements

Authoring time was not instrumented, so no retrospective duration is claimed.

| Quantity | Result |
|---|---:|
| routine scenarios added | 1 |
| linkage for the routine scenario | 0 |
| cross-cutting `Realizes` declarations at the new route | 5 |
| new `Covers` declarations | 0 |
| derived members added to the rider route class | 1 |
| machine findings before discharge | 1 `invariant-breach` |

The machine-tier finding was not reproducible by the routine scenario matrix: the new scenario had
no linkage obligation. It came from the build-derived site domain, which is the discriminating
result this change was selected to produce.
