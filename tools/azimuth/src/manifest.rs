//! Manifest reading.
//!
//! Ported from the alpha with one change (D2.2): the key is the **pair** `(spec, scenario)`, not
//! the triple. Dropping the requirement id is what makes splitting or merging a requirement free —
//! scenarios move between parents without a single tag being touched.
//!
//! Each ecosystem emits this natively; the core only ever reads it. That is also why the core can
//! stay dependency-free while code-consuming checks still get AST access (D17).

use crate::diag::Diag;
use crate::json::{self, Json};
use crate::model::{Quantification, Scope, Site, UntracedTest};
use std::fs;
use std::path::Path;

#[derive(Debug, Default)]
pub struct Manifest {
    pub realizes: Vec<Site>,
    pub covers: Vec<Site>,
    pub untraced: Vec<UntracedTest>,
}

pub fn load(path: &Path) -> Result<Manifest, Vec<Diag>> {
    let display = path.display().to_string();
    let source = fs::read_to_string(path)
        .map_err(|e| vec![Diag::file(&display, format!("cannot read manifest: {e}"))])?;
    let root = json::parse(&source)
        .map_err(|e| vec![Diag::file(&display, format!("malformed manifest: {e}"))])?;
    parse(&display, &root)
}

pub fn parse(path: &str, root: &Json) -> Result<Manifest, Vec<Diag>> {
    let mut out = Manifest::default();
    let mut errors = Vec::new();

    if root.get("realizes").is_none() && root.get("covers").is_none() {
        errors.push(Diag::expecting(
            path,
            0,
            "manifest declares neither realizes nor covers",
            "at least one of `realizes` or `covers`",
        ));
    }

    for (key, is_test) in [("realizes", false), ("covers", true)] {
        let Some(value) = root.get(key) else { continue };
        let Some(items) = value.as_array() else {
            errors.push(Diag::expecting(path, 0, format!("`{key}` is not an array"), "an array"));
            continue;
        };
        for (index, item) in items.iter().enumerate() {
            match site(path, key, index, item, is_test) {
                Ok(s) => {
                    if is_test {
                        out.covers.push(s)
                    } else {
                        out.realizes.push(s)
                    }
                }
                Err(mut d) => errors.append(&mut d),
            }
        }
    }

    if let Some(value) = root.get("untraced_tests") {
        if let Some(items) = value.as_array() {
            for (index, item) in items.iter().enumerate() {
                let where_ = format!("untraced_tests[{index}]");
                let site = string_field(path, &where_, item, "site", &mut errors);
                let file = string_field(path, &where_, item, "file", &mut errors);
                let lang = string_field(path, &where_, item, "lang", &mut errors);
                out.untraced.push(UntracedTest {
                    site: site.unwrap_or_default(),
                    file: file.unwrap_or_default(),
                    lang: lang.unwrap_or_default(),
                });
            }
        }
    }

    if errors.is_empty() {
        Ok(out)
    } else {
        Err(errors)
    }
}

fn site(
    path: &str,
    key: &str,
    index: usize,
    item: &Json,
    is_test: bool,
) -> Result<Site, Vec<Diag>> {
    let where_ = format!("{key}[{index}]");
    let mut errors = Vec::new();

    // The alpha keyed on (spec, req, scenario). Say so plainly rather than ignoring the field:
    // silently dropping it would leave a stale emitter producing tags that look fine and are not.
    if item.get("req").is_some() {
        errors.push(Diag::expecting(
            path,
            0,
            format!("{where_} carries `req`; the manifest key is now the pair (spec, scenario)"),
            "an emitter updated for the pair key — see D2.2",
        ));
    }

    let spec = string_field(path, &where_, item, "spec", &mut errors);
    let scenario = string_field(path, &where_, item, "scenario", &mut errors);
    let site_name = string_field(path, &where_, item, "site", &mut errors);
    let file = string_field(path, &where_, item, "file", &mut errors);
    let lang = string_field(path, &where_, item, "lang", &mut errors);

    let mut scope = None;
    let mut quantification = None;
    let mut oracle = None;

    if is_test {
        // The tag declares what the test *actually* is. What it must be lives in the verification
        // plan; `wrong-form` is the comparison (D5).
        if let Some(v) = item.get("scope").and_then(|v| v.as_str()) {
            match Scope::parse(v) {
                Some(s) => scope = Some(s),
                None => errors.push(Diag::expecting(
                    path,
                    0,
                    format!("{where_} has unknown scope `{v}`"),
                    "unit, component or e2e",
                )),
            }
        }
        if let Some(v) = item.get("quantification").and_then(|v| v.as_str()) {
            match Quantification::parse(v) {
                Some(q) => quantification = Some(q),
                None => errors.push(Diag::expecting(
                    path,
                    0,
                    format!("{where_} has unknown quantification `{v}`"),
                    "example or invariant",
                )),
            }
        }
        oracle = item.get("oracle").and_then(|v| v.as_str()).map(|s| s.to_string());
    } else if item.get("scope").is_some() || item.get("quantification").is_some() {
        errors.push(Diag::at(
            path,
            0,
            format!("{where_} carries a form; form is how a test checks, not a property of code"),
        ));
    }

    if !errors.is_empty() {
        return Err(errors);
    }
    Ok(Site {
        spec: spec.unwrap_or_default(),
        scenario: scenario.unwrap_or_default(),
        site: site_name.unwrap_or_default(),
        file: file.unwrap_or_default(),
        lang: lang.unwrap_or_default(),
        scope,
        quantification,
        oracle,
    })
}

fn string_field(
    path: &str,
    where_: &str,
    item: &Json,
    key: &str,
    errors: &mut Vec<Diag>,
) -> Option<String> {
    match item.get(key).and_then(|v| v.as_str()) {
        Some(s) => Some(s.to_string()),
        None => {
            errors.push(Diag::expecting(
                path,
                0,
                format!("{where_} is missing `{key}`"),
                format!("a string `{key}`"),
            ));
            None
        }
    }
}
