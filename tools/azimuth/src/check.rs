//! Checks over the model.
//!
//! `rtm` is one check among several, not the product (D9). Every hole kind it reports is a
//! missing-facet combination (D3) — intent without mechanism, intent without evidence, evidence
//! without intent, mechanism without intent. A hole kind that is *not* one of those would imply a
//! fourth facet, and is the recorded falsifier for D3.

use crate::design::Target;
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
    /// mechanism present, intent absent — a design entry for a requirement that does not exist
    DanglingDesignEntry,
    /// D6.5 requires a design entry for a `critical` requirement. Incomplete-facet: the mechanism
    /// may well exist in code, but its strategy is undeclared and therefore uncheckable.
    UndeclaredMechanism,
    /// A plan declares proof-strength evidence that no proof-capable mechanism backs.
    UnbackedProof,
    /// The agent tier judged the covering evidence as not discriminating: it exists, it passes, and
    /// it would also pass against an implementation that is wrong.
    ToothlessEvidence,
    /// The agent tier judged a tag as declaring a stronger form than the test has.
    DishonestTag,
    /// The agent tier found a behaviour the spec should name and does not.
    SpecGap,
    /// A judgment whose fingerprint no longer matches what it looked at.
    StaleJudgment,
    /// A critical claim the agent tier has never judged. Incomplete-facet, like `Unclassified`.
    Unjudged,
    /// A site that joined a claim's class and discharges nothing.
    ///
    /// The one hole kind the per-scenario matrix structurally cannot find: a claim quantified over
    /// a *set of sites* is not established by evidence about one site, however good.
    InvariantBreach,
    /// An invariant naming a class no spec defines.
    DanglingClass,
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
            HoleKind::DanglingDesignEntry => "dangling-design-entry",
            HoleKind::UndeclaredMechanism => "undeclared-mechanism",
            HoleKind::UnbackedProof => "unbacked-proof",
            HoleKind::ToothlessEvidence => "toothless-evidence",
            HoleKind::DishonestTag => "dishonest-tag-judged",
            HoleKind::SpecGap => "spec-gap",
            HoleKind::StaleJudgment => "stale-judgment",
            HoleKind::Unjudged => "unjudged",
            HoleKind::InvariantBreach => "invariant-breach",
            HoleKind::DanglingClass => "dangling-class",
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
    holes.extend(design_holes(model));
    holes.extend(judgment_holes(model));
    holes.extend(surface_holes(model));

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

/// Holes about the mechanism facet, and the first check that needs all three artifacts.
fn design_holes(model: &Model) -> Vec<Hole> {
    let mut holes = Vec::new();

    for design in &model.designs {
        let Some(spec) = model.specs.iter().find(|s| s.id == design.spec) else {
            holes.push(Hole {
                kind: HoleKind::DanglingDesignEntry,
                severity: Severity::Error,
                claim: Some(design.spec.clone()),
                criticality: None,
                path: design.path.clone(),
                line: 1,
                detail: format!("designs spec `{}`, which does not exist", design.spec),
            });
            continue;
        };
        for entry in &design.entries {
            let id = entry.target.id();
            let exists = match &entry.target {
                Target::Requirement(_) => spec.requirements.iter().any(|r| r.id == id),
                Target::Scenario(_) => {
                    spec.requirements.iter().any(|r| r.scenarios.iter().any(|s| s.id == id))
                }
            };
            if !exists {
                holes.push(Hole {
                    kind: HoleKind::DanglingDesignEntry,
                    severity: Severity::Error,
                    claim: Some(format!("{}#{}", design.spec, id)),
                    criticality: None,
                    path: design.path.clone(),
                    line: entry.line,
                    detail: "names a requirement or claim that does not exist".into(),
                });
            }
        }
    }

    // D6.5: a design entry is required for `critical`, optional for `standard`, absent for
    // `routine`. Nothing here says the mechanism is missing from the code — only that its strategy
    // is undeclared, and therefore that no check can ever compare claim against reality.
    //
    // Gated on the artifact being in use at all. D8.1 requires each mechanism to be usable alone —
    // `rtm` without the design artifact — and a project that has not adopted it must not be told
    // that every critical requirement is a hole. Partial adoption still reports: one design file
    // means the artifact is in use, and the specs it omits are visible.
    for spec in &model.specs {
        if model.designs.is_empty() {
            break;
        }
        let design = model.design_for(&spec.id);
        for requirement in &spec.requirements {
            if requirement.criticality != Some(Criticality::Critical) {
                continue;
            }
            let declared = design.is_some_and(|d| {
                d.for_requirement(&requirement.id).is_some()
                    || requirement.scenarios.iter().any(|s| d.for_scenario(&s.id).is_some())
            });
            if !declared {
                holes.push(Hole {
                    kind: HoleKind::UndeclaredMechanism,
                    severity: Severity::Error,
                    claim: Some(format!("{}#{}", spec.id, requirement.id)),
                    criticality: requirement.criticality,
                    path: spec.path.clone(),
                    line: requirement.line,
                    detail: "critical requirement declares no enforcement mechanism".into(),
                });
            }
        }
    }

    // The three-artifact check. A plan may cite proof-strength evidence, but proof comes from a
    // mechanism at the top of the enforcement ladder (D7) — and the developer owns which mechanism
    // that is. A plan claiming proof with no proof-capable mechanism behind it is asserting the
    // strongest available result out of thin air.
    for plan in &model.plans {
        for entry in &plan.entries {
            let Some(evidence) = &entry.evidence else { continue };
            if evidence.strength != Strength::Proof {
                continue;
            }
            let Some(claim) = model.find_claim(&plan.spec, &entry.scenario) else { continue };
            let backed = model.design_for(&plan.spec).is_some_and(|d| {
                let for_scenario = d.for_scenario(&entry.scenario);
                let for_requirement = d.for_requirement(&claim.requirement.id);
                for_scenario
                    .into_iter()
                    .chain(for_requirement)
                    .any(|e| e.mechanisms.iter().any(|m| m.kind.is_proof_capable()))
            });
            if !backed {
                holes.push(Hole {
                    kind: HoleKind::UnbackedProof,
                    severity: Severity::Error,
                    claim: Some(format!("{}#{}", plan.spec, entry.scenario)),
                    criticality: claim.requirement.criticality,
                    path: plan.path.clone(),
                    line: entry.line,
                    detail: format!(
                        "claims proof-strength evidence, but `{}` declares no mechanism at the top \
                         two rungs of the enforcement ladder",
                        claim.requirement.id
                    ),
                });
            }
        }
    }

    holes
}

/// Claims whose domain is a set of sites (D13).
///
/// Membership is **derived**: a site joins the class by realizing any claim in the named spec, so
/// it cannot escape by forgetting to declare itself. That is D13.1 — an enumerator must come from
/// the same source the system is built from, because a hand-listed surface reproduces the very bug
/// the rule prevents and reports green.
///
/// Discharge is a `realizes` tag naming the invariant. No new tag: one claim type parameterized by
/// domain means one of everything downstream.
///
/// **Limitation, stated rather than hidden.** This verifies the weakest rung of the enforcement
/// ladder — a guard at every site. A choke point that every member routes through would show as N−1
/// breaches, which is exactly the defect D7 names in the alpha. Crediting a choke point needs
/// call-graph analysis, which belongs to the extractor and D10.1's code-consuming check class.
fn surface_holes(model: &Model) -> Vec<Hole> {
    let mut holes = Vec::new();

    for spec in &model.specs {
        for requirement in &spec.requirements {
            if requirement.domain != crate::model::Domain::Sites {
                continue;
            }
            let Some(over) = &requirement.over else { continue };
            let Some(class_spec) = model.specs.iter().find(|s| &s.id == over) else {
                holes.push(Hole {
                    kind: HoleKind::DanglingClass,
                    severity: Severity::Error,
                    claim: Some(format!("{}#{}", spec.id, requirement.id)),
                    criticality: requirement.criticality,
                    path: spec.path.clone(),
                    line: requirement.line,
                    detail: format!("`Over: {over}` names a spec that does not exist"),
                });
                continue;
            };

            let behavioural: Vec<&str> = class_spec
                .requirements
                .iter()
                .filter(|r| r.domain == crate::model::Domain::Behaviour)
                .flat_map(|r| r.scenarios.iter().map(|s| s.id.as_str()))
                .collect();

            let mut members: Vec<(&str, &str)> = model
                .realizes
                .iter()
                .filter(|site| site.spec == class_spec.id && behavioural.contains(&site.scenario.as_str()))
                .map(|site| (site.site.as_str(), site.file.as_str()))
                .collect();
            members.sort();
            members.dedup();

            let discharged: Vec<&str> = model
                .realizes
                .iter()
                .filter(|site| site.spec == spec.id && site.scenario == requirement.id)
                .map(|site| site.site.as_str())
                .collect();

            for (site, file) in members {
                if discharged.contains(&site) {
                    continue;
                }
                holes.push(Hole {
                    kind: HoleKind::InvariantBreach,
                    severity: severity_for(requirement.criticality),
                    claim: Some(format!("{}#{}", spec.id, requirement.id)),
                    criticality: requirement.criticality,
                    path: file.to_string(),
                    line: 0,
                    detail: format!("`{site}` is in the class and discharges nothing"),
                });
            }
        }
    }

    holes
}

/// Holes the machine tier cannot find on its own.
///
/// The machine makes structure checkable; it does not make truth checkable. Everything here comes
/// from a verdict the agent tier recorded, and the tool's contribution is to hold that verdict to a
/// fingerprint so it cannot quietly outlive what it judged.
fn judgment_holes(model: &Model) -> Vec<Hole> {
    let mut holes = Vec::new();
    if model.judgments.is_empty() {
        // D8.1: each mechanism is usable alone. A project that has not adopted the agent tier is
        // not told that every critical claim is unjudged.
        return holes;
    }

    for claim in model.claims() {
        let id = claim.id();
        let judged = model.judgments_for(&claim.spec.id).and_then(|j| j.entry(&claim.scenario.id));

        let Some(judgment) = judged else {
            if claim.requirement.criticality == Some(Criticality::Critical) {
                holes.push(Hole {
                    kind: HoleKind::Unjudged,
                    severity: Severity::Error,
                    claim: Some(id),
                    criticality: claim.requirement.criticality,
                    path: claim.spec.path.clone(),
                    line: claim.scenario.line,
                    detail: "critical claim carries no agent-tier judgment".into(),
                });
            }
            continue;
        };

        let expected = crate::judgment::fingerprint(
            &model.claim_text(&claim),
            model.evidence_files(&claim.spec.id, &claim.scenario.id),
        );
        let path = model
            .judgments_for(&claim.spec.id)
            .map(|j| j.path.clone())
            .unwrap_or_default();

        if judgment.fingerprint != expected {
            holes.push(Hole {
                kind: HoleKind::StaleJudgment,
                severity: severity_for(claim.requirement.criticality),
                claim: Some(id.clone()),
                criticality: claim.requirement.criticality,
                path: path.clone(),
                line: judgment.line,
                detail: format!(
                    "judged `{}` against {}, but the claim or its evidence has changed since (now {})",
                    judgment.verdict.name(),
                    judgment.fingerprint,
                    expected
                ),
            });
            continue;
        }

        let kind = match judgment.verdict {
            crate::judgment::Verdict::Sound => continue,
            crate::judgment::Verdict::Toothless => HoleKind::ToothlessEvidence,
            crate::judgment::Verdict::DishonestTag => HoleKind::DishonestTag,
            crate::judgment::Verdict::SpecGap => HoleKind::SpecGap,
        };

        holes.push(Hole {
            kind,
            severity: severity_for(claim.requirement.criticality),
            claim: Some(id),
            criticality: claim.requirement.criticality,
            path,
            line: judgment.line,
            detail: judgment.reason.clone(),
        });
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
        HoleKind::DanglingDesignEntry,
        HoleKind::UndeclaredMechanism,
        HoleKind::UnbackedProof,
        HoleKind::UnacceptedWeakening,
        HoleKind::WrongForm,
        HoleKind::Unjudged,
        HoleKind::StaleJudgment,
        HoleKind::ToothlessEvidence,
        HoleKind::DishonestTag,
        HoleKind::SpecGap,
        HoleKind::InvariantBreach,
        HoleKind::DanglingClass,
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
