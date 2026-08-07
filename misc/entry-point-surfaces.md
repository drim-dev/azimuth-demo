# Proposal — an entry-point inventory, and classes as subsets of it

**Status: proposed, with one finding from the 2026-08-07 run that is not proposed but measured.**
Follows the derived-membership change to `invariant-breach`, which is implemented; everything below
is about generalizing it and is not.

## The finding: app-level class mapping is too coarse

The implemented flag reads `--next-app trips/rider-view=app/web/rider` — *every route in this app is
a member of this class*. Run against the corpus it produced 8 breaches, and three of the five on the
rider side are members that have nothing to do with the claim:

| Route | Breaches `position-confined-to-live-phases` | Can it carry a driver position? |
|---|---|---|
| `/api/rider/trips/[id]` | yes | **yes** — serves the rider trip view |
| `/api/rider/trips/[id]/receipt` | yes | **yes** — the surface that historically leaked |
| `/api/rider/trips` | yes | no — creation response carries no position |
| `/api/rider/quotes` | yes | no — a quotes route |
| `/` | yes | no — the request form |

Three of five are noise, and each still costs a declaration. That is measured, not predicted, and it
is the argument for what follows.

*(Recorded for the record: the prediction before the run was four breaches, all rider-side. It was
wrong twice — `/api/rider/trips` is tagged for a `trips/request` claim and so was never a member of
this class under the old rule, and the driver app was not counted at all.)*

## The two levels this needs

**Inventory** — every entry point, derived per technology, complete and audience-agnostic. HTTP
routes, gRPC methods, message handlers, scheduled jobs. This is what the enumerators produce.

**Class** — a *subset* of the inventory, selected by a predicate the project supplies.

Conflating them fails as soon as any of these is true, and all three are ordinary:

- one app serves two audiences, so app membership does not imply class membership;
- a class spans components — "rider-reachable" covers the BFF route, the trip service endpoint and
  a push payload builder in three different processes;
- an entry point belongs to two classes at once.

The selector is a project fact and stays project-side, the same division `covers` and `realizes`
already use. The tool consumes an inventory and a class assignment; it derives neither.

## The correction that matters more: ingress is not the surface

`position-confined-to-live-phases` is about what **leaves** the system toward a principal. Entry
points are a usable proxy only while every egress is a response to a request, and two of the
technologies that motivated this break that assumption:

- a **scheduler** that mails a receipt has no entry point in the request sense and can still leak;
- a **Kafka producer** is egress with no inbound trigger from the principal at all;
- conversely a health check is an entry point that can never leak.

So for confidentiality the class ranges over *sites that can emit this kind of data to this
principal*, and an entry-point inventory both over- and under-approximates it at once.

Other concern kinds want ingress directly — "every entry point authorizes the principal", "every
mutating handler is replay-safe". Those are ingress claims and the inventory fits them without
adjustment. **The inventory is one surface, not the surface**, and which one a class needs is a
property of the concern.

## The ordering constraint

Take the full list seriously — HTTP, gRPC, Kafka, schedulers, across several services — and a
mid-size estate has hundreds of entry points and several classes. Discharged individually that is
N × M annotations, against a repo that already carries the open falsifier *artifact and annotation
cost exceeds what the defects justify → ceremony*.

The escape is already named in `tools/azimuth/README.md`: `invariant-breach` verifies only the
weakest rung of the enforcement ladder — a guard at every site — and **crediting a choke point
needs call-graph analysis (D10.1)**. With choke-point crediting, one proven projection discharges
every member routing through it and N × M collapses to a few mechanisms plus their exceptions.
Without it, each new enumerator multiplies the annotation burden linearly.

**So: choke-point crediting before more enumerators.** Adding gRPC and Kafka adapters first makes
the cost falsifier more likely to fire, not less.

## What is derivable, per technology

| Technology | Derived from | Difficulty |
|---|---|---|
| gRPC | `.proto` service descriptors | easiest — the contract *is* the enumeration |
| HTTP (ASP.NET) | endpoint data source, or OpenAPI | easy |
| HTTP (Next.js) | route manifest | **implemented** |
| Schedulers | job registrations — Quartz, Hangfire, cron config | moderate |
| Kafka consumers | consumer registrations in the container | moderate, least standardized |

D13.1 already names "the route table, the DI container, the type graph, the migration set" as the
sources an enumerator must come from. The rule anticipated all of these; only the adapters are
missing.

## What is deliberately not built

**No gRPC, Kafka or scheduler adapters.** The fixture uses none of them, and building extractors for
technologies the corpus does not exercise is the failure mode `AGENTS.md` names — the fixture
becoming the product. It is also premature given the ordering constraint above.

**No inventory/class split yet either.** It is a small change and testable on the two apps that
exist, but it should be driven by the measured noise above rather than by this argument, and it
wants deciding rather than assuming.

## Falsifiers

- **If splitting inventory from class selection does not reduce the 8 breaches to roughly 4**, the
  noise is not coming from the mapping and this analysis is wrong.
- **If the three "cannot carry a position" routes turn out to be dischargeable in one line each**,
  then the coarse mapping costs almost nothing, the split is ceremony, and the right answer is to
  leave it alone and declare them.
- **If choke-point crediting lands and the breach count does not fall sharply**, the scaling
  argument is wrong and per-site discharge is simply what this costs.

## Relation to the other open items

Three proposals now turn on the same thing — `site-class-evidence.md` (a test cannot enumerate a
derived class), `unclaimed-outcomes.md` (refusal outcomes as a derived surface), and this one. In
all three the enumerator is the load-bearing part and the notation is unchanged. That is support for
D13.1 and D13.2 doing real work, and evidence *against* needing new notation — which is the opposite
of what a growing pile of proposals usually means.
