# Exploration: Drim dogfooding findings

Id: drim-dogfood
Status: exploring
Created: 2026-08-16

## Objective

Record observations made while adopting and using published Azimuth versions in Drim. The record
keeps consumer evidence available for later analysis without treating an observation, inference or
possible remedy as accepted framework truth.

## Boundaries

This exploration covers onboarding, initialization, agent guidance, migration friction, modeling
gaps and tool behavior observed in `drim-dev/drim-dev`. Each finding identifies the Azimuth version
and consumer context that produced it.

Registry reservation, artifact publication and other release-production findings remain outside
this exploration because the canonical alpha-release exploration already owns that activity. This
record is not a roadmap, issue tracker or source of current framework behavior.

## Existing context

The consumer exercised the public `0.1.0-alpha.1` release. Adoption has not yet carried the planned
referral feature through the complete Azimuth change lifecycle, so the entries below are adoption
findings rather than evidence about that lifecycle as a whole.

## Findings

### DF-001 — Consumer installation is not self-contained

Version: `0.1.0-alpha.1`
Context: initial installation in `drim-dev/drim-dev`
Status: open

**Observation.** The crate README documents installation from a source checkout. The public
repository directs readers to repository-local framework documents, while the consumer start path
does not explain the native release archives or distinguish binary installation from compilation
by `cargo install`. Dogfooding required separate explanations of the command, crate archive contents
and precompiled archives.

**Inference.** A new consumer cannot reliably discover every supported installation path from the
published package documentation alone.

**Consequence.** Installation can succeed while the consumer remains uncertain about which artifact
was installed, whether compilation is expected and when a qualified native archive applies.

**Falsifier.** A first-time consumer installs either supported form and accurately explains its
provenance and platform qualification using only the published consumer entry point.

**Disposition.** Retain for later analysis; no remedy is selected.

### DF-002 — Initialization does not bootstrap an agent workflow

Version: `0.1.0-alpha.1`
Context: `azimuth init` in `drim-dev/drim-dev`
Status: open

**Observation.** Initialization creates the model, change, exploration and standards directories,
`standards/verification.md`, and `workspace.json`. It does not install Azimuth skills, format
contracts, operating documentation or agent instructions. The initialized Drim repository contains
only the two generated files under `azimuth/`.

**Inference.** A coding agent reading only the consumer repository cannot discover how to propose,
apply, verify and archive an Azimuth change.

**Consequence.** Successful initialization does not make the released workflow operable by a cold
coding agent without additional, manually supplied context.

**Falsifier.** A cold agent correctly follows the released change workflow using only the
initialized consumer repository.

**Disposition.** Retain for later analysis; no remedy is selected.

### DF-003 — Generated configuration is not self-explanatory at its point of use

Version: `0.1.0-alpha.1`
Context: review of files produced by `azimuth init`
Status: open

**Observation.** Understanding the generated verification standard and empty workspace required
separate explanations. The generated workspace does not state when areas, surfaces or realization
obligations should be introduced.

**Inference.** Initialization establishes valid syntax but does not supply enough local context for
a first-time adopter to make sound modeling decisions.

**Consequence.** A consumer can retain valid defaults without understanding what they require or
when the empty architectural declarations must change.

**Falsifier.** A first-time adopter accurately explains the two generated files and makes a sound
decision about leaving or changing each one using only repository-local initialized material.

**Disposition.** Retain for later analysis; no remedy is selected.

### DF-004 — Replacement of pre-alpha state is manual

Version: `0.1.0-alpha.1`
Context: replacement of earlier Azimuth and OpenSpec integration in `drim-dev/drim-dev`
Status: accepted limitation

**Observation.** Drim contained pre-alpha Azimuth linkage, build integration and OpenSpec workflow
artifacts. They required a repository audit and selective removal while educational content had to
remain. `azimuth init` is additive and performs no migration or stale-state detection.

**Inference.** Adoption from an earlier experiment requires an explicit audit of the consumer's
previous state.

**Consequence.** Stale integration can survive initialization or broad cleanup can remove content
that merely discusses the superseded tools.

**Falsifier.** A consumer with structurally similar pre-alpha state can initialize the public alpha
without a manual audit and without retaining technical residue or deleting unrelated content.

**Disposition.** Retain as an observed consequence of the alpha's deliberate no-compatibility
boundary; it does not by itself establish a framework defect.

### DF-005 — One ordered strength cannot require complementary evidence

Version: `0.1.0-alpha.1`
Context: modeling settlement reconciliation with both tests and operational detection
Status: open

**Observation.** Evidence strength is one ordered value:
`detection < demonstration < proof`. A demonstration therefore satisfies a detection floor. Azimuth
checks the detector and detector-test bindings when a detection item is declared, but deleting that
entire item creates no hole when demonstration evidence still satisfies the claim's required
strength.

**Inference.** The verification policy cannot express an obligation for both demonstration and
detection evidence on one claim.

**Consequence.** If monitoring is required only as complementary assurance, its declaration can
disappear without making the model incomplete. When notification is durable system behavior, a
separate behavioral claim can preserve that obligation, but that does not express a general
requirement for complementary assurance forms.

**Falsifier.** Show that the released model can require demonstration and detection independently
for one claim, such that removing either evidence form creates a machine finding while the other
remains.

**Disposition.** Retain for later analysis. Evidence-before-notation prevents generalizing one
observation into new syntax before a second structurally different concern demands it.

## Decisions

Findings use stable `DF-NNN` identifiers and retain observation, inference, consequence, falsifier,
status and disposition. Corrections will be marked rather than silently rewritten. Recording a
finding does not authorize a framework change.

## Open questions

Analysis is deliberately deferred. No remedy, experiment or change decomposition is selected.

## Result

Five adoption findings are recorded. The exploration remains active while Drim dogfooding
continues.
