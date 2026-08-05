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

CHECKS
    rtm     claims against the code and evidence that reference them

OPTIONS
    --specs <dir>       spec root (default: specs)
    --manifest <file>   a linkage manifest; repeatable
    --only <pattern>    restrict to spec ids; `trip/**` or an exact id; repeatable
    --out <file>        export destination (default: stdout)
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
    let options = parse_options(&args[1..])?;

    match command.as_str() {
        "check" => command_check(options),
        "export" => command_export(options),
        other => Err(format!("unknown command `{other}`\n\n{USAGE}")),
    }
}

fn parse_options(args: &[String]) -> Result<Options, String> {
    let mut o = Options {
        specs: PathBuf::from("specs"),
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
    let loaded = match azimuth::load(&options.specs, &options.manifests, &options.only) {
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
    println!("{} error(s), {} warning(s)", summary.errors, summary.warnings);

    Ok(if summary.errors > 0 { ExitCode::from(1) } else { ExitCode::SUCCESS })
}

fn command_export(options: Options) -> Result<ExitCode, String> {
    let loaded = match azimuth::load(&options.specs, &options.manifests, &options.only) {
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
