# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Graph layout and curation in Core, the step between a report and a diagram: `LayeredLayout` places nodes on a grid so dependencies read left to right (column = longest path from a node nothing depends on; rows keep a group together, heaviest first; cycle-closing edges are dropped deterministically and counted), and `GraphCuration` keeps the heaviest twelve nodes, folds the rest into one "Other" node per group, merges the edges that follow with a count, and offers one view per group, five at most.
- Architecture: the first area. `Tools > Clarity Game Optimizer > Write Architecture Report` reads every script assembly from the compilation pipeline and writes `report.json` and `report.md` under `ClarityGameOptimizerReports/architecture/` beside `Assets`. One node per assembly typed as Runtime, Editor, Test (read from the `.asmdef`) or External (under `Packages/` or `Assets/Plugins/`) and grouped by that role, one edge per reference with fan-in as the weight, metrics for assemblies, scripts, references and the share of scripts outside any assembly definition, and findings for that share (graded by its size), for hubs referenced by five or more assemblies, and for assemblies of 250 scripts or more.
- Report model: one `Report` shape for every area (`ClarityGameOptimizer.Core`): metrics, findings with a severity, a budget, measured and projected values, a verdict, a fix id and evidence paths, a graph of nodes and edges, and notes. A validator enforces the invariants, and two exporters write `report.json` (format `clarity-game-optimizer/report`, version 1, documented in `Documentation~/report-format.md`) and `report.md`, which keeps download size and runtime RAM in separate tables.
- Package scaffold: `package.json`, the seven assembly definitions, `IdentifierSlug` in `ClarityGameOptimizer.Core` with EditMode tests, the `DevProject~` development project, the CI test matrix, the tag-driven release workflow, the README and six decision records.
- Repository workflow: contribution guide, `AGENTS.md` rules for AI assistants, local git hooks, pull request and issue templates, Conventional Commit title check, Dependabot for GitHub Actions.

[Unreleased]: https://github.com/alvaris924/unity-clarity-game-optimizer/commits/main
