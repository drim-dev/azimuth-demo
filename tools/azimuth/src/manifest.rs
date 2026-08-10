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
use crate::model::{
    Artifact, ClassMember, Enumeration, MechanismCover, MechanismImplementation, Oracle,
    Quantification, Scope, Site,
};
use std::fs;
use std::path::Path;

#[derive(Debug, Default)]
pub struct Manifest {
    pub realizes: Vec<Site>,
    pub covers: Vec<Site>,
    pub mechanism_implementations: Vec<MechanismImplementation>,
    pub mechanism_covers: Vec<MechanismCover>,
    pub class_members: Vec<ClassMember>,
    pub enumerations: Vec<Enumeration>,
    pub artifacts: Vec<Artifact>,
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

    if [
        "realizes",
        "covers",
        "mechanism_implementations",
        "mechanism_covers",
    ]
    .iter()
    .all(|key| root.get(key).is_none())
    {
        errors.push(Diag::expecting(
            path,
            0,
            "manifest declares no linkage relations",
            "at least one claim or mechanism linkage array",
        ));
    }

    for (key, is_test) in [("realizes", false), ("covers", true)] {
        let Some(value) = root.get(key) else { continue };
        let Some(items) = value.as_array() else {
            errors.push(Diag::expecting(
                path,
                0,
                format!("`{key}` is not an array"),
                "an array",
            ));
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

    if let Some(value) = root.get("mechanism_implementations") {
        let Some(items) = value.as_array() else {
            errors.push(Diag::expecting(
                path,
                0,
                "`mechanism_implementations` is not an array",
                "an array",
            ));
            return Err(errors);
        };
        for (index, item) in items.iter().enumerate() {
            let where_ = format!("mechanism_implementations[{index}]");
            let spec = string_field(path, &where_, item, "spec", &mut errors);
            let mechanism = string_field(path, &where_, item, "mechanism", &mut errors);
            let binding = string_field(path, &where_, item, "binding", &mut errors);
            let file = string_field(path, &where_, item, "file", &mut errors);
            let lang = string_field(path, &where_, item, "lang", &mut errors);
            let source_fingerprint =
                optional_string_field(path, &where_, item, "source_fingerprint", &mut errors)
                    .unwrap_or_default();
            out.mechanism_implementations.push(MechanismImplementation {
                spec: spec.unwrap_or_default(),
                mechanism: mechanism.unwrap_or_default(),
                binding: binding.unwrap_or_default(),
                file: file.unwrap_or_default(),
                lang: lang.unwrap_or_default(),
                source_fingerprint,
            });
        }
    }

    if let Some(value) = root.get("mechanism_covers") {
        let Some(items) = value.as_array() else {
            errors.push(Diag::expecting(
                path,
                0,
                "`mechanism_covers` is not an array",
                "an array",
            ));
            return Err(errors);
        };
        for (index, item) in items.iter().enumerate() {
            let where_ = format!("mechanism_covers[{index}]");
            let spec = string_field(path, &where_, item, "spec", &mut errors);
            let mechanism = string_field(path, &where_, item, "mechanism", &mut errors);
            let site = string_field(path, &where_, item, "site", &mut errors);
            let file = string_field(path, &where_, item, "file", &mut errors);
            let lang = string_field(path, &where_, item, "lang", &mut errors);
            let source_fingerprint =
                optional_string_field(path, &where_, item, "source_fingerprint", &mut errors)
                    .unwrap_or_default();
            let scope = enum_field(
                path,
                &where_,
                item,
                "scope",
                Scope::parse,
                "unit, component or e2e",
                &mut errors,
            );
            let quantification = enum_field(
                path,
                &where_,
                item,
                "quantification",
                Quantification::parse,
                "example or universal",
                &mut errors,
            );
            let oracle = optional_enum_field(
                path,
                &where_,
                item,
                "oracle",
                Oracle::parse,
                "direct, golden, relational, metamorphic, model-based or contract",
                &mut errors,
            );
            out.mechanism_covers.push(MechanismCover {
                spec: spec.unwrap_or_default(),
                mechanism: mechanism.unwrap_or_default(),
                site: site.unwrap_or_default(),
                file: file.unwrap_or_default(),
                lang: lang.unwrap_or_default(),
                source_fingerprint,
                scope,
                quantification,
                oracle,
            });
        }
    }

    if let Some(value) = root.get("class_members") {
        let Some(items) = value.as_array() else {
            errors.push(Diag::expecting(
                path,
                0,
                "`class_members` is not an array",
                "an array",
            ));
            return Err(errors);
        };
        for (index, item) in items.iter().enumerate() {
            let where_ = format!("class_members[{index}]");
            let class = string_field(path, &where_, item, "class", &mut errors);
            let site = string_field(path, &where_, item, "site", &mut errors);
            let file = string_field(path, &where_, item, "file", &mut errors);
            let lang = string_field(path, &where_, item, "lang", &mut errors);
            out.class_members.push(ClassMember {
                class: class.unwrap_or_default(),
                site: site.unwrap_or_default(),
                file: file.unwrap_or_default(),
                lang: lang.unwrap_or_default(),
            });
        }
    }

    if let Some(value) = root.get("enumerations") {
        let Some(items) = value.as_array() else {
            errors.push(Diag::expecting(
                path,
                0,
                "`enumerations` is not an array",
                "an array",
            ));
            return Err(errors);
        };
        for (index, item) in items.iter().enumerate() {
            let where_ = format!("enumerations[{index}]");
            let class = string_field(path, &where_, item, "class", &mut errors);
            let kind = string_field(path, &where_, item, "kind", &mut errors);
            let source = string_field(path, &where_, item, "source", &mut errors);
            let source_fingerprint =
                string_field(path, &where_, item, "source_fingerprint", &mut errors);
            out.enumerations.push(Enumeration {
                class: class.unwrap_or_default(),
                kind: kind.unwrap_or_default(),
                source: source.unwrap_or_default(),
                source_fingerprint: source_fingerprint.unwrap_or_default(),
            });
        }
    }

    if let Some(value) = root.get("artifacts") {
        let Some(items) = value.as_array() else {
            errors.push(Diag::expecting(
                path,
                0,
                "`artifacts` is not an array",
                "an array",
            ));
            return Err(errors);
        };
        for (index, item) in items.iter().enumerate() {
            let where_ = format!("artifacts[{index}]");
            let id = string_field(path, &where_, item, "id", &mut errors);
            let kind = string_field(path, &where_, item, "kind", &mut errors);
            let file = string_field(path, &where_, item, "file", &mut errors);
            let unique = match item.get("unique") {
                Some(value) => match value.as_bool() {
                    Some(value) => Some(value),
                    None => {
                        errors.push(Diag::at(
                            path,
                            0,
                            format!("{where_}.unique is not a boolean"),
                        ));
                        None
                    }
                },
                None => None,
            };
            let mut columns = Vec::new();
            if let Some(value) = item.get("columns") {
                match value.as_array() {
                    Some(values) => {
                        for column in values {
                            match column.as_str() {
                                Some(column) => columns.push(column.to_string()),
                                None => errors.push(Diag::at(
                                    path,
                                    0,
                                    format!("{where_}.columns contains a non-string"),
                                )),
                            }
                        }
                    }
                    None => errors.push(Diag::at(
                        path,
                        0,
                        format!("{where_}.columns is not an array"),
                    )),
                }
            }
            let predicate = item
                .get("predicate")
                .and_then(Json::as_str)
                .map(str::to_string);
            out.artifacts.push(Artifact {
                id: id.unwrap_or_default(),
                kind: kind.unwrap_or_default(),
                file: file.unwrap_or_default(),
                unique,
                columns,
                predicate,
            });
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
    let source_fingerprint = match item.get("source_fingerprint") {
        Some(value) => match value.as_str() {
            Some(value) => value.to_string(),
            None => {
                errors.push(Diag::at(
                    path,
                    0,
                    format!("{where_}.source_fingerprint is not a string"),
                ));
                String::new()
            }
        },
        None => String::new(),
    };
    let evidence_kind = optional_string_field(path, &where_, item, "evidence_kind", &mut errors);
    let evidence_outcome =
        optional_string_field(path, &where_, item, "evidence_outcome", &mut errors);
    let observed_at = optional_string_field(path, &where_, item, "observed_at", &mut errors);
    let expires_at = match item.get("expires_at") {
        Some(value) => match value.as_num() {
            Some(value) if value >= 0.0 && value.fract() == 0.0 && value <= u64::MAX as f64 => {
                Some(value as u64)
            }
            _ => {
                errors.push(Diag::expecting(
                    path,
                    0,
                    format!("{where_}.expires_at is not a Unix-second integer"),
                    "a non-negative integer",
                ));
                None
            }
        },
        None => None,
    };

    if let Some(kind) = &evidence_kind {
        if !is_test {
            errors.push(Diag::at(
                path,
                0,
                format!("{where_} imports evidence under `realizes`; use `covers`"),
            ));
        }
        if kind != "manual-test" {
            errors.push(Diag::expecting(
                path,
                0,
                format!("{where_} has unknown evidence kind `{kind}`"),
                "manual-test",
            ));
        }
        if !matches!(evidence_outcome.as_deref(), Some("passed" | "failed")) {
            errors.push(Diag::expecting(
                path,
                0,
                format!("{where_} has no usable evidence outcome"),
                "`evidence_outcome: passed` or `failed`",
            ));
        }
        if observed_at.is_none() {
            errors.push(Diag::expecting(
                path,
                0,
                format!("{where_} has no observation instant"),
                "an ISO-8601 `observed_at` string",
            ));
        }
        if expires_at.is_none() {
            errors.push(Diag::expecting(
                path,
                0,
                format!("{where_} has no expiry"),
                "`expires_at` as Unix seconds",
            ));
        }
        if source_fingerprint.is_empty() {
            errors.push(Diag::expecting(
                path,
                0,
                format!("{where_} has no immutable result fingerprint"),
                "`source_fingerprint` from the imported result payload",
            ));
        }
    } else if evidence_outcome.is_some() || observed_at.is_some() || expires_at.is_some() {
        errors.push(Diag::at(
            path,
            0,
            format!("{where_} carries receipt fields without `evidence_kind`"),
        ));
    }

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
                    "example or universal",
                )),
            }
        }
        oracle = optional_enum_field(
            path,
            &where_,
            item,
            "oracle",
            Oracle::parse,
            "direct, golden, relational, metamorphic, model-based or contract",
            &mut errors,
        );
    } else if item.get("scope").is_some()
        || item.get("quantification").is_some()
        || item.get("oracle").is_some()
    {
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
        source_fingerprint,
        evidence_kind,
        evidence_outcome,
        observed_at,
        expires_at,
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

fn optional_string_field(
    path: &str,
    where_: &str,
    item: &Json,
    key: &str,
    errors: &mut Vec<Diag>,
) -> Option<String> {
    match item.get(key) {
        Some(value) => match value.as_str() {
            Some(value) => Some(value.to_string()),
            None => {
                errors.push(Diag::expecting(
                    path,
                    0,
                    format!("{where_}.{key} is not a string"),
                    format!("a string `{key}`"),
                ));
                None
            }
        },
        None => None,
    }
}

fn enum_field<T>(
    path: &str,
    where_: &str,
    item: &Json,
    key: &str,
    parse: impl Fn(&str) -> Option<T>,
    expected: &str,
    errors: &mut Vec<Diag>,
) -> Option<T> {
    let Some(value) = item.get(key).and_then(Json::as_str) else {
        errors.push(Diag::expecting(
            path,
            0,
            format!("{where_} is missing `{key}`"),
            expected,
        ));
        return None;
    };
    match parse(value) {
        Some(value) => Some(value),
        None => {
            errors.push(Diag::expecting(
                path,
                0,
                format!("{where_} has unknown {key} `{value}`"),
                expected,
            ));
            None
        }
    }
}

fn optional_enum_field<T>(
    path: &str,
    where_: &str,
    item: &Json,
    key: &str,
    parse: impl Fn(&str) -> Option<T>,
    expected: &str,
    errors: &mut Vec<Diag>,
) -> Option<T> {
    let value = optional_string_field(path, where_, item, key, errors)?;
    match parse(&value) {
        Some(value) => Some(value),
        None => {
            errors.push(Diag::expecting(
                path,
                0,
                format!("{where_} has unknown {key} `{value}`"),
                expected,
            ));
            None
        }
    }
}
