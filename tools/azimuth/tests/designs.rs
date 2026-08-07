//! Design artifact tests. Synthetic fixtures only (D2).

use azimuth::check::{rtm, HoleKind};
use azimuth::design::{parse_design, Enforcement, Target};
use azimuth::model::Model;
use azimuth::plan::{parse_plan, parse_standards};
use azimuth::spec::parse_spec;

const STANDARDS: &str = "\
# Verification standards
Default scope: unit

## Level: critical
Strength: demonstration
Quantification: universal
Residual: required

## Level: standard
Strength: demonstration
Quantification: example
Residual: optional

## Level: routine
Strength: none
Residual: optional
";

const SPEC: &str = "\
# Spec: alpha

## Requirement: matters
Criticality: critical

A SHALL.

### Scenario: concurrent-thing
WHEN things happen concurrently
THEN exactly one of them wins

## Requirement: lesser
Criticality: standard

A lesser SHALL.

### Scenario: ordinary-thing
WHEN a thing happens
THEN it works
";

fn design_err(source: &str) -> String {
    match parse_design("d.md", source) {
        Ok(_) => panic!("expected a parse error"),
        Err(d) => d.iter().map(|x| x.to_string()).collect::<Vec<_>>().join("\n"),
    }
}

fn model(design_source: &str, plan_source: &str) -> Model {
    let spec = parse_spec("alpha.md", SPEC).expect("spec parses");
    let standards = parse_standards("s.md", STANDARDS).expect("standards parse");
    let mut m = Model { specs: vec![spec], standards: Some(standards), ..Default::default() };
    if !design_source.is_empty() {
        m.designs = vec![parse_design("d.md", design_source).expect("design parses")];
    }
    if !plan_source.is_empty() {
        m.plans = vec![parse_plan("p.md", plan_source).expect("plan parses")];
    }
    m
}

fn kinds(m: &Model) -> Vec<(HoleKind, String)> {
    rtm(m).into_iter().map(|h| (h.kind, h.claim.unwrap_or_default())).collect()
}

const DESIGN: &str = "\
# Design: alpha

## Requirement: matters
Enforcement: constraint
Site: `ux_alpha_thing` — partial unique index on `things(alpha_id)`

Compare-and-set at the storage layer, not a check-then-write in the handler.

## Residue

Offers are fanned out optimistically and withdrawn afterwards. Deliberate.
";

#[test]
fn parses_a_design() {
    let d = parse_design("d.md", DESIGN).expect("parses");
    assert_eq!(d.spec, "alpha");
    assert_eq!(d.entries.len(), 1);
    let entry = &d.entries[0];
    assert_eq!(entry.target, Target::Requirement("matters".into()));
    assert_eq!(entry.mechanisms.len(), 1);
    assert_eq!(entry.mechanisms[0].kind, Enforcement::Constraint);
    assert!(entry.mechanisms[0].site.contains("ux_alpha_thing"));
    assert!(d.residue.contains("optimistically"));
}

/// C2 in the concern catalog: one rule, a choke point *and* a representation constraint. A single
/// mechanism field would have conflated them.
#[test]
fn a_requirement_may_carry_several_mechanisms() {
    let source = "\
# Design: alpha

## Requirement: matters
Enforcement: choke-point
Site: `Alpha.Transition` is the only writer
Enforcement: constraint
Site: conditional update predicated on current state

The choke point alone does not survive concurrency.
";
    let d = parse_design("d.md", source).expect("parses");
    let m = &d.entries[0].mechanisms;
    assert_eq!(m.len(), 2);
    assert_eq!(m[0].kind, Enforcement::ChokePoint);
    assert_eq!(m[1].kind, Enforcement::Constraint);
}

#[test]
fn every_enforcement_needs_a_site() {
    let source = DESIGN.replace("Site: `ux_alpha_thing` — partial unique index on `things(alpha_id)`\n", "");
    let text = design_err(&source);
    assert!(text.contains("names no site"), "{text}");
}

#[test]
fn a_site_needs_an_enforcement() {
    let source = DESIGN.replace("Enforcement: constraint\n", "");
    let text = design_err(&source);
    assert!(text.contains("with no enforcement"), "{text}");
}

#[test]
fn the_enforcement_set_is_closed() {
    let text = design_err(&DESIGN.replace("constraint", "vibes"));
    assert!(text.contains("unknown enforcement `vibes`"), "{text}");
    assert!(text.contains("choke-point"), "{text}");
}

/// "An entry that states a mechanism without a reason records a fact the code already knows."
#[test]
fn an_entry_needs_a_reason() {
    let source = "\
# Design: alpha

## Requirement: matters
Enforcement: constraint
Site: an index
";
    assert!(design_err(source).contains("gives no reason"));
}

/// Strength is derived from the enforcement kind (D7) and writing it would duplicate a derivable
/// fact. The label is not in the closed set, so it fails loudly rather than being ignored.
#[test]
fn strength_is_never_written_in_a_design_entry() {
    let source = DESIGN.replace("Enforcement: constraint", "Strength: proof\nEnforcement: constraint");
    let text = design_err(&source);
    assert!(text.contains("unrecognized line"), "{text}");
}

/// The residue attaches to no claim, participates in no check, and is the one part the machine
/// must never pretend to understand.
#[test]
fn the_residue_is_never_parsed() {
    let source = "\
# Design: alpha

## Requirement: matters
Enforcement: guard
Site: every handler checks

A reason.

## Residue

Enforcement: this line looks like a label and is prose.
Site: so is this one.
";
    let d = parse_design("d.md", source).expect("parses");
    assert_eq!(d.entries.len(), 1);
    assert!(d.residue.contains("looks like a label"));
}

/// D6.5: a design entry is required for `critical`, optional for `standard`.
///
/// Reported only once the artifact is in use: a spec covered by some other design file, with none
/// of its own.
#[test]
fn a_critical_requirement_without_a_design_entry_is_a_hole() {
    let elsewhere = "# Design: beta\n\n## Requirement: other\nEnforcement: guard\nSite: x\n\nA reason.\n";
    let mut m = model("", "");
    m.designs = vec![azimuth::design::parse_design("beta.md", elsewhere).unwrap()];
    let holes = kinds(&m);
    assert!(
        holes.contains(&(HoleKind::UndeclaredMechanism, "alpha#matters".into())),
        "{holes:?}"
    );
    assert!(!holes.contains(&(HoleKind::UndeclaredMechanism, "alpha#lesser".into())));
}

#[test]
fn a_design_entry_closes_it() {
    let m = model(DESIGN, "");
    assert!(!kinds(&m).iter().any(|(k, _)| *k == HoleKind::UndeclaredMechanism));
}

/// A facet attaches at the coarsest level where its statement is true, but an entry may key on a
/// scenario where the mechanism genuinely differs.
#[test]
fn a_scenario_keyed_entry_also_satisfies_the_requirement() {
    let source = "\
# Design: alpha

## Claim: concurrent-thing
Enforcement: constraint
Site: an index

A reason.
";
    let m = model(source, "");
    assert!(!kinds(&m).iter().any(|(k, _)| *k == HoleKind::UndeclaredMechanism));
}

#[test]
fn a_design_entry_for_no_requirement_is_dangling() {
    let source = "\
# Design: alpha

## Requirement: ghost
Enforcement: constraint
Site: an index

A reason.
";
    let m = model(source, "");
    assert!(kinds(&m).contains(&(HoleKind::DanglingDesignEntry, "alpha#ghost".into())));
}

/// The three-artifact check. Proof comes from a mechanism at the top of the enforcement ladder,
/// and the developer owns which mechanism that is — so a plan claiming proof with nothing behind
/// it is asserting the strongest available result out of thin air.
#[test]
fn proof_evidence_needs_a_proof_capable_mechanism() {
    let plan = "\
# Verification: alpha

## Claim: concurrent-thing
Strength: proof
Evidence: it is simply impossible

Asserted without a mechanism.
";
    let weak_design = "\
# Design: alpha

## Requirement: matters
Enforcement: guard
Site: every handler checks

Guards everywhere, because a choke point was not feasible.
";
    let m = model(weak_design, plan);
    let holes = kinds(&m);
    assert!(
        holes.contains(&(HoleKind::UnbackedProof, "alpha#concurrent-thing".into())),
        "{holes:?}"
    );

    let m = model(DESIGN, plan);
    assert!(!kinds(&m).iter().any(|(k, _)| *k == HoleKind::UnbackedProof));
}

#[test]
fn the_ladder_ranks_enforcement() {
    assert_eq!(Enforcement::Type.rung(), 1);
    assert_eq!(Enforcement::Schema.rung(), 1);
    assert_eq!(Enforcement::Constraint.rung(), 2);
    assert_eq!(Enforcement::ChokePoint.rung(), 2);
    assert_eq!(Enforcement::Middleware.rung(), 3);
    assert_eq!(Enforcement::Guard.rung(), 4);

    assert!(Enforcement::Type.is_proof_capable());
    assert!(Enforcement::ChokePoint.is_proof_capable());
    assert!(!Enforcement::Middleware.is_proof_capable());
    assert!(!Enforcement::Guard.is_proof_capable());
}

/// D8.1: each mechanism is usable alone. A project that has not adopted the design artifact must
/// not be told that every critical requirement is a hole.
#[test]
fn without_any_design_artifact_the_mechanism_check_is_silent() {
    let m = model("", "");
    assert!(m.designs.is_empty());
    assert!(!kinds(&m).iter().any(|(k, _)| *k == HoleKind::UndeclaredMechanism));
}
