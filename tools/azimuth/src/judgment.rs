//! The agent tier's output.
//!
//! D14 said agent judgments are evidence items. Implementing it showed that reading is wrong: a
//! judgment that a test is toothy is not independent evidence *of* a claim — treat it as such and a
//! claim with no tests but a judgment becomes covered, which is nonsense.
//!
//! A judgment is evidence **about** evidence. It qualifies what the tags already assert, and its
//! value is negative: it can take a claim that looks covered and report it as a hole. That is the
//! seam the machine tier cannot reach — the machine makes structure checkable, it does not make
//! truth checkable, and a tag is only as honest as whoever wrote it.
//!
//! Freshness is a fingerprint over everything the judgment looked at. It over-invalidates rather
//! than under-invalidates: a false stale costs a re-judgement, a false fresh is a lie carried on the
//! books.

use crate::diag::{validate_id, Diag};
use crate::labels::read_block;
use std::fs;
use std::path::{Path, PathBuf};

const LABELS: &[&str] = &["Verdict", "Fingerprint", "Judged", "Judge"];

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Verdict {
    /// The evidence discriminates, and the tags describe it honestly.
    Sound,
    /// Tests exist and pass, but would also pass against an implementation that is wrong.
    Toothless,
    /// A tag declares a stronger form than the test actually has.
    DishonestTag,
    /// The claim is satisfied, but the spec does not say something it should.
    SpecGap,
}

impl Verdict {
    pub fn parse(s: &str) -> Option<Self> {
        match s {
            "sound" => Some(Verdict::Sound),
            "toothless" => Some(Verdict::Toothless),
            "dishonest-tag" => Some(Verdict::DishonestTag),
            "spec-gap" => Some(Verdict::SpecGap),
            _ => None,
        }
    }

    pub fn name(self) -> &'static str {
        match self {
            Verdict::Sound => "sound",
            Verdict::Toothless => "toothless",
            Verdict::DishonestTag => "dishonest-tag",
            Verdict::SpecGap => "spec-gap",
        }
    }
}

#[derive(Debug, Clone)]
pub struct Judgment {
    pub scenario: String,
    pub verdict: Verdict,
    pub fingerprint: String,
    pub judged: String,
    pub judge: String,
    pub reason: String,
    pub line: usize,
}

#[derive(Debug, Clone)]
pub struct Judgments {
    pub spec: String,
    pub path: String,
    pub entries: Vec<Judgment>,
}

impl Judgments {
    pub fn entry(&self, scenario: &str) -> Option<&Judgment> {
        self.entries.iter().find(|j| j.scenario == scenario)
    }
}

pub fn load(root: &Path) -> Result<Vec<Judgments>, Vec<Diag>> {
    if !root.exists() {
        return Ok(Vec::new());
    }
    let mut files = Vec::new();
    collect(root, &mut files).map_err(|e| {
        vec![Diag::file(&root.display().to_string(), format!("cannot read judgments: {e}"))]
    })?;
    files.sort();

    let mut all: Vec<Judgments> = Vec::new();
    let mut errors = Vec::new();
    for path in files {
        let display = path.display().to_string();
        let source = match fs::read_to_string(&path) {
            Ok(s) => s,
            Err(e) => {
                errors.push(Diag::file(&display, format!("cannot read: {e}")));
                continue;
            }
        };
        match parse(&display, &source) {
            Ok(j) => all.push(j),
            Err(mut d) => errors.append(&mut d),
        }
    }

    if errors.is_empty() {
        Ok(all)
    } else {
        Err(errors)
    }
}

fn collect(dir: &Path, out: &mut Vec<PathBuf>) -> std::io::Result<()> {
    for entry in fs::read_dir(dir)? {
        let entry = entry?;
        let path = entry.path();
        if path.is_dir() {
            collect(&path, out)?;
        } else if path.extension().and_then(|e| e.to_str()) == Some("md")
            && path.file_name().and_then(|n| n.to_str()) != Some("README.md")
        {
            out.push(path);
        }
    }
    Ok(())
}

pub fn parse(path: &str, source: &str) -> Result<Judgments, Vec<Diag>> {
    let lines: Vec<&str> = source.lines().collect();
    let mut errors = Vec::new();
    let mut spec: Option<String> = None;
    let mut entries: Vec<Judgment> = Vec::new();
    let mut i = 0;

    while i < lines.len() {
        let trimmed = lines[i].trim();
        let ln = i + 1;

        if let Some(rest) = trimmed.strip_prefix("# Judgments:") {
            let id = rest.trim();
            if let Err(why) = validate_id(id, true) {
                errors.push(Diag::at(path, ln, format!("invalid spec id: {why}")));
            } else {
                spec = Some(id.to_string());
            }
            i += 1;
            continue;
        }

        if let Some(rest) = trimmed.strip_prefix("## Claim:") {
            let id = rest.trim().to_string();
            let (block, next) = read_block(&lines, i + 1, LABELS);
            i = next;

            for (text, sl) in &block.stray {
                errors.push(Diag::expecting(
                    path,
                    *sl,
                    format!("unrecognized line `{text}`"),
                    format!("one of: {}", LABELS.join(", ")),
                ));
            }

            let Some(verdict) = block.value("Verdict").and_then(Verdict::parse) else {
                errors.push(Diag::expecting(
                    path,
                    ln,
                    format!("`{id}` has no usable verdict"),
                    "sound, toothless, dishonest-tag or spec-gap",
                ));
                continue;
            };
            let Some(fingerprint) = block.value("Fingerprint") else {
                errors.push(Diag::expecting(
                    path,
                    ln,
                    format!("`{id}` records no fingerprint"),
                    "`Fingerprint:` — without it the judgment can never go stale",
                ));
                continue;
            };
            if block.prose.is_empty() {
                errors.push(Diag::expecting(
                    path,
                    ln,
                    format!("`{id}` gives no reason"),
                    "prose saying what was examined and why the verdict",
                ));
            }

            entries.push(Judgment {
                scenario: id,
                verdict,
                fingerprint: fingerprint.to_string(),
                judged: block.value("Judged").unwrap_or_default().to_string(),
                judge: block.value("Judge").unwrap_or("agent").to_string(),
                reason: block.prose,
                line: ln,
            });
            continue;
        }

        if trimmed.starts_with('#') {
            errors.push(Diag::expecting(
                path,
                ln,
                format!("unrecognized heading `{trimmed}`"),
                "`# Judgments: <spec-id>` or `## Claim: <scenario-id>`",
            ));
        }
        i += 1;
    }

    let Some(spec) = spec else {
        errors.push(Diag::expecting(path, 0, "no spec judged", "a `# Judgments: <spec-id>` heading"));
        return Err(errors);
    };

    if errors.is_empty() {
        Ok(Judgments { spec, path: path.to_string(), entries })
    } else {
        Err(errors)
    }
}

/// A fingerprint over everything a judgment looked at: the claim's own text, and the content of
/// every file carrying evidence for it.
///
/// File-level rather than site-level on purpose. It over-invalidates — an unrelated edit in a test
/// file expires the judgment — and that is the safe direction. A false stale costs one re-judgement;
/// a false fresh means a verdict about code that no longer exists is still being counted.
pub fn fingerprint(claim_text: &str, mut evidence_files: Vec<String>) -> String {
    evidence_files.sort();
    evidence_files.dedup();

    let mut hash: u64 = 0xcbf2_9ce4_8422_2325;
    let mut absorb = |bytes: &[u8]| {
        for &b in bytes {
            hash ^= b as u64;
            hash = hash.wrapping_mul(0x0000_0100_0000_01b3);
        }
    };

    absorb(claim_text.as_bytes());
    for file in &evidence_files {
        absorb(b"\x00");
        absorb(file.as_bytes());
        match fs::read(file) {
            Ok(content) => absorb(&content),
            // A file the checker cannot read is folded in as its own state: if it reappears, the
            // fingerprint changes and the judgment expires, which is the safe direction.
            Err(_) => absorb(b"<unreadable>"),
        }
    }

    format!("{hash:016x}")
}
