# azimuth

The core. Reads claims and linkage tags, derives a model, runs checks over it, and exports the
model for everything else to consume.

No dependencies (D17). `cargo build` needs nothing but a toolchain.

## Use

```
azimuth check                       # all checks, specs/ by default
azimuth check rtm --only 'trip/**'  # one check, scoped by id
azimuth export --out model.json
```

Exit codes: `0` clean, `1` errors found, `2` the model could not be derived.

Selection operates on **ids**, not paths (`--only 'trip/**'`), so it keeps working if the tree is
reorganized.

## What it does now

- **`spec.rs`** parses the format in `specs/README.md`. Strict: an unrecognized construct fails the
  parse with file, line and what was expected. A missing *declaration* is different — a
  requirement without `Criticality:` parses and becomes an `unclassified` hole (D6.2 vs D11).
- **`manifest.rs`** reads linkage manifests, keyed on the pair `(spec, scenario)` (D2.2). The
  alpha's triple is rejected with an explanation rather than silently accepted, so a stale emitter
  cannot produce tags that look fine and are not.
- **`check.rs`** runs `rtm`. Every hole kind it reports is a missing-facet combination (D3):
  `unrealized`, `uncovered`, `dangling-tag`, `dangling-realization`, `untraced-test`, plus
  `unclassified`. Severity comes from criticality, not from the check (D9.2).
- **`model.rs`** holds the derived model and writes the export (D10).

## What it does not do yet

- **No verification plan parsing, so `wrong-form` is never reported.** The format exists in
  `verification/`, and until it is read, required scope and quantification are unknown and the
  most interesting hole kind cannot fire. This is the next piece.
- **No design parsing**, so the enforcement-claimed-versus-found check (D3's highest-value
  cross-facet check) does not exist.
- **No emitters.** Nothing produces manifests yet, so every claim is unrealized and uncovered by
  construction. The .NET and TypeScript emitters are ported in slice 1 (D16.2).
- **One domain.** Claims are `(domain, predicate)` (D13), but the steel thread exercises only the
  behavioural domain, which scenarios take implicitly. `domain` becomes a field when a second one
  arrives — not a second artifact type.

## Tests

`cargo test`. Fixtures are synthetic by decision (D2): the moment this suite asserts against real
demo specs, the tool and the fixture are welded together and neither can move independently.
