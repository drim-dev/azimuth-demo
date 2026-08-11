---
name: azimuth-explore
description: Research and shape an uncertain initiative before creating Azimuth changes. Use for a new domain or module, a substantial refactor, curriculum or content design, or any effort likely to span several changes; separate discoverable facts from user-owned decisions and persist the confirmed result under azimuth/explorations/.
---

# Explore an initiative

Turn an uncertain objective into a researched, agreed direction and a candidate change graph. An
exploration is above changes: it may produce several changes, an experiment, or no work at all.

## Boundary

Do not create a change, plan implementation, or edit the deliverable until the user confirms shared
understanding. Research may inspect the repository, history, documentation, external primary
sources and non-mutating system state. Ask the user for decisions; do not ask them for facts the
environment can establish.

## Workflow

1. Locate the Azimuth project and read current model packages, active changes, relevant source and
   recent history. In a federated checkout, run `azimuth project locate` before assuming authority.
2. Define the objective, boundaries and what would make the exploration useful. Keep adjacent
   ambitions outside the boundary.
3. Research the factual frontier. Label each material statement as fact, inference or unknown and
   retain a source or repository location for facts.
4. Build the decision frontier: ask only questions whose prerequisites are settled. Default to one
   decision question at a time; use numbered independent rounds when the user prefers batching.
   Give a recommendation and its trade-off, then wait for the user's answer.
5. When discussion cannot settle a question, name the smallest prototype, measurement or external
   research that could settle it. Do not reason indefinitely over an empirical choice.
6. Present the resulting direction, rejected alternatives, unresolved questions and candidate
   change dependency graph. Ask the user to confirm shared understanding.
7. Only after confirmation, persist the account. Run
   `azimuth explore create <id> --title <title>` when no package exists, then update its
   `exploration.md`. Add `research.md` only when sourced findings obscure the anchor and
   `change-map.md` only when more than one likely change exists.

## Artifact rules

- `azimuth/explorations/<id>/exploration.md` is the anchor and is not accepted product truth.
- Keep proposed target behaviour out of `azimuth/model/`; that directory describes current truth.
- Do not put a multi-change exploration inside the first change.
- A downstream proposal declares `Exploration: <id>` and the decision ids it carries. Derive the
  reverse exploration-to-change map; do not maintain two authoritative copies.
- Archive an exploration when every decision has a disposition and its intended changes or
  experiments are identified. Do not wait for those changes to finish.

## Completion

Stop with one of: an agreed change map, an agreed experiment, an explicit abandonment, or named
open decisions the user chose not to settle. Never turn silence into agreement.
