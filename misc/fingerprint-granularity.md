# Fingerprint granularity — a question D19.1 raised and did not answer

**Status: the under-firing half was fixed on 2026-08-08; over-invalidation remains open.** Judgment
fingerprints now include effective verification files, applicable design and the source files of
machine bindings. That deliberately expired all 60 recorded verdicts once. The normalized-body and
split-fingerprint options below remain undecided because file-level over-invalidation still has no
measured correctness cost.

## What happened

D19 renamed a tag value. Editing 64 `covers` tags — 47 C#, 17 TypeScript — changed nothing about any
claim, any test body, or any verdict. `stale-judgment` went from 10 to 18: **8 of 18 judgments
expired on a change that altered no meaning.**

The cause is not a defect. D18's fingerprint hashes the claim text and the *content of every file*
carrying evidence for the claim. A file whose bytes changed is a file the judgment did not see, and
the fingerprint cannot tell a rewritten test from a renamed token.

## The other half, found 2026-08-07 — it under-fires as well

The fingerprint covers the claim text and the evidence files. **It does not cover the verification
plan.**

Measured: a `Scope: component` entry was added to `verification/trips/request.md` for
`request-admitted-after-terminal`, changing that claim's required form from the inherited `unit`.
The fingerprint before and after is byte-identical — `42dfddab6442d5f1` — and the verdict stayed
`sound` without being re-examined.

That is the more serious direction. A judgment is a claim that *this evidence meets that standard*.
When the standard moves, the verdict is about a comparison nobody has made. Raise a claim's required
quantification from `example` to `universal` and every existing `sound` judgment stands
unchallenged, having been reached against a weaker requirement — and unlike the rename case, nothing
in the output tells a reader to look.

So the two failures come from one design choice about what the hash ranges over:

| | Trigger | Effect |
|---|---|---|
| Over-fires | any byte in an evidence file — a rename, formatting, an unrelated test in the same file | 8 verdicts expired by D19 for no semantic reason |
| Under-fires | any change to the plan entry that sets the required form | a verdict survives the standard it was measured against |

The second is cheap to fix and does not need the granularity question settled: include the claim's
effective required form — after plan entry and standard — in the hash. That is a small, bounded
input, unlike the evidence-file question below.

## The question

**Is file-granularity content hashing correctly conservative, or too coarse to survive routine
work?**

Both readings are defensible, which is why this is a question rather than a finding.

**Correctly conservative.** A judgment is a claim about specific evidence. Anything that changes the
bytes of that evidence is, from the fingerprint's position, indistinguishable from a change that
matters — and the cost of a false "stale" is a re-read, while the cost of a false "fresh" is a
verdict standing over evidence nobody looked at. D18 explicitly values a judgment that can expire
over one that cannot.

**Too coarse.** A signal that fires on renames, formatting and unrelated edits in the same file
teaches its readers to clear it without looking. That is the failure mode of every alert that cries
wolf, and it converts `stale` from "look again" into "look again, but it is usually nothing" — at
which point the freshness mechanism has the shape of evidence and none of the force. The corpus is
already at 18 stale out of 27 judged, and one commit caused eight of them.

## Options, unranked because there is no evidence to rank them by

1. **Nothing.** Accept re-judging as the price of the guarantee. Cheapest, and honest as long as
   re-judging actually happens rather than the count being ignored.
2. **Hash a normalized projection of the evidence** — the covering test bodies rather than whole
   files. Narrower blast radius; needs the extractor to emit spans, and a normalization that is
   itself a thing to get wrong.
3. **Two fingerprints, one verdict.** One over the claim, one over the evidence, so a reader can see
   *which* side moved. More information at no semantic cost; still cannot distinguish a rename from
   a rewrite.
4. **A judge-declared re-affirmation** — an explicit "re-read after D19, verdict unchanged" carrying
   the new fingerprint. Keeps the audit trail honest and costs a pass over the claims, which is the
   thing being avoided.

## What would settle it

Whether re-judging a `stale` verdict ever changes it. That is a measurement the corpus can produce
and has not: of the 18 stale judgments, re-judge them and record how many verdicts move. If none
move after a mechanical change, option 2 or 3 has an argument. If any move, the conservatism was
earning its cost and option 1 is right.

**Cheap to run and not yet run**, which is the same shape as `framework.md:271`'s ceremony falsifier
— and, like it, the sort of question that stays open because measuring it is boring rather than
because it is hard.
