# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Report model: one `Report` shape for every area (`ClarityGameOptimizer.Core`): metrics, findings with a severity, a budget, measured and projected values, a verdict, a fix id and evidence paths, a graph of nodes and edges, and notes. A validator enforces the invariants, and two exporters write `report.json` (format `clarity-game-optimizer/report`, version 1, documented in `Documentation~/report-format.md`) and `report.md`, which keeps download size and runtime RAM in separate tables.
- Package scaffold: `package.json`, the seven assembly definitions, `IdentifierSlug` in `ClarityGameOptimizer.Core` with EditMode tests, the `DevProject~` development project, the CI test matrix, the tag-driven release workflow, the README and six decision records.
- Repository workflow: contribution guide, `AGENTS.md` rules for AI assistants, local git hooks, pull request and issue templates, Conventional Commit title check, Dependabot for GitHub Actions.

[Unreleased]: https://github.com/alvaris924/unity-clarity-game-optimizer/commits/main
