# Exploration: Independent Azimuth public identity

Id: independent-public-identity
Created: 2026-08-15
Status: complete

## Objective

Give the first public Azimuth alpha an identity independent of Drim without renaming the framework.
The identity must distinguish the release from other software named Azimuth and provide controlled
namespaces for its website, npm packages, source repository and container images.

## Boundaries

- Keep `Azimuth` as the product and `azimuth` as the CLI name.
- Change distribution identity, not framework semantics or the frozen alpha feature set.
- Keep framework development in `azimuth-demo`; the public alpha repository remains a frozen
  distribution.
- Do not use a Drim-owned npm scope, repository owner, container namespace or website as the
  permanent Azimuth identity.
- Do not treat registry or domain ownership as trademark clearance.

## Existing context

- The archived `canonical-alpha-release` exploration selected `drim-dev/azimuth`, the unavailable
  `@azimuth` npm scope and GHCR images under `drim-dev` in CAR1 and CAR8.
- The repository now declares `drim-dev/azimuth` to be a frozen alpha and `azimuth-demo` to be the
  development repository. The earlier conclusion that the distribution repository becomes
  development authority therefore no longer describes current repository policy.
- ServiceNow has used Azimuth since 2021 for an open-source text-classification dataset and model
  analysis application. Its repository is not archived, its latest release is v2.8.0 from 2023,
  and its last source push was in 2024. The application and this framework address different jobs,
  but both appear in developer-tool and software-analysis searches. See
  <https://github.com/ServiceNow/azimuth> and
  <https://servicenow.github.io/azimuth/v2.7/>.
- npm assigns an organization a matching package scope and describes scopes as a way to group
  official packages and avoid disputes over popular names. See
  <https://docs.npmjs.com/about-organization-scopes-and-packages/>.
- The user created the `azimuth-sh` npm organization, created the `azimuth-sh` GitHub organization
  and acquired `azimuth.sh` on 2026-08-15.
- Read-only GitHub inspection on 2026-08-15 established that the user administers both the public
  `drim-dev/azimuth` source and the active `azimuth-sh` target organization, while
  `azimuth-sh/azimuth` does not exist.
- The local npm CLI identifies as `mitro52` but receives HTTP 403 when reading membership of the
  new organization. The browser creation receipt establishes the organization; it does not
  establish that the current CLI credential can publish to it.

## Findings

### F1 — The domain makes a modified npm scope coherent

`@azimuth-sh` reads as an intentional owner when `azimuth.sh` is the canonical product site. It
avoids a dependency on the unavailable `@azimuth` scope without presenting Drim as Azimuth's
owner. The `.sh` suffix suggests developer and command-line tooling, although it may initially
suggest a shell-only implementation.

### F2 — The existing name collision is a persistent cost, not a release blocker

ServiceNow's prior use will mix some search results and can require a clarifying descriptor. The
products do not perform the same job, and the exact domain plus a stable `software assurance
framework` descriptor provides a consistent disambiguator. This finding does not predict the
outcome of a trademark search.

### F3 — Repository ownership participates in release identity

Leaving releases and GHCR images under `drim-dev` after establishing `azimuth.sh` and
`@azimuth-sh` would retain the domain dependency that the new identity is intended to remove.
Transferring the existing repository preserves history while allowing the first public tag,
provenance and images to name the independent owner.

### F4 — Identity changes invalidate the previous final rehearsal

npm package names, repository metadata and image coordinates are retained release inputs. Changing
them changes packed artifacts, checksums or provenance subjects, so publication must use a new
no-write rehearsal from the final revision rather than the earlier retained candidates.

## Decisions

### IPI1 — Retain the product name

Keep `Azimuth` as the product name and use `software assurance framework` as its stable public
descriptor. This accepts the ServiceNow search collision rather than replacing a meaningful name
before the first alpha.

### IPI2 — Use the domain-led public identity

Use `https://azimuth.sh` as the canonical product URL and `azimuth-sh` as the independent owner
where the bare name is unavailable. The npm artifacts are `@azimuth-sh/annotations` and
`@azimuth-sh/emit`; the CLI remains `azimuth`, and the .NET artifacts retain their `Azimuth.*`
identities.

### IPI3 — Transfer the frozen distribution repository

Transfer `drim-dev/azimuth` to `azimuth-sh/azimuth` before the first public tag. Preserve its
history rather than creating a new repository. GitHub releases and GHCR images use the
`azimuth-sh` owner. Framework development remains in `azimuth-demo`.

IPI3 supersedes CAR1 only for current development authority and repository ownership: the frozen
distribution no longer becomes the development authority. IPI2 and IPI3 supersede the npm and
GHCR owner identities in CAR8. The remaining canonical-alpha-release decisions are unchanged.

### IPI4 — Requalify before publication

Revise the active publication contract for the new identities, run every release gate, and retain
a new no-write rehearsal before creating `v0.1.0-alpha.1`. Establish organization-authorized npm
publication credentials before the write-enabled run. Retrieve every public target after
publication and record the resulting evidence.

## Open questions

None block the identity transition. Trademark clearance remains separate professional work and is
not inferred from this exploration.

## Rejected alternatives and residual risks

- **Rename before alpha.** Rejected because the user values Azimuth and accepts a manageable
  collision. Reopen if legal clearance finds a conflict or users repeatedly confuse the products.
- **Use `Azimuth Assurance` as the product name.** Rejected because a stable descriptor provides
  disambiguation without making the formal product and CLI names diverge.
- **Keep publication under `drim-dev`.** Rejected because repository, provenance and GHCR ownership
  would continue to associate Azimuth with the educational product.
- **Create a new GitHub repository.** Rejected because it would split history without improving the
  selected identity over a transfer.
- **Treat the current npm login as publication evidence.** Rejected because the organization read
  returned HTTP 403. A refreshed login or bounded CI credential must establish access.
- **Trademark certainty.** The name remains exposed to an unmeasured legal risk until a qualified
  search is performed.

## Result

Proceed in dependency order:

1. transfer the frozen distribution repository to `azimuth-sh/azimuth`;
2. revise its active public-alpha-publication change for the selected npm, repository, website and
   GHCR identities;
3. establish bounded registry credentials and produce a fresh retained rehearsal;
4. publish and retrieve `v0.1.0-alpha.1`; and
5. record the public evidence before accepting and archiving the publication change.
