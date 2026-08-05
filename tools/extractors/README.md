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
                 "quantification": "example|invariant",
                 "oracle": "direct|golden|metamorphic|model-based|contract" }],
  "untraced_tests": [{ "site": "", "file": "", "lang": "" }]
}
```

`realizes` carries no form: form is how a test checks, not a property of code. The core rejects a
`realizes` that carries one, and rejects any entry carrying `req`, rather than ignoring it — a
stale emitter must not be able to produce tags that look fine and are not.

## dotnet

Reflects over built assemblies. Running after a build rather than scanning source is deliberate:
reflection resolves inheritance and generics that a text scan gets wrong, and a tag the compiler
rejected is not a tag. Source paths come from the portable PDB, best-effort — an assembly without
one emits no paths and says so, rather than inventing them.

```
azimuth-emit-dotnet --output m.json --root . --traced-root My.Tests path/to/Assembly.dll
```

## typescript

Static scan over the compiler API. The front end is functions, not classes, so the tags are typed
no-op function calls rather than decorators; the emitter resolves each call's enclosing named
symbol as the site, which makes a `covers` inside `test('…')` name the test.

```
azimuth-emit-ts --output m.json --root . src
```

## Traced areas

`untraced_tests` — a test that declares no claim and is not exempt — is reported only inside an
opt-in area: a namespace prefix in .NET, a file already carrying at least one `covers` in
TypeScript. Holding every test in a repo to it would be noise, and partial adoption is what makes
the ratchet work (D8).

## Fixtures

Each extractor has a synthetic fixture beside it. They are synthetic by decision (D2): the moment
an extractor's tests assert against the real demo app, the two are welded together and neither can
move independently.
