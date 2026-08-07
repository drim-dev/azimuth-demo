# Extractors

One per ecosystem. Each finds the linkage tags in its own language and writes the same
language-neutral manifest; the core only ever reads manifests.

That seam is why adding a language is a day's work rather than a fork of the core — and it is what
lets the core stay dependency-free (D17) while this side does metadata and AST work in ecosystems
where that tooling is already present and idiomatic.

## Manifest

Keyed on the pair `(spec, scenario)`, not the alpha's triple (D2.2). Scenario ids are unique per
spec, so a requirement id would be redundant information that can go stale — and dropping it is
what makes splitting or merging a requirement free, since no tag moves.

```json
{
  "realizes": [{ "spec": "", "scenario": "", "site": "", "file": "", "lang": "" }],
  "covers":   [{ "spec": "", "scenario": "", "site": "", "file": "", "lang": "",
                 "scope": "unit|component|e2e",
                 "quantification": "example|universal",
                 "oracle": "direct|golden|metamorphic|model-based|contract" }],
  "enumerations": [{ "class": "", "kind": "", "source": "",
                     "source_fingerprint": "" }],
  "class_members": [{ "class": "", "site": "", "file": "", "lang": "" }],
  "artifacts": [{ "id": "", "kind": "", "file": "" }]
}
```

`realizes` carries no form: form is how a test checks, not a property of code. The core rejects a
`realizes` that carries one, and rejects any entry carrying `req`, rather than ignoring it — a
stale emitter must not be able to produce tags that look fine and are not.

An enumeration witness says where a class came from independently of linkage tags. Its members are
authoritative only when the source was read completely; a missing build output or unresolved member
fails emission. Artifacts are exact binding targets for current design entries.

## dotnet

Reflects over built assemblies. Running after a build rather than scanning source is deliberate:
reflection resolves inheritance and generics that a text scan gets wrong, and a tag the compiler
rejected is not a tag. Source paths come from the portable PDB, best-effort — an assembly without
one emits no paths and says so, rather than inventing them.

The .NET extractor also emits type/method symbols and executes compiled EF migration metadata to
derive database indexes, including uniqueness, ordered columns and predicates. It does not infer
that a method is an exclusive choke point.

```
azimuth-emit-dotnet --output m.json --root . path/to/Assembly.dll
```

## typescript

Static scan over the compiler API. The front end is functions, not classes, so the tags are typed
no-op function calls rather than decorators; the emitter resolves each call's enclosing named
symbol as the site, which makes a `covers` inside `test('…')` name the test.

```
azimuth-emit-ts --output m.json --root . src
```

`--next-app <class>=<dir>` derives route members from Next's built route manifest. The option fails
closed when the build manifest is absent or a route cannot be resolved to project source.

## Evidence opt-in

`covers` opts a test into the evidence model. An untagged test emits nothing: it may exercise
routine behavior, infrastructure or a project rule that no Azimuth claim names. `uncovered` is
derived in the other direction, from a standard or critical claim with no sufficient evidence.

## Tests

```
dotnet test tools/extractors/dotnet/Azimuth.Emit.Tests
(cd tools/extractors/typescript && npx tsc -p tsconfig.json && node --test dist/emitter.test.js)
```

Each extractor has a synthetic fixture beside it, synthetic by decision (D2): the moment an
extractor's tests assert against the real demo app, the two are welded together and neither can
move independently.

The tests assert on the *shape* of what is emitted rather than merely that something was, because
a silently wrong emitter produces a green matrix — the exact failure the framework exists to
prevent. The .NET suite includes a regression for the enum-boxing bug that first emitted
`"scope": "1"` instead of `"component"`.
