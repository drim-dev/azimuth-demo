//! Azimuth core.
//!
//! Reads claims, linkage tags and (later) evidence; derives a model; runs checks over it. The
//! model is a first-class artifact (D10): checks, dashboards, PR annotations and the agent tier
//! are all consumers of the export, and nothing else re-parses specs.
//!
//! No dependencies, by decision (D17).

pub mod check;
pub mod diag;
pub mod json;
pub mod manifest;
pub mod model;
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
    manifests: &[std::path::PathBuf],
    only: &[String],
) -> Result<Loaded, Vec<Diag>> {
    let loaded = spec::load_specs(specs_dir)?;
    let mut model = Model { specs: loaded.specs, ..Default::default() };

    if !only.is_empty() {
        model.specs.retain(|s| only.iter().any(|p| selects(p, &s.id)));
    }

    let mut errors = Vec::new();
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

    Ok(Loaded { model, warnings: loaded.warnings })
}
