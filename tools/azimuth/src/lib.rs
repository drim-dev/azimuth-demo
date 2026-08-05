//! Azimuth core.
//!
//! Reads claims, linkage tags and (later) evidence; derives a model; runs checks over it. The
//! model is a first-class artifact (D10): checks, dashboards, PR annotations and the agent tier
//! are all consumers of the export, and nothing else re-parses specs.
//!
//! No dependencies, by decision (D17).

pub mod check;
pub mod design;
pub mod diag;
pub mod json;
pub mod manifest;
pub mod labels;
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
/// `trip/**` matches every spec whose id starts with `trip/`; anything else matches exactly.
pub fn selects(pattern: &str, spec_id: &str) -> bool {
    match pattern.strip_suffix("/**") {
        Some(prefix) => spec_id == prefix || spec_id.starts_with(&format!("{prefix}/")),
        None => pattern == spec_id,
    }
}

pub fn load(
    specs_dir: &Path,
    verification_dir: &Path,
    design_dir: &Path,
    manifests: &[std::path::PathBuf],
    only: &[String],
) -> Result<Loaded, Vec<Diag>> {
    let loaded = spec::load_specs(specs_dir)?;
    let mut model = Model { specs: loaded.specs, ..Default::default() };
    let mut warnings = loaded.warnings;

    if !only.is_empty() {
        model.specs.retain(|s| only.iter().any(|p| selects(p, &s.id)));
    }

    let mut errors = Vec::new();

    // Without a standards file nothing is known to require, so `wrong-form` cannot fire. Say so
    // rather than reporting a clean run that only looks clean.
    let standards_path = verification_dir.join("standards.md");
    if standards_path.exists() {
        match plan::load_standards(&standards_path) {
            Ok(s) => model.standards = Some(s),
            Err(mut d) => errors.append(&mut d),
        }
    } else if verification_dir.exists() {
        warnings.push(Diag::file(
            &standards_path.display().to_string(),
            "no standards file; no evidence standard is known, so wrong-form cannot be reported",
        ));
    }

    match design::load_designs(design_dir) {
        Ok(designs) => {
            model.designs = designs;
            if !only.is_empty() {
                model.designs.retain(|d| only.iter().any(|pat| selects(pat, &d.spec)));
            }
        }
        Err(mut d) => errors.append(&mut d),
    }

    match plan::load_plans(verification_dir) {
        Ok(plans) => {
            model.plans = plans;
            if !only.is_empty() {
                model.plans.retain(|p| only.iter().any(|pat| selects(pat, &p.spec)));
            }
        }
        Err(mut d) => errors.append(&mut d),
    }

    for path in manifests {
        match manifest::load(path) {
            Ok(m) => {
                model.realizes.extend(m.realizes);
                model.covers.extend(m.covers);
                model.untraced.extend(m.untraced);
            }
            Err(mut d) => errors.append(&mut d),
        }
    }
    if !errors.is_empty() {
        return Err(errors);
    }

    if !only.is_empty() {
        model.realizes.retain(|s| only.iter().any(|p| selects(p, &s.spec)));
        model.covers.retain(|s| only.iter().any(|p| selects(p, &s.spec)));
    }

    Ok(Loaded { model, warnings })
}
