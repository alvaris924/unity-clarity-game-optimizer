# ADR-0003: Scans never write; fixes dry-run, back up and quarantine

- Status: accepted
- Date: 2026-09-17

## Context

Half of what this package offers is a batch fix per finding: change importer settings on hundreds of textures, pad a sprite's source file, move an unreferenced asset out. Those actions rewrite art and metadata across a whole project, and the trust a user places in the numbers is lost the first time a fix damages a build. The in-project tool this package grew out of learned several of these lessons the hard way: a backup written under `Resources/` shipped in the build, a lowered Max Size destroyed a loading screen, and a scanner flagged its own backups as fresh offenders.

## Decision

Scanning is read-only, always. A fix is a separate object with `DryRun()` that returns the exact list of files it would change and `Apply()` that performs them. Any fix that rewrites a source file first copies the original beside it with a recognizable suffix, never under `Resources/` or `StreamingAssets/`, and scanners skip those copies. Importer changes are `.meta` edits and are revertible with git; by default the apply row refuses to run on a dirty working tree and says so. Deletions are quarantines: the asset moves to a folder outside `Assets/` with a manifest, and restore is one action. Every apply writes a receipt with the report before, the report after and the diff. A fix that could destroy content, such as shrinking a texture until its size is divisible by four, is not offered at all.

## Consequences

- A user can preview every fix and revert every fix, with or without version control.
- Fixes cost more to write: two code paths, a backup policy and a receipt each.
- Receipts double as the before/after material for diagrams and for the README.
- The git-clean gate needs git on the path; without it the gate reports unknown and asks for confirmation.

## Alternatives considered

- Apply on click with Undo support: `Undo` does not cover importer changes and file rewrites reliably, and it does not survive a domain reload. Rejected.
- Delete unreferenced assets outright: the reachability analysis has known blind spots (reflection, string paths, code-driven loads), so a quarantine with an easy restore is the only honest option.
