## Summary

<!-- What changes and why, in one short paragraph. Link the design section or ADR if one applies. -->

Closes #

## Type of change

- [ ] `feat` new capability
- [ ] `fix` bug fix
- [ ] `docs` documentation only
- [ ] `chore` / `ci` tooling, workflow, dependencies
- [ ] `refactor` / `perf` no behaviour change intended
- [ ] `test` tests only

## Test plan

<!-- How you verified it. Which Unity versions you ran the EditMode tests on. For analyzers: which project you scanned and what the numbers were checked against. -->

## Checklist

- [ ] PR title is a Conventional Commit: `type(scope): subject`
- [ ] EditMode tests added or updated for logic in `ClarityGameOptimizer.Core`
- [ ] Scans stay read-only; any new fix has a dry run, a backup path and a receipt
- [ ] Every new number names its budget (download, runtime RAM, frame time, iteration time)
- [ ] Diagram exports still pass `archify validate --quality showcase`, or the change does not touch exporters
- [ ] `CHANGELOG.md` updated under `[Unreleased]`, or the `skip-changelog` label applied
- [ ] Docs updated (`README.md`, `Documentation~/`) if behaviour or public API changed
- [ ] No new third-party dependencies; APIs newer than Unity 2022.3 are version-guarded
- [ ] Verified on Unity 2022.3 and 6000.x, or the reason it was not is noted above
