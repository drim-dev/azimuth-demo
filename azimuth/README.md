# Azimuth artifacts

This directory contains the complete repository-owned Azimuth account. Physical colocation makes
one domain area readable without collapsing the logical separation between intent, mechanism,
evidence and judgment (D32).

```text
azimuth/
├── model/<spec-id>/
│   ├── spec.md            # required
│   ├── design.md          # required for critical intent; otherwise optional
│   ├── verification.md    # deviations, non-test evidence and residuals only
│   └── judgments.md       # agent-tier verdicts when required or performed
├── standards/
│   └── verification.md
├── changes/
│   ├── <active-change>/
│   └── archive/
└── formats/
    ├── spec.md
    ├── design.md
    └── verification.md
```

The leaf directory is a **model package**, not a four-file template. `spec.md` is its anchor.
Sibling files are discovered only by the exact names above and remain absent when the corresponding
facet has nothing non-derivable to say. In particular, routine intent normally creates only
`spec.md`, and a standard claim following the project evidence standard needs no empty
`verification.md`.

Every file declares the spec id it belongs to. The id is authoritative; the package path is a
navigation convention. A mismatch or a facet outside its spec's package produces a warning rather
than changing identity. Moving a package therefore changes source locations without changing tags
or expiring judgments when the inspected content is identical.

The four sibling files do not imply four one-to-one records. Requirements, scenarios, mechanisms,
implementation sites and evidence retain their existing declared relations. A reusable control
gets a concern-oriented package such as `security/authentication` or
`resilience/dependency-failures`; its application to business domains must be established by
bindings and evidence rather than inferred from directory proximity.

Format contracts live in `formats/`. Proposed states and immutable history live in `changes/`.
Neither is scanned as the accepted current model.
