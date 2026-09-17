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
| `Editor/Core` | Pure C# core: the report model and its validator (`Model/`), the JSON writer and the exporters (`Export/`), the Architecture logic (`Architecture/`), graph layout and curation (`Layout/`). The diagram exporters land here too. No engine references. |
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

## Reports

Every scan produces one `Report` in the shape [report-format.md](report-format.md) documents: metrics for the whole scope, findings with a verdict and evidence, a graph, and notes. It is written as `report.json` for agents and CI and as `report.md` for people; the diagram exports are derived from the same object.

## Architecture

The first area. `Tools > Clarity Game Optimizer > Write Architecture Report` scans the project and writes the report; the window will take over scanning and exporting when it exists. The scan reads every script assembly the Editor compiles, so an assembly excluded from the Editor platform does not appear and the active build target's defines apply.

| Piece | What it holds |
|---|---|
| Node | One per assembly, named after it. Kind `Runtime`, `Editor` (compiled for the Editor only), `Test` (the `.asmdef` carries the `UNITY_INCLUDE_TESTS` constraint, the legacy `TestAssemblies` option or an explicit Test Runner reference) or `External` (anything under `Packages/` or `Assets/Plugins/`, which wins over the other three). Group: Runtime, Editor, Tests, Plugins or Packages. Weight is fan-in. Evidence points at the `.asmdef`. |
| Edge | One per reference, labelled `references`, deduplicated, never to itself, never to an assembly outside the scan (a note counts those). |
| Metrics | Assemblies, project, plugin and package assemblies, project scripts, scripts outside any assembly definition and their share, references, highest fan-in. |
| `assembly.outside-definition` | The predefined `Assembly-CSharp` family: scripts no `.asmdef` claims. Warning at half the project's scripts or more, Advice at a tenth, Info below that. |
| `assembly.hub` | Info. A project assembly referenced by five or more others; a change there recompiles all of them. |
| `assembly.large` | Advice. A project assembly with 250 scripts or more. |

Plugin and package assemblies are drawn but never judged: they are not the project's to split. The thresholds are constants for now and move to the project settings with the settings page, together with the list of third-party folders beyond `Assets/Plugins` and the name rules behind the Data, Service and Messaging kinds.

## From a report to a diagram

A report's graph holds every node; a diagram cannot. Two pure steps in Core sit between them, and the diagram exporters call both:

| Step | What it does |
|---|---|
| `GraphCuration` | Keeps the twelve heaviest nodes (weight is the area's metric: fan-in, bytes, references), folds every other node into one `Other <group> (n)` node per group with the majority kind and the summed weight, remaps the edges and merges the ones that now coincide into a counted edge such as `12 references`, and offers one view per group, five at most. The full graph is untouched and still goes out as DOT and Mermaid. |
| `LayeredLayout` | Places a graph on a grid so dependencies read left to right: a node's column is the longest path leading to it from a node nothing depends on, so the foundations everything rests on end up in the rightmost columns. Within a column, nodes of the same group sit together, heaviest first, ties by label then id. An edge that would close a cycle is left out of the layering, deterministically, and counted; a dependency graph Unity accepts has none. |

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
