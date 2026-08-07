# Proposal — make "which claims must exist" checkable over a derived outcome surface

**Status: proposed, not decided.** Prompted by a question the corpus could not answer objectively on
2026-08-07: do request-validation rules belong in a spec? The framework has no rule, so the answer
was taste.

## The gap

Azimuth is strong on *given a claim, what does it owe* — criticality gates facets, facets generate
holes, `standards.md` fixes required evidence. It says nothing about **which claims must exist**.
That silence is where the remaining subjectivity lives, and it is not a local oversight: deciding
what ought to be specified is a normative act, and no derivation from code can produce it, because
code is what *is* true.

What can be made objective is narrower and still worth having: **coverage of a surface the code
already defines.** You can show that every client-visible outcome has been considered. You cannot
show that the rules chosen for them are the right rules.

## The proposal

A claim whose domain is **the code artifact itself** — D13's third domain, already in the closed set
(D13.3) — and whose members are the outcomes a client can distinguish. For this corpus that surface
is the refusal codes: the thing a client branches on.

**An outcome that no claim names is a hole.** Same shape as `invariant-breach`, which reports a
member of a site class that discharges nothing.

**It needs no new artifact type and no spec change.** In particular, scenarios must not start naming
error codes — `specs/README.md` requires them to be declarative, "THEN the request is rejected", not
mechanical. The linkage already exists in the other direction: `Realizes` on the member that emits
the outcome. `RequestRide.RequestHandler.Handle` carries eight `Realizes` tags and emits four
refusals, so its outcomes are claimed. `Common/Exceptions/DomainExceptionHandler` emits
`validation:request:validate:invalid` and carries none, so that outcome is claimed by nothing —
which is exactly the question that started this.

## The measurement

Distinct client-visible refusal outcomes in the corpus, 2026-08-07:

| | Count |
|---|---|
| Total distinct outcomes | 19 |
| Asserted by some test | 10 |
| **Never asserted anywhere** | **9** — seven `not_found`, `trip:trip:transition:state_moved`, `validation:request:validate:invalid` |

By kind: 8 domain rules, 10 `not_found`, 1 validation.

Two of the nine are worth a second look on their own. `state_moved` is a domain rule about a
concurrent transition losing a race, and no test asserts a client ever sees it.
`validation:...:invalid` is the one this investigation started from.

**The size of the original question is now a number.** "Do validator and lookup rules belong in a
spec" is a decision about 11 outcomes, not a matter of feel.

## The obstacle, which is the real content of this proposal

**The enumerator must come from the project, not the tool.** `CLAUDE.md` forbids domain vocabulary
in `tools/`, and "an error code looks like `a:b:c:d`" is fixture convention. The tool cannot derive
this surface without knowing something about the project. The shape that fits the existing design is
the one `covers` and `realizes` already use: the project's extractor emits an `outcomes` array into
the manifest, and the tool consumes it and cross-references `Realizes`. How the array gets filled is
the project's business.

**The naive enumerator is already demonstrably unsound**, which is D13.2 arriving with a concrete
instance rather than as a principle. A scan for string literals finds 15 outcomes. It misses four,
because `RequestRide.cs:156` builds them by interpolation:

```csharp
new(message, $"trip:request:create:{reason}");
```

Those four are the most-tested refusals in the corpus and a literal scan reports none of them. An
enumerator that silently returns 15 of 19 is worse than no rule at all — D13.1's argument, met in
practice on the first attempt at building one. This one was caught by accident, which is not a
method.

## The limitation, stated

**Member-level linkage is the weakest rung.** `Realizes` sits on a method, and a method emitting
four outcomes carries tags that say "this site realizes something", not "this outcome is claimed". A
handler could grow a fifth refusal and remain green. That is the same weakness `tools/azimuth/`
records for `invariant-breach` — it verifies a guard at every site, and crediting a choke point
needs call-graph analysis (D10.1). The proposal inherits it rather than fixing it.

**The surface is refusals only.** Success payloads, response field sets and published events are
equally client-visible and are not covered. Refusals are the cheapest surface to enumerate, not the
complete one.

## Falsifier

Apply it and count what it flags. **Prediction, recorded before the evidence:** it will flag 11
outcomes, the team will immediately decide that `not_found` never needs a claim of its own, and 10
of the 11 will be dismissed as a category rather than considered one by one.

If that happens the domain is drawn too wide and the check is noise — the correct response would be
to exclude a *kind* of outcome by policy (decide the boundary once), not to add a check that
produces a list nobody reads. If instead the dismissal is contested, or `state_moved` and
`validation:...:invalid` turn out to be real gaps, the check earns its place.

Either result is informative, which is the argument for building it before arguing further.

## Relation to the other open questions

This is not the multi-axis quantification concern (`quantification-review.md` §6) and not the
site-class enumeration finding (`site-class-evidence.md`), though it shares machinery with the
second: both are claims over a derived domain where the enumerator is the load-bearing part. Three
instances of "the enumerator is the thing that can be wrong" now exist — sites, terminal states,
outcomes — which is more support for D13.1 and D13.2 than for any new notation.
