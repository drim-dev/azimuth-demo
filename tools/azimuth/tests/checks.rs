//! Model, manifest and check tests. Synthetic fixtures only (D2).

use azimuth::check::{rtm, HoleKind, Severity};
use azimuth::json;
use azimuth::manifest;
use azimuth::model::Model;
use azimuth::selects;
use azimuth::spec::parse_spec;

const SPEC: &str = "\
# Spec: alpha

## Requirement: matters
Criticality: critical

A SHALL.

### Scenario: guarded
WHEN a thing happens
THEN another thing happens

## Requirement: cosmetic
Criticality: routine

A lesser SHALL.

### Scenario: decorative
WHEN a thing happens
THEN it looks right
";

/// A class claim and one behavioural claim in the same spec, so a site can be a member by tag.
const CLASS_SPEC: &str = "\
# Spec: beta

## Invariant: confined
Criticality: critical
Over: beta

Nothing SHALL leak at any site in the class.

## Requirement: shown
Criticality: standard

A lesser SHALL.

### Scenario: rendered
WHEN a thing is rendered
THEN it looks right
";

fn class_model(manifest_json: &str) -> Model {
    let spec = parse_spec("beta.md", CLASS_SPEC).expect("spec parses");
    let mut model = Model {
        specs: vec![spec],
        ..Default::default()
    };
    let root = json::parse(manifest_json).expect("manifest is valid json");
    let m = manifest::parse("m.json", &root).expect("manifest parses");
    model.realizes = m.realizes;
    model.covers = m.covers;
    model.class_members = m.class_members;
    model.enumerations = m.enumerations;
    model
}

fn model_with(manifest_json: &str) -> Model {
    let spec = parse_spec("alpha.md", SPEC).expect("spec parses");
    let mut model = Model {
        specs: vec![spec],
        ..Default::default()
    };
    if !manifest_json.is_empty() {
        let root = json::parse(manifest_json).expect("manifest is valid json");
        let m = manifest::parse("m.json", &root).expect("manifest parses");
        model.realizes = m.realizes;
        model.covers = m.covers;
        model.class_members = m.class_members;
        model.enumerations = m.enumerations;
    }
    model
}

fn kinds(model: &Model) -> Vec<(HoleKind, String)> {
    rtm(model)
        .into_iter()
        .map(|h| (h.kind, h.claim.unwrap_or_default()))
        .collect()
}

#[test]
fn an_untagged_claim_is_both_unrealized_and_uncovered() {
    let holes = kinds(&model_with(""));
    assert!(holes.contains(&(HoleKind::Unrealized, "alpha#guarded".into())));
    assert!(holes.contains(&(HoleKind::Uncovered, "alpha#guarded".into())));
}

/// D20: `routine` stops at intent, so neither linkage facet exists to have a hole.
#[test]
fn a_routine_claim_has_no_linkage_holes() {
    let holes = kinds(&model_with(""));
    assert!(!holes.contains(&(HoleKind::Uncovered, "alpha#decorative".into())));
    assert!(!holes.contains(&(HoleKind::Unrealized, "alpha#decorative".into())));
}

/// D9.2: severity comes from criticality, not from the check. Routine has no linkage finding to
/// classify under D20.
#[test]
fn severity_follows_criticality() {
    let holes = rtm(&model_with(""));
    let critical = holes
        .iter()
        .find(|h| h.claim.as_deref() == Some("alpha#guarded"))
        .unwrap();
    assert_eq!(critical.severity, Severity::Error);
}

#[test]
fn tags_close_holes() {
    let model = model_with(
        r#"{
          "realizes": [
            {"spec":"alpha","scenario":"guarded","site":"Trip.Do","file":"a.cs","lang":"csharp"},
            {"spec":"alpha","scenario":"decorative","site":"Ui.Show","file":"b.ts","lang":"ts"}
          ],
          "covers": [
            {"spec":"alpha","scenario":"guarded","site":"Tests.Guarded","file":"t.cs",
             "lang":"csharp","scope":"component","quantification":"universal"}
          ]
        }"#,
    );
    assert!(rtm(&model).is_empty(), "{:?}", rtm(&model));
}

#[test]
fn a_tag_naming_no_claim_is_dangling() {
    let model = model_with(
        r#"{
          "covers": [
            {"spec":"alpha","scenario":"ghost","site":"Tests.Ghost","file":"t.cs","lang":"csharp"}
          ],
          "realizes": [
            {"spec":"beta","scenario":"guarded","site":"X.Y","file":"a.cs","lang":"csharp"}
          ]
        }"#,
    );
    let holes = kinds(&model);
    assert!(holes.contains(&(HoleKind::DanglingTag, "alpha#ghost".into())));
    assert!(holes.contains(&(HoleKind::DanglingRealization, "beta#guarded".into())));
}

/// D6.2: a requirement without a declared criticality is a hole, and the parse still succeeds.
#[test]
fn an_unclassified_requirement_is_a_hole() {
    let source = SPEC.replace("Criticality: critical\n", "");
    let spec = parse_spec("alpha.md", &source).expect("parses");
    let model = Model {
        specs: vec![spec],
        ..Default::default()
    };
    let holes = rtm(&model);
    let hole = holes
        .iter()
        .find(|h| h.kind == HoleKind::Unclassified)
        .expect("unclassified");
    assert_eq!(hole.severity, Severity::Error);
    assert_eq!(hole.claim.as_deref(), Some("alpha#matters"));
}

/// D2.2: the manifest key is the pair, not the alpha's triple. Silently ignoring `req` would leave
/// a stale emitter producing tags that look fine and are not.
#[test]
fn the_triple_key_is_rejected_with_an_explanation() {
    let root = json::parse(
        r#"{"realizes":[{"spec":"alpha","req":"matters","scenario":"guarded",
            "site":"X","file":"a.cs","lang":"csharp"}]}"#,
    )
    .unwrap();
    let errors = manifest::parse("m.json", &root).unwrap_err();
    let text = errors
        .iter()
        .map(|d| d.to_string())
        .collect::<Vec<_>>()
        .join("\n");
    assert!(text.contains("the pair (spec, scenario)"), "{text}");
}

/// Form is how a test checks, not a property of code.
#[test]
fn realizes_cannot_carry_a_form() {
    let root = json::parse(
        r#"{"realizes":[{"spec":"alpha","scenario":"guarded","site":"X","file":"a.cs",
            "lang":"csharp","scope":"unit"}]}"#,
    )
    .unwrap();
    let errors = manifest::parse("m.json", &root).unwrap_err();
    let text = errors
        .iter()
        .map(|d| d.to_string())
        .collect::<Vec<_>>()
        .join("\n");
    assert!(text.contains("not a property of code"), "{text}");
}

#[test]
fn unknown_form_values_are_rejected() {
    let root = json::parse(
        r#"{"covers":[{"spec":"alpha","scenario":"guarded","site":"X","file":"t.cs",
            "lang":"csharp","scope":"integration"}]}"#,
    )
    .unwrap();
    let errors = manifest::parse("m.json", &root).unwrap_err();
    let text = errors
        .iter()
        .map(|d| d.to_string())
        .collect::<Vec<_>>()
        .join("\n");
    assert!(text.contains("unknown scope `integration`"), "{text}");
    assert!(text.contains("unit, component or e2e"), "{text}");
}

/// D19 renamed the quantification value `invariant` → `universal` with no alias (D2.3). The retired
/// word must fail as an unknown value rather than be quietly accepted, because a manifest emitted
/// by a stale extractor would otherwise report a form the model no longer defines.
#[test]
fn the_retired_quantification_value_is_rejected() {
    let root = json::parse(
        r#"{"covers":[{"spec":"alpha","scenario":"guarded","site":"X","file":"t.cs",
            "lang":"csharp","scope":"unit","quantification":"invariant"}]}"#,
    )
    .unwrap();
    let errors = manifest::parse("m.json", &root).unwrap_err();
    let text = errors
        .iter()
        .map(|d| d.to_string())
        .collect::<Vec<_>>()
        .join("\n");
    assert!(
        text.contains("unknown quantification `invariant`"),
        "{text}"
    );
    assert!(text.contains("example or universal"), "{text}");
}

/// Selection operates on ids, not paths, so it survives a reorganization of the tree.
#[test]
fn selection_matches_id_prefixes() {
    assert!(selects("trip/**", "trip/dispatch"));
    assert!(selects("trip/**", "trip/a/b"));
    assert!(selects("trip/**", "trip"));
    assert!(!selects("trip/**", "trips/dispatch"));
    assert!(!selects("trip/**", "driver/dispatch"));
    assert!(selects("trip/dispatch", "trip/dispatch"));
    assert!(!selects("trip/dispatch", "trip/dispatching"));
}

#[test]
fn the_export_carries_claims_tags_and_holes() {
    let model = model_with(
        r#"{"covers":[{"spec":"alpha","scenario":"guarded","site":"T.A","file":"t.cs",
            "lang":"csharp","scope":"unit","quantification":"example"}]}"#,
    );
    let holes = rtm(&model);
    let text = model.to_json(&holes).to_string_pretty();
    let round_tripped = json::parse(&text).expect("export is valid json");

    assert!(round_tripped.get("specs").is_some());
    assert!(round_tripped.get("covers").is_some());
    assert!(round_tripped.get("holes").is_some());
    assert_eq!(
        round_tripped
            .get("holes")
            .unwrap()
            .as_array()
            .unwrap()
            .len(),
        holes.len()
    );
}

#[test]
fn json_round_trips_escapes_and_unicode() {
    let original = r#"{"a":"quote \" backslash \\ newline \n tab \t unicode é é"}"#;
    let parsed = json::parse(original).unwrap();
    let text = parsed.to_string_pretty();
    assert_eq!(json::parse(&text).unwrap(), parsed);
}

/// The pre-existing membership rule: a site joins by realizing a behavioural claim in the class
/// spec, and must then discharge the invariant separately. Two different tags, so this can fail.
#[test]
fn a_tagged_site_that_does_not_discharge_breaches() {
    let model = class_model(
        r#"{"realizes":[{"spec":"beta","scenario":"rendered","site":"Page","file":"page.tsx",
            "lang":"typescript"}],"enumerations":[{"class":"beta","kind":"routes",
            "source":"routes.json","source_fingerprint":"abc"}]}"#,
    );
    let holes = kinds(&model);
    assert!(
        holes.contains(&(HoleKind::InvariantBreach, "beta#confined".into())),
        "{holes:?}"
    );
}

/// The case tags cannot reach, and the reason `class_members` exists: a file carrying no tag at all
/// is invisible to a tag-derived enumerator, so the surface the claim ranges over silently stops at
/// whatever somebody remembered to annotate (D13.1).
#[test]
fn an_emitted_member_with_no_tags_at_all_breaches() {
    let model = class_model(
        r#"{"realizes":[{"spec":"beta","scenario":"rendered","site":"Page","file":"page.tsx",
            "lang":"typescript"},{"spec":"beta","scenario":"confined","site":"Page",
            "file":"page.tsx","lang":"typescript"}],
            "class_members":[{"class":"beta","site":"/untouched","file":"untouched.ts",
            "lang":"typescript"}],"enumerations":[{"class":"beta","kind":"routes",
            "source":"routes.json","source_fingerprint":"abc"}]}"#,
    );
    let holes = kinds(&model);
    assert!(
        holes.contains(&(HoleKind::InvariantBreach, "beta#confined".into())),
        "{holes:?}"
    );
    assert_eq!(
        holes
            .iter()
            .filter(|(k, _)| *k == HoleKind::InvariantBreach)
            .count(),
        1,
        "the tagged-and-discharged site must not also breach: {holes:?}"
    );
}

/// An emitted member *is* its file — the enumerator names files, not symbols inside them — so a
/// discharge anywhere in the file discharges the member, whatever the site is called.
#[test]
fn an_emitted_member_discharges_from_anywhere_in_its_file() {
    let model = class_model(
        r#"{"realizes":[{"spec":"beta","scenario":"confined","site":"GET",
            "file":"route.ts","lang":"typescript"}],
            "class_members":[{"class":"beta","site":"/thing","file":"route.ts",
            "lang":"typescript"}],"enumerations":[{"class":"beta","kind":"routes",
            "source":"routes.json","source_fingerprint":"abc"}]}"#,
    );
    let holes = kinds(&model);
    assert!(
        !holes.iter().any(|(k, _)| *k == HoleKind::InvariantBreach),
        "discharge in the same file should clear the member: {holes:?}"
    );
}

#[test]
fn a_site_domain_without_a_derived_enumerator_fails_closed() {
    let model = class_model(
        r#"{"realizes":[{"spec":"beta","scenario":"rendered","site":"Page",
            "file":"page.tsx","lang":"typescript"}]}"#,
    );
    let holes = kinds(&model);
    assert!(
        holes.contains(&(
            HoleKind::EnumeratorUnsoundOrUnderived,
            "beta#confined".into()
        )),
        "{holes:?}"
    );
    assert!(
        !holes
            .iter()
            .any(|(kind, _)| *kind == HoleKind::InvariantBreach),
        "a partial domain must not produce authoritative member findings: {holes:?}"
    );
}
