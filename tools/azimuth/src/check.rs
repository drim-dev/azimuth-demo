//! Checks over the model.
//!
//! `rtm` is one check among several, not the product (D9). Every hole kind it reports is a
//! missing-facet combination (D3) — intent without mechanism, intent without evidence, evidence
//! without intent, mechanism without intent. A hole kind that is *not* one of those would imply a
//! fourth facet, and is the recorded falsifier for D3.

use crate::json::Json;
use crate::model::{Criticality, Model, Quantification, Required, Scope, Site, Strength};
use crate::plan::EvidenceItem;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Severity {
    Error,
    Warning,
}

impl Severity {
    pub fn name(self) -> &'static str {
        match self {
            Severity::Error => "error",
            Severity::Warning => "warning",
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum HoleKind {
    /// intent present, mechanism absent
    Unrealized,
    /// intent present, evidence absent
    Uncovered,
    /// evidence present, intent absent — the tag names a claim that does not exist
    DanglingTag,
    /// mechanism present, intent absent
    DanglingRealization,
    /// evidence present, intent absent — the test names nothing at all
    UntracedTest,
    /// evidence present, intent absent — a plan entry for a claim that does not exist
    DanglingPlanEntry,
    /// intent and evidence present, but no evidence meets the declared standard
    WrongForm,
    /// D6.2: a requirement without a declared criticality.
    ///
    /// Not a missing-facet combination: the intent facet is *present but incomplete*. See
    /// `UnacceptedWeakening` and the note on `rtm`.
    Unclassified,
    /// A plan requires less than the standard without recording and accepting the residual.
    /// Incomplete-facet, like `Unclassified`.
    UnacceptedWeakening,
}

impl HoleKind {
    pub fn name(self) -> &'static str {
        match self {
            HoleKind::Unrealized => "unrealized",
            HoleKind::Uncovered => "uncovered",
            HoleKind::DanglingTag => "dangling-tag",
            HoleKind::DanglingRealization => "dangling-realization",
            HoleKind::UntracedTest => "untraced-test",
            HoleKind::DanglingPlanEntry => "dangling-plan-entry",
            HoleKind::WrongForm => "wrong-form",
            HoleKind::Unclassified => "unclassified",
            HoleKind::UnacceptedWeakening => "unaccepted-weakening",
        }
    }
}

#[derive(Debug, Clone)]
pub struct Hole {
    pub kind: HoleKind,
    pub severity: Severity,
    pub claim: Option<String>,
    pub criticality: Option<Criticality>,
    pub path: String,
    pub line: usize,
    pub detail: String,
}

impl Hole {
    pub fn to_json(&self) -> Json {
        Json::obj(vec![
            ("kind", Json::str(self.kind.name())),
            ("severity", Json::str(self.severity.name())),
            (
                "claim",
                match &self.claim {
                    Some(c) => Json::str(c),
                    None => Json::Null,
                },
            ),
            (
                "criticality",
                match self.criticality {
                    Some(c) => Json::str(c.name()),
                    None => Json::Null,
                },
            ),
            ("file", Json::str(&self.path)),
            ("line", Json::Num(self.line as f64)),
            ("detail", Json::str(&self.detail)),
        ])
    }
}

/// D9.2: severity comes from criticality, not from the check. One non-zero exit for "something
/// failed" stops being useful at ten checks.
///
/// `routine` warns rather than fails, because D6.5 gives it a spec entry and nothing else — it is
/// the tier D9.2 means by low-criticality. `standard` fails: it requires a verification plan, so
/// its holes are real.
fn severity_for(criticality: Option<Criticality>) -> Severity {
    match criticality {
        Some(Criticality::Routine) => Severity::Warning,
        _ => Severity::Error,
    }
}

pub fn rtm(model: &Model) -> Vec<Hole> {
    let mut holes = Vec::new();

    for spec in &model.specs {
        for requirement in &spec.requirements {
            if requirement.criticality.is_none() {
                holes.push(Hole {
                    kind: HoleKind::Unclassified,
                    severity: Severity::Error,
                    claim: Some(format!("{}#{}", spec.id, requirement.id)),
                    criticality: None,
                    path: spec.path.clone(),
                    line: requirement.line,
                    detail: format!(
                        "requirement `{}` declares no criticality",
                        requirement.id
                    ),
                });
            }
        }
    }

    for claim in model.claims() {
        let id = claim.id();
        let criticality = claim.requirement.criticality;
        let severity = severity_for(criticality);

        let realized = model
            .realizes
            .iter()
            .any(|s| s.spec == claim.spec.id && s.scenario == claim.scenario.id);
        if !realized {
            holes.push(Hole {
                kind: HoleKind::Unrealized,
                severity,
                claim: Some(id.clone()),
                criticality,
                path: claim.spec.path.clone(),
                line: claim.scenario.line,
                detail: "no production code realizes this claim".into(),
            });
        }

        // A `routine` claim cannot be uncovered: D6.5 requires no verification plan for it, so no
        // evidence was ever demanded. Reporting it would make the level meaningless.
        let required = model.required_for(&claim);
        let evidence_required = match required {
            Some(r) => r.strength.is_some(),
            None => criticality.map(|c| c.requires_evidence()).unwrap_or(true),
        };
        if !evidence_required {
            continue;
        }

        let tags: Vec<&Site> = model
            .covers
            .iter()
            .filter(|s| s.spec == claim.spec.id && s.scenario == claim.scenario.id)
            .collect();

        // Non-test evidence declared in the plan. The machine cannot verify a manual pass or an
        // attestation; that is the agent tier's job (D14). What it can do is refuse to let the
        // item stand in for a stronger requirement than it claims.
        let declared = model
            .plan_for(&claim.spec.id)
            .and_then(|p| p.entry(&claim.scenario.id))
            .and_then(|e| e.evidence.as_ref());

        if tags.is_empty() && declared.is_none() {
            holes.push(Hole {
                kind: HoleKind::Uncovered,
                severity,
                claim: Some(id),
                criticality,
                path: claim.spec.path.clone(),
                line: claim.scenario.line,
                detail: "no evidence covers this claim".into(),
            });
            continue;
        }

        let Some(required) = required else { continue };
        let Some(min_strength) = required.strength else { continue };

        // D7's identity: strong enforcement is self-evidencing, so proof-strength evidence
        // satisfies a demonstration requirement without any test. The old model penalized exactly
        // this design.
        let satisfied_by_declaration = declared.is_some_and(|e| e.strength >= min_strength);
        let satisfied_by_test = tags.iter().any(|t| satisfies(t, &required));

        if !satisfied_by_declaration && !satisfied_by_test {
            holes.push(Hole {
                kind: HoleKind::WrongForm,
                severity,
                claim: Some(id),
                criticality,
                path: claim.spec.path.clone(),
                line: claim.scenario.line,
                detail: format!(
                    "requires {} at {} scope, {} quantification; found {}",
                    min_strength.name(),
                    required.scope.name(),
                    required.quantification.map(|q| q.name()).unwrap_or("any"),
                    describe_evidence(&tags, declared)
                ),
            });
        }
    }

    holes.extend(plan_holes(model));

    for (sites, kind) in
        [(&model.covers, HoleKind::DanglingTag), (&model.realizes, HoleKind::DanglingRealization)]
    {
        for site in sites {
            if !model.has_claim(&site.spec, &site.scenario) {
                holes.push(Hole {
                    kind,
                    severity: Severity::Error,
                    claim: Some(format!("{}#{}", site.spec, site.scenario)),
                    criticality: None,
                    path: site.file.clone(),
                    line: 0,
                    detail: format!("`{}` names a claim that does not exist", site.site),
                });
            }
        }
    }

    for test in &model.untraced {
        holes.push(Hole {
            kind: HoleKind::UntracedTest,
            severity: Severity::Error,
            claim: None,
            criticality: None,
            path: test.file.clone(),
            line: 0,
            detail: format!("`{}` declares no claim and is not exempt", test.site),
        });
    }

    holes.sort_by(|a, b| {
        (a.path.clone(), a.line, a.kind.name()).cmp(&(b.path.clone(), b.line, b.kind.name()))
    });
    holes
}

/// A tag declares what the test *actually* is. An emitter that omits a form is read at the weakest
/// rung rather than the strongest — an unstated claim should never satisfy a requirement.
fn satisfies(tag: &Site, required: &Required) -> bool {
    let scope = tag.scope.unwrap_or(Scope::Unit);
    let quantification = tag.quantification.unwrap_or(Quantification::Example);
    Strength::Demonstration >= required.strength.unwrap_or(Strength::Detection)
        && scope >= required.scope
        && required.quantification.is_none_or(|q| quantification >= q)
}

fn describe_evidence(tags: &[&Site], declared: Option<&EvidenceItem>) -> String {
    let mut parts: Vec<String> = tags
        .iter()
        .map(|t| {
            format!(
                "{} ({}/{})",
                t.site,
                t.scope.map(|s| s.name()).unwrap_or("unit, undeclared"),
                t.quantification.map(|q| q.name()).unwrap_or("example, undeclared")
            )
        })
        .collect();
    if let Some(e) = declared {
        parts.push(format!("declared {} evidence", e.strength.name()));
    }
    if parts.is_empty() {
        "nothing".into()
    } else {
        parts.join(", ")
    }
}

/// Holes about the plan itself rather than about a claim's facets.
fn plan_holes(model: &Model) -> Vec<Hole> {
    let mut holes = Vec::new();
    for plan in &model.plans {
        let spec_exists = model.specs.iter().any(|s| s.id == plan.spec);
        if !spec_exists {
            holes.push(Hole {
                kind: HoleKind::DanglingPlanEntry,
                severity: Severity::Error,
                claim: Some(plan.spec.clone()),
                criticality: None,
                path: plan.path.clone(),
                line: 1,
                detail: format!("plans spec `{}`, which does not exist", plan.spec),
            });
            continue;
        }

        for entry in &plan.entries {
            let Some(claim) = model.find_claim(&plan.spec, &entry.scenario) else {
                holes.push(Hole {
                    kind: HoleKind::DanglingPlanEntry,
                    severity: Severity::Error,
                    claim: Some(format!("{}#{}", plan.spec, entry.scenario)),
                    criticality: None,
                    path: plan.path.clone(),
                    line: entry.line,
                    detail: "names a claim that does not exist".into(),
                });
                continue;
            };

            // "Silent weakening is not available." A plan may require less than the standard, but
            // only with an accepted residual (D6.3 applied to evidence).
            let (Some(standards), Some(criticality)) =
                (model.standards.as_ref(), claim.requirement.criticality)
            else {
                continue;
            };
            let Some(level) = standards.for_level(criticality) else { continue };
            let weakened = match (entry.quantification, level.quantification) {
                (Some(entry_q), Some(level_q)) => entry_q < level_q,
                _ => false,
            };
            if weakened && (entry.residual.is_none() || entry.accepted.is_none()) {
                holes.push(Hole {
                    kind: HoleKind::UnacceptedWeakening,
                    severity: Severity::Error,
                    claim: Some(format!("{}#{}", plan.spec, entry.scenario)),
                    criticality: Some(criticality),
                    path: plan.path.clone(),
                    line: entry.line,
                    detail: format!(
                        "requires {} where the {} standard is {}, with no accepted residual",
                        entry.quantification.unwrap().name(),
                        criticality.name(),
                        level.quantification.unwrap().name()
                    ),
                });
            }
        }
    }
    holes
}

pub struct Summary {
    pub claims: usize,
    pub errors: usize,
    pub warnings: usize,
}

pub fn summarize(model: &Model, holes: &[Hole]) -> Summary {
    Summary {
        claims: model.scenario_count(),
        errors: holes.iter().filter(|h| h.severity == Severity::Error).count(),
        warnings: holes.iter().filter(|h| h.severity == Severity::Warning).count(),
    }
}

pub fn counts_by_kind(holes: &[Hole]) -> Vec<(&'static str, usize)> {
    let kinds = [
        HoleKind::DanglingPlanEntry,
        HoleKind::UnacceptedWeakening,
        HoleKind::WrongForm,
        HoleKind::Unclassified,
        HoleKind::Unrealized,
        HoleKind::Uncovered,
        HoleKind::DanglingTag,
        HoleKind::DanglingRealization,
        HoleKind::UntracedTest,
    ];
    kinds
        .iter()
        .map(|k| (k.name(), holes.iter().filter(|h| h.kind == *k).count()))
        .filter(|(_, n)| *n > 0)
        .collect()
}
