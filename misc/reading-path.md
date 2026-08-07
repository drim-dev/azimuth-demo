# The literatures behind the register

**Status: notes, outside the framework.** Nothing here constrains the repo. Citations are from model
knowledge and were not verified this session — check editions and availability before buying
anything.

The writing register used in this repo is not invented here. `AGENTS.md`'s "Writing" section
prescribes it: claims as propositions rather than aspirations, every assertion derived or cited or
marked as a prediction, say what would falsify it, quantities over adjectives, no marketing
register, concede prior art, distinguish decided from proposed. Four separate literatures produced
those rules, and they barely cite each other.

## 1. The register itself

**Booth, Colomb & Williams, *The Craft of Research*.** Writing as claim → reason → evidence →
**warrant** (the principle connecting evidence to claim) → acknowledgment and response. A
research-methods book with no code in it, and the shortest path to producing the register rather
than recognizing it.

Behind it: **Toulmin, *The Uses of Argument*** (1958) — claim, grounds, warrant, backing, qualifier,
rebuttal. A summary suffices; the book is heavy going. Worth knowing because the structure of an
Azimuth claim is close to Toulmin's, reached independently.

## 2. The falsifier habit

**Popper, *Conjectures and Refutations*** — the first chapter carries it. "State what would refute
this, before you have the evidence" is his, and it is the rule behind `docs/status.md` recording
falsifiers ahead of results.

**Feynman, *Cargo Cult Science*** — four pages, free, the practitioner's version.

## 3. The discipline nearest this work

**Safety and assurance cases.** The field that professionally argues *here is a claim, here is the
evidence, here is why the evidence supports it, and here is what is left uncovered*. Residuals,
criticality tiers and per-level evidence standards all exist here already.

- **GSN Community Standard** — free, and the applied vocabulary closest to `verification/`.
- **Leveson, *Engineering a Safer World*** — free PDF from MIT Press. Also a sustained attack on
  assurance practices that produce confidence without grounds, which is the failure mode this
  framework is most exposed to.

## 4. The formal branch

Pick by appetite; only after the first three.

- **Leino, *Program Proofs*** (2023) — hands-on Dafny. Best value if you want the machine to argue
  back.
- **learntla.com** (Hillel Wayne) — TLA+, faster payoff, no proof obligations, and the
  counterexample culture lands immediately.
- **Jackson, *Software Abstractions*** — Alloy, and the clearest writing about what a specification
  is *for*.

For whether evidence discriminates — the `toothless` question directly — the mutation testing
literature (Jia & Harman, IEEE TSE 2011) and Barr et al.'s oracle survey (IEEE TSE 2015). See also
`formal-registers.md` on vacuity detection.

## An order

1. *The Craft of Research* — the register itself.
2. Popper, chapter 1 — the falsifier habit.
3. GSN standard — the applied vocabulary nearest this work.
4. One of the formal three, chosen by taste.

## Two honest notes

**Nobody has written the book that unifies these.** The gap between the argumentation literature and
the formal-methods literature is real, and is part of why this repo has to invent structure rather
than adopt it.

**The register is installed by writing, not reading.** Write a claim with an explicit falsifier,
then have someone attack it. Four books and no writing leaves you able to recognize the style and
not to produce it.
