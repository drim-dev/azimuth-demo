---
name: azimuth-verify
description: Run the Azimuth agent tier over a claim or a spec — judge whether the covering evidence is toothy, whether the tags are honest, and whether the spec names what it should. Use when asked to verify, judge, or audit claims, or after adding tests that close holes.
---

# The agent tier

The machine tier makes structure checkable. It cannot make truth checkable: a tag is only as
honest as whoever wrote it, and an agent can write a toothless test and tag its form correctly.
This pass is what keeps the self-declaration honest, and without it a green matrix is a
self-certification.

Your output is a judgment per claim, recorded in `verification/judgments/<spec-id>.md`.

## Worklist

```
azimuth judge --manifest <each manifest>
```

Each line is `spec  scenario  criticality  fingerprint  state`, followed by the evidence files a
judgment must look at. Judge everything `unjudged` or `stale`; a `stale` judgment means the claim
or its evidence changed since the verdict, so the verdict no longer applies even if it still reads
true.

## What to examine, per claim

1. **Read the claim** in `specs/`, and the required form in `verification/`.
2. **Read every covering test** in the evidence files. Not the name — the body.
3. **Ask, in order:**
   - *Would this test fail against a plausible wrong implementation?* Construct one mentally: delete
     the guard, drop the constraint, return a constant. If the test still passes, it is
     **toothless**.
   - *Does the test do what its tag claims?* A tag saying `invariant` on a test with one hard-coded
     case is a **dishonest-tag**, however true the case is. So is `component` on a test that never
     touches the real store.
   - *Does the claim describe the behaviour that matters?* If the code is right, the test is toothy,
     and a reader would still be surprised by something the spec never says, that is a **spec-gap**.
4. Only if none of those fire is the verdict **sound**.

## Traps

- **A passing test is not evidence of toothiness.** The question is never "does it pass".
- **Setup is not assertion.** A test that builds elaborate state and asserts one trivial
  consequence is usually toothless.
- **A test that never constructs the failure case is toothless**, even when the claim is true. A
  cancellation claim verified without ever cancelling anything is the common shape.
- **Do not judge the implementation.** A correct implementation with a toothless test is still
  toothless: the verdict is about the evidence, not the code.
- **Do not soften a verdict because the code is yours.** The pass exists precisely for the case
  where the same author wrote the claim, the code, the test and the tag.

## Recording

```markdown
# Judgments: <spec-id>

## Claim: <scenario-id>
Verdict: sound | toothless | dishonest-tag | spec-gap
Fingerprint: <the fingerprint from `azimuth judge`, verbatim>
Judged: <YYYY-MM-DD>
Judge: <model or person>

What you examined, the wrong implementation you tried it against, and why the verdict. A verdict
without that is an opinion the next reader cannot check.
```

The fingerprint covers the claim's text and the content of every evidence file. Copy it exactly:
it is what makes the verdict expire when what it judged changes, and a judgment that cannot expire
is worse than none.

`sound` is not a pass mark to be handed out. If you find yourself writing `sound` for everything,
you are reading names rather than bodies.
