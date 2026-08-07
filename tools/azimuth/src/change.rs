//! The change lifecycle is deliberately narrower than the accepted-state model.
//!
//! The machine derives additions and their obligations, and verifies that an accepted change was
//! applied before archiving. Explanations of departures and residual acceptance remain authored
//! because deriving them would manufacture the judgment the archive exists to preserve.

use crate::check::{self, Hole};
use crate::fingerprint::sha256;
use crate::json::Json;
use crate::model::{Criticality, Model, StepKind};
use crate::spec::parse_spec;
use std::fs;
use std::path::{Path, PathBuf};

#[derive(Debug, Clone)]
pub struct Addition {
    pub spec: String,
    pub requirement: String,
    pub criticality: Criticality,
    pub statement: String,
    pub scenarios: Vec<AddedScenario>,
    pub applied: bool,
}

#[derive(Debug, Clone)]
pub struct AddedScenario {
    pub id: String,
    pub steps: Vec<(StepKind, String)>,
}

#[derive(Debug)]
pub struct Report {
    pub id: String,
    pub additions: Vec<Addition>,
    pub incomplete_plan_items: usize,
    pub current_claims: usize,
    pub target_claims: usize,
}

pub fn inspect(root: &Path, model: &Model) -> Result<Report, Vec<String>> {
    let mut errors = Vec::new();
    let Some(id) = root
        .file_name()
        .and_then(|name| name.to_str())
        .map(str::to_string)
    else {
        return Err(vec!["change path has no id".into()]);
    };
    if !root.join("proposal.md").is_file() {
        errors.push("proposal.md is missing".into());
    }
    let plan = match fs::read_to_string(root.join("plan.md")) {
        Ok(plan) => plan,
        Err(_) => {
            errors.push("plan.md is missing".into());
            String::new()
        }
    };
    let incomplete_plan_items = plan
        .lines()
        .filter(|line| line.trim_start().starts_with("- [ ]"))
        .count();

    let mut additions = Vec::new();
    let specs = root.join("specs");
    if specs.exists() {
        let mut files = Vec::new();
        collect_markdown(&specs, &mut files, &mut errors);
        files.sort();
        for file in files {
            match fs::read_to_string(&file) {
                Ok(source) => parse_delta(&file, &source, model, &mut additions, &mut errors),
                Err(error) => errors.push(format!("cannot read {}: {error}", file.display())),
            }
        }
    }

    if !errors.is_empty() {
        return Err(errors);
    }
    let unapplied_claims: usize = additions
        .iter()
        .filter(|addition| !addition.applied)
        .map(|addition| addition.scenarios.len())
        .sum();
    Ok(Report {
        id,
        additions,
        incomplete_plan_items,
        current_claims: model.scenario_count(),
        target_claims: model.scenario_count() + unapplied_claims,
    })
}

fn collect_markdown(root: &Path, files: &mut Vec<PathBuf>, errors: &mut Vec<String>) {
    let entries = match fs::read_dir(root) {
        Ok(entries) => entries,
        Err(error) => {
            errors.push(format!("cannot read {}: {error}", root.display()));
            return;
        }
    };
    for entry in entries.flatten() {
        let path = entry.path();
        if path.is_dir() {
            collect_markdown(&path, files, errors);
        } else if path.extension().and_then(|extension| extension.to_str()) == Some("md") {
            files.push(path);
        }
    }
}

fn parse_delta(
    path: &Path,
    source: &str,
    model: &Model,
    additions: &mut Vec<Addition>,
    errors: &mut Vec<String>,
) {
    let mut normalized = String::new();
    let mut lines = source.lines();
    let Some(first) = lines.next() else {
        errors.push(format!(
            "{}: expected `# Intent delta: <spec-id>`",
            path.display()
        ));
        return;
    };
    let Some(spec) = first.trim().strip_prefix("# Intent delta:").map(str::trim) else {
        errors.push(format!(
            "{}: expected `# Intent delta: <spec-id>`",
            path.display()
        ));
        return;
    };
    normalized.push_str("# Spec: ");
    normalized.push_str(spec);
    normalized.push('\n');

    for (offset, line) in lines.enumerate() {
        let trimmed = line.trim();
        if let Some(requirement) = trimmed.strip_prefix("## Add requirement:") {
            normalized.push_str("## Requirement:");
            normalized.push_str(requirement);
        } else if let Some(scenario) = trimmed.strip_prefix("### Add scenario:") {
            normalized.push_str("### Scenario:");
            normalized.push_str(scenario);
        } else if trimmed.starts_with("## ") || trimmed.starts_with("### ") {
            errors.push(format!(
                "{}:{}: unsupported delta operation `{trimmed}`; only additions are machine-projected",
                path.display(),
                offset + 2,
            ));
            continue;
        } else {
            normalized.push_str(line);
        }
        normalized.push('\n');
    }

    if !errors.is_empty() {
        return;
    }
    let parsed = match parse_spec(&path.display().to_string(), &normalized) {
        Ok(parsed) => parsed,
        Err(diags) => {
            errors.extend(diags.into_iter().map(|diag| diag.to_string()));
            return;
        }
    };

    for requirement in parsed.requirements {
        let Some(criticality) = requirement.criticality else {
            errors.push(format!(
                "{}: `{}` declares no criticality",
                path.display(),
                requirement.id
            ));
            continue;
        };
        if requirement.scenarios.is_empty() {
            errors.push(format!(
                "{}: `{}` adds no scenarios",
                path.display(),
                requirement.id
            ));
            continue;
        }
        let scenarios: Vec<AddedScenario> = requirement
            .scenarios
            .iter()
            .map(|scenario| AddedScenario {
                id: scenario.id.clone(),
                steps: scenario
                    .steps
                    .iter()
                    .map(|step| (step.kind, step.text.clone()))
                    .collect(),
            })
            .collect();
        let applied = model.specs.iter().any(|candidate| {
            candidate.id == parsed.id
                && candidate.requirements.iter().any(|existing| {
                    existing.id == requirement.id
                        && existing.criticality == Some(criticality)
                        && existing.statement == requirement.statement
                        && scenarios.iter().all(|scenario| {
                            existing.scenarios.iter().any(|item| {
                                item.id == scenario.id
                                    && item.steps.len() == scenario.steps.len()
                                    && item.steps.iter().zip(&scenario.steps).all(
                                        |(current, target)| {
                                            current.kind == target.0 && current.text == target.1
                                        },
                                    )
                            })
                        })
                })
        });
        additions.push(Addition {
            spec: parsed.id.clone(),
            requirement: requirement.id,
            criticality,
            statement: requirement.statement,
            scenarios,
            applied,
        });
    }
}

pub fn completion_issues(root: &Path, report: &Report) -> Vec<String> {
    let mut issues = Vec::new();
    if report.additions.is_empty() {
        issues.push("change declares no additive intent delta".into());
    }
    if report.incomplete_plan_items > 0 {
        issues.push(format!(
            "{} plan item(s) remain incomplete",
            report.incomplete_plan_items
        ));
    }
    for addition in &report.additions {
        if !addition.applied {
            issues.push(format!(
                "{}#{} has not been applied to current specs",
                addition.spec, addition.requirement
            ));
        }
    }
    let proposal = fs::read_to_string(root.join("proposal.md")).unwrap_or_default();
    if !proposal.lines().any(|line| {
        line.trim()
            .eq_ignore_ascii_case("Status: accepted and complete")
            || line
                .trim()
                .eq_ignore_ascii_case("Status: **accepted and complete**")
    }) {
        issues.push("proposal status is not `accepted and complete`".into());
    }
    let outcome = fs::read_to_string(root.join("outcome.md")).unwrap_or_default();
    if outcome.is_empty() {
        issues.push("outcome.md is missing".into());
    } else {
        for heading in ["## Departures", "## Residual decisions", "## Measurements"] {
            if !outcome.lines().any(|line| line.trim() == heading) {
                issues.push(format!("outcome.md is missing `{heading}`"));
            }
        }
        if !outcome
            .lines()
            .any(|line| line.trim() == "Status: accepted")
        {
            issues.push("outcome status is not `accepted`".into());
        }
    }
    issues
}

pub fn finalization(model: &Model, holes: &[Hole]) -> (String, String) {
    let model_json = model.to_json(holes).to_string_pretty();
    let fingerprint = sha256(model_json.as_bytes());
    let summary = check::summarize(model, holes);
    let json = Json::obj(vec![
        ("version", Json::Num(1.0)),
        ("model_fingerprint", Json::str(&fingerprint)),
        ("claims", Json::Num(summary.claims as f64)),
        ("specs", Json::Num(model.specs.len() as f64)),
        ("errors", Json::Num(summary.errors as f64)),
        ("warnings", Json::Num(summary.warnings as f64)),
    ])
    .to_string_pretty();
    (fingerprint, json)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::spec::parse_spec;
    use std::time::{SystemTime, UNIX_EPOCH};

    #[test]
    fn additions_project_until_the_current_facet_contains_them() {
        let root = temp_change();
        fs::create_dir_all(root.join("specs")).unwrap();
        fs::write(root.join("proposal.md"), "# Change: x\n\nStatus: active\n").unwrap();
        fs::write(root.join("plan.md"), "- [ ] Apply it.\n").unwrap();
        fs::write(
            root.join("specs/alpha.md"),
            "# Intent delta: alpha\n\n## Add requirement: added\nCriticality: routine\n\nText.\n\n### Add scenario: visible\nWHEN x\nTHEN y\n",
        )
        .unwrap();
        let current = parse_spec(
            "alpha.md",
            "# Spec: alpha\n\n## Requirement: old\nCriticality: routine\n\nOld.\n\n### Scenario: existing\nWHEN x\nTHEN y\n",
        )
        .unwrap();
        let model = Model {
            specs: vec![current],
            ..Default::default()
        };
        let report = inspect(&root, &model).unwrap();
        assert_eq!(report.current_claims, 1);
        assert_eq!(report.target_claims, 2);
        assert!(!report.additions[0].applied);
        fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn an_applied_addition_requires_the_same_statement_and_steps() {
        let root = temp_change();
        fs::create_dir_all(root.join("specs")).unwrap();
        fs::write(root.join("proposal.md"), "# Change: x\n\nStatus: active\n").unwrap();
        fs::write(root.join("plan.md"), "- [ ] Apply it.\n").unwrap();
        fs::write(
            root.join("specs/alpha.md"),
            "# Intent delta: alpha\n\n## Add requirement: added\nCriticality: routine\n\nTarget statement.\n\n### Add scenario: visible\nWHEN x\nTHEN target behavior\n",
        )
        .unwrap();
        let current = parse_spec(
            "alpha.md",
            "# Spec: alpha\n\n## Requirement: added\nCriticality: routine\n\nDifferent statement.\n\n### Scenario: visible\nWHEN x\nTHEN different behavior\n",
        )
        .unwrap();
        let model = Model {
            specs: vec![current],
            ..Default::default()
        };

        let report = inspect(&root, &model).unwrap();

        assert!(!report.additions[0].applied);
        fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn completion_requires_an_intent_delta() {
        let root = temp_change();
        fs::write(
            root.join("proposal.md"),
            "# Change: x\n\nStatus: accepted and complete\n",
        )
        .unwrap();
        fs::write(root.join("plan.md"), "- [x] Complete.\n").unwrap();
        fs::write(
            root.join("outcome.md"),
            "# Outcome: x\n\nStatus: accepted\n\n## Departures\n\nNone.\n\n## Residual decisions\n\nNone.\n\n## Measurements\n\nNone.\n",
        )
        .unwrap();
        let report = inspect(&root, &Model::default()).unwrap();

        assert_eq!(
            completion_issues(&root, &report),
            vec!["change declares no additive intent delta"]
        );
        fs::remove_dir_all(root).unwrap();
    }

    fn temp_change() -> PathBuf {
        let nonce = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root = std::env::temp_dir().join(format!("azimuth-change-{nonce}"));
        fs::create_dir_all(&root).unwrap();
        root
    }
}
