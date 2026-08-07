//! The `azimuth` CLI.
//!
//! `azimuth` is the tool; `rtm` is one check among several (D9). Commands are `azimuth check`,
//! `azimuth check <id>` and `azimuth export`.

use azimuth::check;
use azimuth::diag::Diag;
use std::path::PathBuf;
use std::process::ExitCode;

const USAGE: &str = "\
azimuth — derives a model from claims and linkage tags, and flags its holes

USAGE
    azimuth check [<check-id>...] [options]
    azimuth export [options]
    azimuth judge [options]
    azimuth change check <dir> [options]
    azimuth change finalize <dir> [options]
    azimuth change archive <dir> --date <YYYY-MM-DD> [options]

The judge command lists every claim with the fingerprint a judgment must carry, so the
agent tier can record verdicts that expire when what they judged changes.

CHECKS
    rtm     claims against the code and evidence that reference them

OPTIONS
    --specs <dir>          spec root (default: specs)
    --verification <dir>   verification plans (default: verification)
    --design <dir>         design artifacts (default: design)
    --manifest <file>      a linkage manifest; repeatable
    --only <pattern>       restrict to spec ids; `billing/**` or an exact id; repeatable
    --out <file>           export destination (default: stdout)
    -h, --help
    -V, --version
";

const CHECKS: &[&str] = &["rtm"];

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();
    match run(&args) {
        Ok(code) => code,
        Err(message) => {
            eprintln!("azimuth: {message}");
            ExitCode::from(2)
        }
    }
}

struct Options {
    specs: PathBuf,
    verification: PathBuf,
    design: PathBuf,
    manifests: Vec<PathBuf>,
    only: Vec<String>,
    out: Option<PathBuf>,
    checks: Vec<String>,
}

fn run(args: &[String]) -> Result<ExitCode, String> {
    if args.is_empty() || args[0] == "-h" || args[0] == "--help" {
        print!("{USAGE}");
        return Ok(ExitCode::SUCCESS);
    }
    if args[0] == "-V" || args[0] == "--version" {
        println!("azimuth {}", env!("CARGO_PKG_VERSION"));
        return Ok(ExitCode::SUCCESS);
    }

    let command = args[0].clone();
    if command == "change" {
        return command_change(&args[1..]);
    }
    let options = parse_options(&args[1..])?;

    match command.as_str() {
        "check" => command_check(options),
        "export" => command_export(options),
        "judge" => command_judge(options),
        other => Err(format!("unknown command `{other}`\n\n{USAGE}")),
    }
}

fn command_change(args: &[String]) -> Result<ExitCode, String> {
    if args.len() < 2 {
        return Err(format!(
            "change needs an operation and directory\n\n{USAGE}"
        ));
    }
    let operation = &args[0];
    let root = PathBuf::from(&args[1]);
    let mut option_args = Vec::new();
    let mut date = None;
    let mut index = 2;
    while index < args.len() {
        if args[index] == "--date" {
            date = args.get(index + 1).cloned();
            index += 2;
        } else {
            option_args.push(args[index].clone());
            index += 1;
        }
    }
    let options = parse_options(&option_args)?;
    let loaded = match azimuth::load(
        &options.specs,
        &options.verification,
        &options.design,
        &options.manifests,
        &options.only,
    ) {
        Ok(loaded) => loaded,
        Err(diags) => {
            report(&diags, "error");
            return Ok(ExitCode::from(2));
        }
    };
    let report = match azimuth::change::inspect(&root, &loaded.model) {
        Ok(report) => report,
        Err(errors) => {
            for error in &errors {
                eprintln!("error: {error}");
            }
            return Ok(ExitCode::from(2));
        }
    };
    let holes = check::rtm(&loaded.model);

    match operation.as_str() {
        "check" => {
            println!("change `{}`", report.id);
            for addition in &report.additions {
                let state = if addition.applied {
                    "applied"
                } else {
                    "planned"
                };
                println!(
                    "  add {}#{} · {} · {} scenario(s) · {state} · {}",
                    addition.spec,
                    addition.requirement,
                    addition.criticality.name(),
                    addition.scenarios.len(),
                    change_obligations(addition.criticality)
                );
            }
            println!(
                "current {} claim(s) → target {} claim(s)",
                report.current_claims, report.target_claims
            );
            println!("{} incomplete plan item(s)", report.incomplete_plan_items);
            let summary = check::summarize(&loaded.model, &holes);
            println!(
                "accepted-state model: {} error(s), {} warning(s)",
                summary.errors, summary.warnings
            );
            Ok(if summary.errors > 0 {
                ExitCode::from(1)
            } else {
                ExitCode::SUCCESS
            })
        }
        "finalize" => {
            let issues = azimuth::change::completion_issues(&root, &report);
            let summary = check::summarize(&loaded.model, &holes);
            if summary.errors > 0 || summary.warnings > 0 {
                eprintln!(
                    "error: accepted-state model has {} error(s), {} warning(s)",
                    summary.errors, summary.warnings
                );
            }
            for issue in &issues {
                eprintln!("error: {issue}");
            }
            if summary.errors > 0 || summary.warnings > 0 || !issues.is_empty() {
                return Ok(ExitCode::from(1));
            }
            let (fingerprint, finalization) = azimuth::change::finalization(&loaded.model, &holes);
            std::fs::write(root.join("finalization.json"), finalization).map_err(|error| {
                format!("cannot write {}/finalization.json: {error}", root.display())
            })?;
            println!("finalized `{}` at model {fingerprint}", report.id);
            Ok(ExitCode::SUCCESS)
        }
        "archive" => {
            let Some(date) = date else {
                return Err("change archive needs `--date <YYYY-MM-DD>`".into());
            };
            if !valid_date(&date) {
                return Err(format!(
                    "invalid archive date `{date}`; expected YYYY-MM-DD"
                ));
            }
            let issues = azimuth::change::completion_issues(&root, &report);
            if !issues.is_empty() {
                for issue in &issues {
                    eprintln!("error: {issue}");
                }
                return Ok(ExitCode::from(1));
            }
            let finalization_path = root.join("finalization.json");
            let recorded = std::fs::read_to_string(&finalization_path).map_err(|_| {
                format!(
                    "{} is missing; run `azimuth change finalize` first",
                    finalization_path.display()
                )
            })?;
            let (_, expected) = azimuth::change::finalization(&loaded.model, &holes);
            if recorded != expected {
                return Ok({
                    eprintln!("error: finalization is stale; run `azimuth change finalize` again");
                    ExitCode::from(1)
                });
            }
            if !holes.is_empty() {
                eprintln!("error: accepted-state model has holes");
                return Ok(ExitCode::from(1));
            }
            if root
                .parent()
                .and_then(|parent| parent.file_name())
                .and_then(|name| name.to_str())
                != Some("changes")
            {
                return Err("archive source must be a direct child of `changes/`".into());
            }
            let archive_root = root.parent().unwrap().join("archive");
            std::fs::create_dir_all(&archive_root)
                .map_err(|error| format!("cannot create {}: {error}", archive_root.display()))?;
            let destination = archive_root.join(format!("{date}-{}", report.id));
            if destination.exists() {
                return Err(format!(
                    "archive destination {} already exists",
                    destination.display()
                ));
            }
            std::fs::rename(&root, &destination).map_err(|error| {
                format!(
                    "cannot archive {} as {}: {error}",
                    root.display(),
                    destination.display()
                )
            })?;
            println!("archived `{}` as {}", report.id, destination.display());
            Ok(ExitCode::SUCCESS)
        }
        other => Err(format!("unknown change operation `{other}`")),
    }
}

fn change_obligations(criticality: azimuth::model::Criticality) -> &'static str {
    match criticality {
        azimuth::model::Criticality::Routine => "intent only",
        azimuth::model::Criticality::Standard => "realization + evidence",
        azimuth::model::Criticality::Critical => {
            "realization + design + critical evidence + judgment"
        }
    }
}

fn valid_date(value: &str) -> bool {
    value.len() == 10
        && value.as_bytes()[4] == b'-'
        && value.as_bytes()[7] == b'-'
        && value
            .bytes()
            .enumerate()
            .all(|(index, byte)| index == 4 || index == 7 || byte.is_ascii_digit())
}

fn parse_options(args: &[String]) -> Result<Options, String> {
    let mut o = Options {
        specs: PathBuf::from("specs"),
        verification: PathBuf::from("verification"),
        design: PathBuf::from("design"),
        manifests: Vec::new(),
        only: Vec::new(),
        out: None,
        checks: Vec::new(),
    };
    let mut i = 0;
    while i < args.len() {
        let arg = &args[i];
        let value = |name: &str| -> Result<String, String> {
            args.get(i + 1)
                .cloned()
                .ok_or_else(|| format!("`{name}` needs a value"))
        };
        match arg.as_str() {
            "--design" => {
                o.design = PathBuf::from(value("--design")?);
                i += 2;
            }
            "--verification" => {
                o.verification = PathBuf::from(value("--verification")?);
                i += 2;
            }
            "--specs" => {
                o.specs = PathBuf::from(value("--specs")?);
                i += 2;
            }
            "--manifest" => {
                o.manifests.push(PathBuf::from(value("--manifest")?));
                i += 2;
            }
            "--only" => {
                o.only.push(value("--only")?);
                i += 2;
            }
            "--out" => {
                o.out = Some(PathBuf::from(value("--out")?));
                i += 2;
            }
            other if other.starts_with('-') => return Err(format!("unknown option `{other}`")),
            other => {
                if !CHECKS.contains(&other) {
                    return Err(format!(
                        "unknown check `{other}`\n  known checks: {}",
                        CHECKS.join(", ")
                    ));
                }
                o.checks.push(other.to_string());
                i += 1;
            }
        }
    }
    Ok(o)
}

fn report(diags: &[Diag], label: &str) {
    for d in diags {
        eprintln!("{label}: {d}");
    }
}

fn command_check(options: Options) -> Result<ExitCode, String> {
    let loaded = match azimuth::load(
        &options.specs,
        &options.verification,
        &options.design,
        &options.manifests,
        &options.only,
    ) {
        Ok(l) => l,
        Err(diags) => {
            report(&diags, "error");
            eprintln!("\n{} parse error(s); no model was derived", diags.len());
            return Ok(ExitCode::from(2));
        }
    };
    report(&loaded.warnings, "warning");

    let selected: Vec<&str> = if options.checks.is_empty() {
        CHECKS.to_vec()
    } else {
        options.checks.iter().map(|s| s.as_str()).collect()
    };

    let mut holes = Vec::new();
    for id in &selected {
        match *id {
            "rtm" => holes.extend(check::rtm(&loaded.model)),
            _ => unreachable!("checked during option parsing"),
        }
    }

    for hole in &holes {
        let where_ = if hole.line > 0 {
            format!("{}:{}", hole.path, hole.line)
        } else {
            hole.path.clone()
        };
        let claim = hole.claim.clone().unwrap_or_else(|| "-".into());
        let level = hole
            .criticality
            .map(|c| format!(" ({})", c.name()))
            .unwrap_or_default();
        println!(
            "{where_}: {} {} {claim}{level}\n    {}",
            hole.severity.name(),
            hole.kind.name(),
            hole.detail
        );
    }

    let summary = check::summarize(&loaded.model, &holes);
    let by_kind: Vec<String> = check::counts_by_kind(&holes)
        .into_iter()
        .map(|(k, n)| format!("{n} {k}"))
        .collect();

    println!();
    println!(
        "{} claims in {} spec(s) · checks: {}",
        summary.claims,
        loaded.model.specs.len(),
        selected.join(", ")
    );
    if by_kind.is_empty() {
        println!("no holes");
    } else {
        println!("{}", by_kind.join(" · "));
    }
    println!(
        "{} error(s), {} warning(s)",
        summary.errors, summary.warnings
    );

    Ok(if summary.errors > 0 {
        ExitCode::from(1)
    } else {
        ExitCode::SUCCESS
    })
}

/// Lists claims and their current fingerprints — the agent tier's worklist.
fn command_judge(options: Options) -> Result<ExitCode, String> {
    let loaded = match azimuth::load(
        &options.specs,
        &options.verification,
        &options.design,
        &options.manifests,
        &options.only,
    ) {
        Ok(l) => l,
        Err(diags) => {
            report(&diags, "error");
            return Ok(ExitCode::from(2));
        }
    };
    let model = &loaded.model;

    for claim in model.claims() {
        let files = model.judgment_files(&claim.spec.id, &claim.scenario.id);
        let fingerprint = azimuth::judgment::fingerprint(&model.claim_text(&claim), files.clone());
        let existing = model
            .judgments_for(&claim.spec.id)
            .and_then(|j| j.entry(&claim.scenario.id));
        let state = match existing {
            Some(j) if j.fingerprint == fingerprint => format!("judged {}", j.verdict.name()),
            Some(j) => format!("stale ({})", j.verdict.name()),
            None => "unjudged".to_string(),
        };
        println!(
            "{}\t{}\t{}\t{}\t{}",
            claim.spec.id,
            claim.scenario.id,
            claim
                .requirement
                .criticality
                .map(|c| c.name())
                .unwrap_or("-"),
            fingerprint,
            state
        );
        for file in files {
            println!("\tevidence\t{file}");
        }
    }
    Ok(ExitCode::SUCCESS)
}

fn command_export(options: Options) -> Result<ExitCode, String> {
    let loaded = match azimuth::load(
        &options.specs,
        &options.verification,
        &options.design,
        &options.manifests,
        &options.only,
    ) {
        Ok(l) => l,
        Err(diags) => {
            report(&diags, "error");
            return Ok(ExitCode::from(2));
        }
    };
    report(&loaded.warnings, "warning");

    let holes = check::rtm(&loaded.model);
    let json = loaded.model.to_json(&holes).to_string_pretty();

    match options.out {
        Some(path) => std::fs::write(&path, json)
            .map_err(|e| format!("cannot write {}: {e}", path.display()))?,
        None => print!("{json}"),
    }
    Ok(ExitCode::SUCCESS)
}
