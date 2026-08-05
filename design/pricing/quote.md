# Design: pricing/quote

## Requirement: quote-amount-integrity
Enforcement: type
Site: `Money` — integer minor units plus a currency, with no floating-point constructor,
conversion, or implicit numeric operator

Violation is unrepresentable rather than untested, which is why
`verification/pricing/quote.md` records proof strength and no runtime test for
`total-in-minor-units`.

*Corrected against the code.* An earlier version of this entry claimed that `Money.Sum` refuses a
mix of currencies **at the type level**. It does not, and cannot without phantom types — the check
is a runtime throw. The claim was falsifiable, writing the code falsified it, and the correction is
the mechanism working rather than a lapse. What is genuinely type-level is the absence of any
floating-point constructor, conversion, or arithmetic operator; that alone is what
`total-in-minor-units` rests on.

The rounding decision lives here too: splits allocate the remainder to the first component in a
deterministic order rather than distributing it, so that the sum relation holds exactly. This is
arbitrary but must stay stable — changing it changes historical totals recomputed for disputes.

## Residue

**Currency agreement is a runtime check, and the compiler will not stop a mixed-currency sum from
being written.** The throw is covered by a test, but the test is the only thing between a
mixed-currency total and production. A `Money<TCurrency>` shape would fix it and was rejected as
disproportionate for a fixture; if a second currency ever ships, this is the first thing to revisit.

**The type protects each language separately, and nothing protects the seam between them.** A
mobile client parsing an amount into a double for display and sending it back would satisfy every
mechanism above. C10 is the concern; the residual in `verification/pricing/quote.md` is where the
missing evidence is recorded. This note exists so that whoever adds the second language knows the
guarantee stops at the boundary rather than assuming the type travels.

**Quotes are never reissued under the same identifier**, which makes them safe to cache anywhere
and safe to log. That property is relied upon by the trip service's admission path and by
support tooling. It is not enforced by anything except the absence of an update path, and an
`UPDATE quotes SET` written in a migration or a fix-up script would break it without touching any
code that a check looks at.
