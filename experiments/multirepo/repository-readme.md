# Azimuth repository context

This checkout is one repository in the `rides` project. Its repository identity, project catalog,
owned areas and model sources are discoverable from `project-reference.json`:

```text
./azimuth/bin/azimuth project locate --reference azimuth/project-reference.json
```

The reference is a locator, not a second project authority. The catalog it resolves owns topology;
the catalog's model-source entries own current intent. Integration supplies revision-bound
repository manifests, execution receipts and the complete workset. A local result is partial and
cannot be finalized. Catalog and workset locators are resolved relative to the reference file, not
the shell's current directory.

Repository observation enumerates the tracked active and archived changes in this checkout.
Complete assembly rejects a change id owned by another repository as well. A work-package checkout
therefore contributes implementation and evidence to the coordinator-owned change instead of
creating a second proposal.

The operations checkout also provides `./azimuth/bin/check-monitoring`, which runs its rule tests
through the pinned Prometheus container; a host `promtool` installation is not assumed.
