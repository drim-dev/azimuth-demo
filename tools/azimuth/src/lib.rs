//! Azimuth core.
//!
//! Reads claims, linkage tags and (later) evidence; derives a model; runs checks over it. The
//! model is a first-class artifact (D10): checks, dashboards, PR annotations and the agent tier
//! are all consumers of the export, and nothing else re-parses specs.
//!
//! No dependencies, by decision (D17).

pub mod change;
pub mod check;
pub mod design;
pub mod diag;
pub mod fingerprint;
pub mod json;
pub mod judgment;
pub mod labels;
pub mod manifest;
pub mod model;
pub mod plan;
pub mod spec;

use diag::Diag;
use model::Model;
use std::path::Path;

pub struct Loaded {
    pub model: Model,
    pub warnings: Vec<Diag>,
}

/// Selection operates on **ids**, not paths — the hierarchy lives in the id string, so scoping
/// keeps working if the folders are reorganized tomorrow.
///
/// `billing/**` matches every spec whose id starts with `billing/`; anything else matches exactly.
pub fn selects(pattern: &str, spec_id: &str) -> bool {
    match pattern.strip_suffix("/**") {
        Some(prefix) => spec_id == prefix || spec_id.starts_with(&format!("{prefix}/")),
        None => pattern == spec_id,
    }
}

pub fn load(
    model_dir: &Path,
    standards_path: &Path,
    manifests: &[std::path::PathBuf],
    only: &[String],
) -> Result<Loaded, Vec<Diag>> {
    let loaded = spec::load_specs(model_dir)?;
    let mut model = Model {
        specs: loaded.specs,
        ..Default::default()
    };
    let mut warnings = loaded.warnings;

    if !only.is_empty() {
        model
            .specs
            .retain(|s| only.iter().any(|p| selects(p, &s.id)));
    }

    let mut errors = Vec::new();

    // Without a standards file nothing is known to require, so `wrong-form` cannot fire. Say so
    // rather than reporting a clean run that only looks clean.
    if standards_path.exists() {
        match plan::load_standards(standards_path) {
            Ok(s) => model.standards = Some(s),
            Err(mut d) => errors.append(&mut d),
        }
    } else if model_dir.exists() {
        warnings.push(Diag::file(
            &standards_path.display().to_string(),
            "no standards file; no evidence standard is known, so wrong-form cannot be reported",
        ));
    }

    match judgment::load(model_dir) {
        Ok(js) => {
            model.judgments = js;
            if !only.is_empty() {
                model
                    .judgments
                    .retain(|j| only.iter().any(|p| selects(p, &j.spec)));
            }
        }
        Err(mut d) => errors.append(&mut d),
    }

    match design::load_designs(model_dir) {
        Ok(designs) => {
            model.designs = designs;
            if !only.is_empty() {
                model
                    .designs
                    .retain(|d| only.iter().any(|pat| selects(pat, &d.spec)));
            }
        }
        Err(mut d) => errors.append(&mut d),
    }

    match plan::load_plans(model_dir) {
        Ok(plans) => {
            model.plans = plans;
            if !only.is_empty() {
                model
                    .plans
                    .retain(|p| only.iter().any(|pat| selects(pat, &p.spec)));
            }
        }
        Err(mut d) => errors.append(&mut d),
    }

    for path in manifests {
        match manifest::load(path) {
            Ok(m) => {
                model.realizes.extend(m.realizes);
                model.covers.extend(m.covers);
                model
                    .mechanism_implementations
                    .extend(m.mechanism_implementations);
                model.mechanism_covers.extend(m.mechanism_covers);
                model.class_members.extend(m.class_members);
                model.enumerations.extend(m.enumerations);
                model.artifacts.extend(m.artifacts);
            }
            Err(mut d) => errors.append(&mut d),
        }
    }
    if !errors.is_empty() {
        return Err(errors);
    }

    if !only.is_empty() {
        model
            .realizes
            .retain(|s| only.iter().any(|p| selects(p, &s.spec)));
        model
            .covers
            .retain(|s| only.iter().any(|p| selects(p, &s.spec)));
        model
            .mechanism_implementations
            .retain(|s| only.iter().any(|p| selects(p, &s.spec)));
        model
            .mechanism_covers
            .retain(|s| only.iter().any(|p| selects(p, &s.spec)));
    }

    warnings.extend(package_location_warnings(&model));

    Ok(Loaded { model, warnings })
}

fn package_location_warnings(model: &Model) -> Vec<Diag> {
    let mut warnings = Vec::new();
    for design in &model.designs {
        warn_if_not_sibling(model, &design.spec, &design.path, "design", &mut warnings);
    }
    for plan in &model.plans {
        warn_if_not_sibling(
            model,
            &plan.spec,
            &plan.path,
            "verification plan",
            &mut warnings,
        );
    }
    for judgments in &model.judgments {
        warn_if_not_sibling(
            model,
            &judgments.spec,
            &judgments.path,
            "judgments",
            &mut warnings,
        );
    }
    warnings
}

fn warn_if_not_sibling(
    model: &Model,
    spec_id: &str,
    artifact_path: &str,
    artifact_kind: &str,
    warnings: &mut Vec<Diag>,
) {
    let Some(spec) = model.specs.iter().find(|spec| spec.id == spec_id) else {
        return;
    };
    if Path::new(&spec.path).parent() == Path::new(artifact_path).parent() {
        return;
    }
    warnings.push(Diag::at(
        artifact_path,
        1,
        format!(
            "{artifact_kind} for `{spec_id}` is not beside {}; ids are path-independent, so this \
             is a navigation hint only",
            spec.path
        ),
    ));
}
