# Federation format examples

All six documents currently use schema version 1. Unknown versions fail; this phase has one
consumer and deliberately provides no compatibility migration.

## Repository-local project reference

```json
{
  "format": "azimuth-project-reference",
  "version": 1,
  "project": "rides",
  "repository": "experience",
  "catalog": "../../rides-assurance/azimuth/project.json"
}
```

The reference travels with each checkout and only locates authority; it does not duplicate the
catalog. An optional `workset` locator may name a locally available integration workset. Run
`azimuth project locate --reference azimuth/project-reference.json` to print the repository's exact
areas and model sources. Relative paths resolve from the reference file, not the process working
directory.

## Project catalog

```json
{
  "format": "azimuth-project",
  "version": 1,
  "project": "rides",
  "repositories": [
    { "id": "backend", "required": true },
    { "id": "experience", "required": true }
  ],
  "areas": [
    {
      "id": "payments",
      "repository": "backend",
      "mounts": [
        { "id": "code", "path": "app/services/Payments" },
        { "id": "tests", "path": "app/services/Payments.Tests" }
      ]
    }
  ],
  "model_sources": [
    {
      "id": "system-intent",
      "repository": "backend",
      "path": "azimuth/model",
      "required": true
    }
  ],
  "standards": {
    "repository": "backend",
    "path": "azimuth/standards/verification.md"
  },
  "required_receipts": [
    { "id": "system-e2e", "subjects": ["backend", "experience"] }
  ]
}
```

## Repository manifest

The `linkage` object may also carry D39 `observations`. Repository observation assigns the run a
typed area source from its first configuration input (or report when it has none), preserves all
claim bindings and includes its fingerprint in the source account. Evidence bindings are projected
into `covers` only when the complete model is loaded; challenge bindings remain judgment context.

```json
{
  "format": "azimuth-repository-manifest",
  "version": 1,
  "project": "rides",
  "repository": "backend",
  "revision": "0123456789abcdef",
  "producer": "azimuth-emit-dotnet/0.1.0",
  "areas": ["payments"],
  "model_sources": [
    { "id": "system-intent", "digest": "sha256..." }
  ],
  "standards_digest": "sha256...",
  "changes": [
    {
      "id": "critical-rider-refunds",
      "state": "active",
      "path": "azimuth/changes/critical-rider-refunds",
      "digest": "sha256..."
    }
  ],
  "linkage": {
    "realizes": [
      {
        "spec": "payments/capture",
        "scenario": "capture-equals-trip-fare",
        "site": "Handle",
        "file": "app/services/Payments/Features/Captures/CaptureTrip.cs",
        "lang": "csharp",
        "source_fingerprint": "sha256...",
        "area": "payments",
        "address_kind": "dotnet-symbol",
        "address": "Payments.Features.Captures.CaptureTrip.RequestHandler.Handle",
        "mount": "code"
      }
    ]
  }
}
```

The four source fields are atomic: supplying only some is an error. Assembly independently
rederives the most-specific mount from the tracked repository-relative file and rejects a producer
that claims an enclosing or escaping mount. Only area, address kind and address enter stable
identity. `changes` is also closed-world: observation derives every direct active change and dated
archive under `azimuth/changes`, and assembly compares the declaration with the tracked checkout.
The same change id in two repository observations is an authority conflict.

## Execution receipt

```json
{
  "format": "azimuth-execution-receipt",
  "version": 1,
  "id": "system-e2e",
  "project": "rides",
  "outcome": "passed",
  "subjects": [
    { "repository": "backend", "revision": "0123456789abcdef" },
    { "repository": "experience", "revision": "fedcba9876543210" }
  ]
}
```

## Workset

```json
{
  "format": "azimuth-workset",
  "version": 1,
  "project": "rides",
  "repositories": [
    {
      "id": "backend",
      "root": "../rides-backend",
      "revision": "0123456789abcdef",
      "manifest": "artifacts/backend.json",
      "manifest_digest": "sha256..."
    }
  ],
  "receipts": [
    { "path": "artifacts/system-e2e.json", "digest": "sha256..." }
  ]
}
```

Paths are resolved relative to the workset file. `azimuth project check --local backend` may use a
workset containing all repositories but selects only `backend`; its output remains explicitly
partial. Required model-source and standards owners cannot be made optional by omitting their
repositories. `azimuth project finalize` requires the complete clean workset and re-runs semantic
model validation at the snapshot boundary.

## Project snapshot

`azimuth project finalize` emits `azimuth-project-snapshot` version 1. It carries the catalog digest
and complete area/mount topology, the derived model fingerprint, every selected repository revision
and manifest digest, every required receipt digest, and the observed change authorities. Keeping
topology in the snapshot lets an archived account explain an old area placement after the current
catalog changes.

Project-aware acceptance uses:

```text
azimuth project accept-change \
  --project <project.json> \
  --before <accepted-active-workset.json> \
  --after <tested-archive-workset.json> \
  --change <change-id> \
  --date <YYYY-MM-DD> \
  --out <project-snapshot.json>
```

Both worksets must be complete, clean and independently receipt-bound. The command requires an
unchanged directory move from the singular active authority to the requested dated archive. Only
the authority repository may advance, and its tracked content outside that change directory must
remain identical. The emitted post-archive snapshot adds `accepted_change` with the archive digest
and the complete pre-archive revision tuple.
