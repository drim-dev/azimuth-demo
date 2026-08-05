//! Checks over the model.
//!
//! `rtm` is one check among several, not the product (D9). Every hole kind it reports is a
//! missing-facet combination (D3) — intent without mechanism, intent without evidence, evidence
//! without intent, mechanism without intent. A hole kind that is *not* one of those would imply a
//! fourth facet, and is the recorded falsifier for D3.

use crate::json::Json;
use crate::model::{Criticality, Model};

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
    /// D6.2: a requirement without a declared criticality
    Unclassified,
}

impl HoleKind {
    pub fn name(self) -> &'static str {
        match self {
            HoleKind::Unrealized => "unrealized",
            HoleKind::Uncovered => "uncovered",
            HoleKind::DanglingTag => "dangling-tag",
            HoleKind::DanglingRealization => "dangling-realization",
            HoleKind::UntracedTest => "untraced-test",
            HoleKind::Unclassified => "unclassified",
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
        let evidence_required = criticality.map(|c| c.requires_evidence()).unwrap_or(true);
        if evidence_required {
            let covered = model
                .covers
                .iter()
                .any(|s| s.spec == claim.spec.id && s.scenario == claim.scenario.id);
            if !covered {
                holes.push(Hole {
                    kind: HoleKind::Uncovered,
                    severity,
                    claim: Some(id),
                    criticality,
                    path: claim.spec.path.clone(),
                    line: claim.scenario.line,
                    detail: "no evidence covers this claim".into(),
                });
            }
        }
    }

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
