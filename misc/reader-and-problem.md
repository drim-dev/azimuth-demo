# Proposal — state the reader and the problem in `framework.md`

**Status: proposed, not decided.** Reasoning only. No evidence from the fixture supports or refutes
it, which is why it is here and not in `docs/decisions.md`.

## The observation

`docs/framework.md` contains no reader. The word does not occur in it. Its sections are:

> The central claim · Five primitives · The three facets · Linkage ·
> Evidence, and what is required · Mechanism, and why strength is never written ·
> Findings · The tool · Decided, proposed, open · What would falsify this ·
> Prior art, conceded · What is not claimed

Every one describes what the framework **is** or whether it is **true**. None states whose problem
it solves, what that person does today instead, or what it would cost them to switch.

This is not a stylistic gap. Two entries already on the falsifier list depend on an answer that is
nowhere written down:

| Falsifier (`framework.md:262`ff) | State |
|---|---|
| Artifact and annotation cost exceeds what the defects justify → ceremony | never measured |
| The three role views over the export turn out identical → the facet split is decorative | never tested |

The second is the sharper one. **If the analyst, engineer and QA views of the export come out
identical, the framework has one reader and it is its author.** That is a failure of value, not of
correctness, and nothing in `decisions.md` can detect it: a design can be internally consistent,
well-argued and machine-checkable while solving a problem nobody has.

The distinction is Larry McEnerney's (*The Craft of Writing Effectively*, University of Chicago
Writing Program): a document is valuable when it removes a problem a specific community of readers
has, not when it is correct. Applied here it is a claim about the framework, not about its prose.

## The proposal

Add a short section to `docs/framework.md`, near the top, stating four things:

1. **Who this is for** — a specific role or team shape, not "engineering organizations".
2. **The problem they have now**, stated so it could be false.
3. **What they do instead today**, named honestly, including why it partly works.
4. **What would falsify the claim that they have this problem.**

Item 4 is what keeps the section from becoming the marketing register `CLAUDE.md` bans. "These
readers have this problem" is a proposition that can be wrong; "powerful and seamless" is not.

## Candidate readers — the author's call, not mine

I cannot supply item 1 by reasoning, and picking one here would be closing an open question the way
this repo forbids. Two candidates, with what each would commit to:

**(a) A team whose specs and traceability already rot.** They have the artifacts and the artifacts
have stopped meaning anything. The framework's offer is that the missing mechanism facet is *why*
they rot (D3's bet). Falsifier: if such a team reports their matrices rot for reasons unrelated to
mechanism — staleness, ownership, tooling friction — the central claim's motivation is wrong even if
the claim itself is defensible.

**(b) A team whose code, tests and tags are increasingly written by agents.** Their problem is that
self-declaration is the whole attack surface: an agent can write the claim, the code, the test and
the tag, and everything reports green. The framework's offer is the two-tier split — structure
checkable by machine, honesty checkable only by a judging pass (D18). Falsifier: if agent-written
evidence turns out to be no less honest than human-written evidence, the tier buys nothing that
review did not already buy.

These are not equivalent. (a) is the problem the framework was designed from — the concern catalog
is drawn from it. (b) is the problem the evidence in this repo actually speaks to: 6 tag failures in
18 judged claims, all of them self-declaration, all of them invisible to `azimuth check`. Choosing
(b) would be a claim that the framework found a better problem than the one it started on, and that
is a substantive decision with consequences for the fixture, not a framing exercise.

## Falsifiers to attach to the section itself

None of these is currently checkable, and saying so is the point:

- No reader outside this repo can state, unprompted, which of their current problems the framework
  removes → the value claim is unsupported however sound the design is.
- An adopter's first question is "what do I do with this" rather than "is this right" → the
  documents describe the framework rather than serve anyone.
- Adoption stalls on annotation cost before the first hole is found → the cure exceeds the disease,
  which is `framework.md:271` reaching a verdict.

## Cost, and the argument against

The section invites the register the repo bans, and a badly written one would do more damage than
its absence — a value claim with no falsifier is marketing wearing a scientific voice, and this
repo's credibility rests on not doing that.

There is also a case for leaving it out entirely: the framework is pre-extraction, has one consumer,
and stating a reader now fixes a target on reasoning rather than evidence. The counter is that both
dormant falsifiers already presuppose the answer, so it is being assumed silently either way, and an
assumption written down can be checked.
