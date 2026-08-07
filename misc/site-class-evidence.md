# A behavioural test cannot be universal evidence for a site-class claim

**Status: finding confirmed and proposal applied.** The e2e remains an `example`; Next route
membership is derived from the build and fails closed without an enumeration witness. On
2026-08-08 an untagged compact-summary route produced `invariant-breach` before its discharge was
added. The historical argument follows because it states the prediction the experiment confirmed.

## The finding

`trips/rider-view#position-confined-to-live-phases` is the corpus's only claim whose domain is a set
of sites rather than a set of executions. The spec states how membership is fixed:

> Membership is derived from what the code built: a new rider-facing site joins the class by being
> written, without anyone remembering to add it.

Its covering evidence is one e2e test, tagged `universal`, which visits **five hand-written URLs** —
the trip API view, the receipt API, the trip service's raw driver route, and two rendered pages.

Those two sentences cannot both be satisfied by a test. A test reaches sites by naming them. The
next rider-reachable surface — a support view, an analytics export, a push payload — is a member of
the class the moment it is written and is not visited by any test until someone remembers to add it,
which is the exact failure the claim exists to prevent and which the plan's
`rider-reachable-surface` residual predicts in those words.

**The general form: for a claim over a derived domain, a behavioural test can only ever be evidence
about named members.** Not because the tests are weak. Because naming is how tests reach things.

## Why this is not fatal to the claim

The class *is* derived elsewhere. Membership comes from `Realizes` tags, and `invariant-breach`
reports a member that discharges nothing (D13.1, D13.2). And `design/trips/rider-view.md` carries
two mechanisms at the top of the enforcement ladder: `DriverPosition` has no serializer, and no
rider-facing route returns a position at all.

So the claim's universality is real and it rests on **the derived enumerator plus the mechanism**,
not on the test. The e2e test is a five-member regression over the surface that historically leaked,
and is valuable as exactly that.

## Proposal — retag, do not enlarge *(proposed, not decided)*

Tag the e2e test `example`. It is one: five named members of a class whose membership is derived.
Then the claim's `universal` requirement is met where it is actually met — by proof-strength
mechanism under D7, which needs no test at all.

Writing a bigger test is the wrong response, and specifically the response D13.1 forbids: a
hand-listed enumeration of a derived domain reports green over an unknown fraction, and a longer
hand-list reports green over a slightly smaller unknown fraction.

**What it costs:** the tag drops below the `critical` floor, so `wrong-form` fires unless the plan
carries the deviation or the mechanism is recorded as the covering evidence. That is the right
argument to have, and having it in the plan is the point.

**What would falsify the finding:** an extractor that derives rider-reachable surfaces from the
route table and the page manifest, and drives each one. That would make the test's enumeration
derived rather than hand-written, and the `universal` tag honest. It is buildable — D13.1 names the
route table as exactly the kind of source an enumerator should come from — and nothing in the repo
attempts it. Until it exists, the finding stands.

## Relation to the quantification question

This is *not* the multi-axis concern from `quantification-review.md` §6, and §6.5 records the failed
prediction that it would be. That one asks which axis of a multi-axis execution space was ranged
over. This one asks who enumerates a domain — the test or the model. Adjacent, and different.

If they ever converge it will be through a shared observation: **the tag reports a property of the
evidence, and in both cases the interesting fact is a relationship between the evidence and the
claim's domain.** One instance each is not enough to build notation on, and `AGENTS.md`'s rule that
mechanisms need ≥2 structurally different concerns is doing real work here — it is the reason
neither has become a field.
