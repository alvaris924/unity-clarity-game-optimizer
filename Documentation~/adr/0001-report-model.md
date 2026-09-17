# ADR-0001: One report model for every area

- Status: accepted
- Date: 2026-09-17

## Context

The package covers seven areas that read very different things: assembly definitions, package manifests, build reports, texture importers, Profiler memory sizes, render pipeline assets, compiled types. The in-project tool this package grew out of had one bespoke table per tab, so every exporter, every summary and every cross-link would have had to be written seven times.

## Decision

Every analyzer produces the same `Report`: scope and environment (Unity version, build target, package version, time), `metrics` with a unit and a budget, `findings` with a severity, a budget, a measured and a projected value, a verdict, an optional fix id and evidence paths, a `graph` of nodes and edges with weights and evidence, and `notes` for caveats the reader must see. Budgets are `Download`, `RuntimeRam`, `FrameTime`, `IterationTime` or `None`, and a report never mixes the two size budgets in one table. The model lives in `ClarityGameOptimizer.Core` with no engine references, so exporters and the window consume one shape and the CLI can diff two reports.

## Consequences

- Exporters to JSON, Markdown, DOT, Mermaid and archify IR are written once.
- Cross-links between areas are a lookup by asset path or assembly name in another report, not bespoke code.
- The `report.json` shape is public API for agents and CI and follows semantic versioning with the package.
- Analyzers must express themselves in the shared vocabulary; a check that needs a new field extends the model for everyone.

## Alternatives considered

- One model per area: fastest to port from the in-project tabs, rejected because every consumer multiplies by seven.
- Reusing Unity's `BuildReport` or Profiler types as the model: ties the core to the engine and to the version-specific shape of those types. Rejected; they are inputs, not the model.
