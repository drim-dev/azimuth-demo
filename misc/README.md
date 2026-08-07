# misc

Working material that belongs to none of the three facets. **Nothing here is authoritative.**
`docs/decisions.md` is authoritative for decisions, `docs/glossary.md` for terminology, and
`specs/`, `design/`, `verification/` for claims, mechanisms and evidence. A file here is a proposal,
an observation, or notes on something outside the framework entirely.

**Items graduate out.** A proposal that gets decided moves to `docs/decisions.md` and the copy here
is deleted rather than left behind to disagree with it. A file that is still here in three months is
either genuinely out of scope or a proposal nobody has been willing to decide.

| File | Holds | Status |
|---|---|---|
| `quantification-review.md` | prior art for `example`/`invariant` and what the field does and does not buy; the rename proposal graduated to D19 | observations · one failed prediction |
| `site-class-evidence.md` | why a behavioural test cannot be universal evidence for a claim over a derived set of sites | finding · one proposal |
| `fingerprint-granularity.md` | that the judgment fingerprint over-fires on renames and under-fires on plan changes | open question · one bounded fix |
| `scope-field.md` | that the harness decided every scope tag in one spec, and that a mutation derived a required scope | observations · open question |
| `exemption-visibility.md` | 17 exemptions disappear; one 400/422 boundary is unspecified | implementation finding · D20.1 decided the framework fix |
| `unclaimed-outcomes.md` | making "which claims must exist" checkable over a derived surface of client-visible refusals | proposed · one prediction |
| `design-fiction.md` | three design entries named mechanisms nobody wrote, and the judging rubric that trusted them | finding · rubric fixed |
| `entry-point-surfaces.md` | an entry-point inventory with classes as subsets of it, and why choke-point crediting comes first | proposed · one measured finding |
| `reader-and-problem.md` | that `framework.md` names no reader, and two candidate readers with their falsifiers | proposed |
| `formal-registers.md` | languages that force precision, and what none of them force | reference · one proposal |
| `reading-path.md` | the literatures behind the writing register, and an order to read them in | notes, outside the framework |
| `business-application.md` | applying the framework's disciplines to teaching and consulting | notes, outside the framework |

## Where this came from

A session across 2026-08-06 and 07 that began by judging `specs/trips/request.md` with the agent
tier and turned into an examination of the quantification field itself. Four artifacts from it are
*not* here, because they have homes:

- The eight `trips/request` judgments — `verification/judgments/trips/request.md`. Two `sound`, two
  `toothless`, four `dishonest-tag`.
- The nine `trips/rider-view` judgments — `verification/judgments/trips/rider-view.md`. Three
  `sound`, three `toothless`, two `spec-gap`, one `dishonest-tag`; the first pass to produce all
  four verdict kinds. The actions they imply — a plan residual for `driver-position-follows-driver`,
  which no fixture path can trigger, and scenarios for the pushed-observation modes `design/` names
  — live in that file and are not restated here.
- The rename decided from it — **D19** and **D19.1** in `docs/decisions.md`.
- The authoring skill written in response — `.agents/skills/azimuth-cover/`, with its own falsifier
  and a fixture-local reference file.

Citations to outside literature in these files are from model knowledge and were **not verified**
this session, except where a URL is given. Check anything before it moves into `docs/`.
