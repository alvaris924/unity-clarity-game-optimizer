# Clarity Game Optimizer documentation

Clarity Game Optimizer is an open-source, dependency-free project analyzer for the Unity Editor, shipped as the UPM package `com.alvaris.clarity-game-optimizer`. It measures a project along seven areas and turns every scan into a ranked findings table, a machine-readable report and a diagram.

## The seven areas

| Area | Question it answers | First ships in |
|---|---|---|
| Architecture | What is this project made of, and what depends on what? | v0.1.0 |
| Dependencies | What did we pull in, from where, and is it pinned? | v0.1.0 |
| Build Size | Where does the download weight sit, and which rows are free wins? | v0.1.0 (scan), v0.5.0 (fixes) |
| Memory | What does the device actually hold, and what is the biggest lever? | v0.5.0 |
| Assets | What is in the project that should not be, and who pulls each asset in? | v0.5.0 |
| Performance | Where does the frame, and the first eight seconds, go? | v1.0.0 |
| Code Quality | What will slow the team down or the frame? | v1.0.0 |

## Layout

| Path | Purpose |
|---|---|
| `Editor/Core` | Pure C# core: report model, graph model, layered layout, curation, exporters (JSON, Markdown, DOT, Mermaid, archify IR). No engine references. |
| `Editor/Analysis` | The analyzers per area, scopes, fixes with dry run and backup, build-report and importer readers. |
| `Editor/UI` | The Editor window, UI Toolkit elements and USS. |
| `Editor/Settings` | Project settings, user preferences, settings pages. |
| `Editor/Extensions` | The public extension API. The only assembly with a compatibility promise. |
| `Editor/Cli` | The headless entry point for CI (`-executeMethod`). |
| `Tests/Editor` | EditMode tests and fixtures. |
| `DevProject~` | Development Unity project. References the package with `file:../..`. Invisible to package consumers. |

## Decision records

- [ADR-0001: One report model for every area](adr/0001-report-model.md)
- [ADR-0002: archify renders the diagrams; the package owns layout and curation](adr/0002-archify-as-renderer.md)
- [ADR-0003: Scans never write; fixes dry-run, back up and quarantine](adr/0003-fix-safety.md)
- [ADR-0004: Unity 2022.3 LTS is the floor](adr/0004-unity-floor.md)
- [ADR-0005: Zero third-party dependencies](adr/0005-dependency-policy.md)
- [ADR-0006: GitHub flow with tags, no development branch](adr/0006-branching-model.md)

## Two budgets

Download size and runtime RAM are different budgets, and most intuitive fixes move only one of them. The table below is the rule the tool is built around; every number in a report names the budget it belongs to, and the two never share a table.

| Lever | Download size | Runtime RAM |
|---|---|---|
| Mesh Compression | yes | no effect |
| Crunch Compression | yes | no effect |
| Weld Vertices | small | small |
| Texture Max Size | yes | yes, scales with area |
| Texture format (ASTC vs ETC) | yes | yes, scales with bits per pixel |
| Read/Write Enabled off | no effect | removes the duplicate CPU copy |
| Vertex count / decimation | yes | yes |

## Working on the package

Open `DevProject~` in Unity 6000.2 or newer, or run the EditMode tests headlessly:

```
Unity -batchmode -nographics -projectPath DevProject~ -runTests -testPlatform EditMode -testResults results.xml
```

The contribution workflow is in [CONTRIBUTING.md](../CONTRIBUTING.md); rules for AI assistants are in [AGENTS.md](../AGENTS.md).
