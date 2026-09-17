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
| `Editor/UI` | The window: the view model, the visual tree, the actions behind its buttons, and the USS. |
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

## The window

`Window > Clarity Game Optimizer` is one window for every area. The rail on the left lists the seven areas with the finding count of each one scanned this session; the six that cannot scan yet are dimmed and say which release they are planned for. The toolbar holds the scope of the selected area (Architecture has one, the project), Scan, the Export menu and Render. Under it, summary cards show the area's headline metrics, and the findings list shows every finding most severe first, then the largest, with severity colours; selecting a row shows its full title, verdict, measurement with budget and evidence, and double-clicking it shows the first evidence in the Project window when it is an asset. The status bar says what the last action did and where archify was found.

| Action | What it does |
|---|---|
| Scan | Runs the area's analyzer and shows the report. Results live in the window for the session only; a recompile starts it empty, because a recompile can invalidate what a scan read. |
| Export > Write report and diagram files | Writes `report.json`, `report.md` and the three diagram files into `ClarityGameOptimizerReports/<area>/` beside `Assets`. |
| Export > Open report folder | Opens that folder. |
| Export > Save ... as | Saves one report or diagram file wherever you choose. |
| Export > Locate archify | Points the package at an archify checkout and remembers it for this user. |
| Render | Scans if nothing was scanned, writes the files, validates and renders the diagram with archify and opens the HTML in the browser. Without archify, the files are still written and the status bar says what to run. |

## Reports

Every scan produces one `Report` in the shape [report-format.md](report-format.md) documents: metrics for the whole scope, findings with a verdict and evidence, a graph, and notes. It is written as `report.json` for agents and CI and as `report.md` for people; the diagram exports are derived from the same object.

## Architecture

The first area. Scan it from the window. The scan reads every script assembly the Editor compiles, so an assembly excluded from the Editor platform does not appear and the active build target's defines apply.

| Piece | What it holds |
|---|---|
| Node | One per assembly, named after it. Kind `Runtime`, `Editor` (compiled for the Editor only), `Test` (the `.asmdef` carries the `UNITY_INCLUDE_TESTS` constraint, the legacy `TestAssemblies` option or an explicit Test Runner reference) or `External` (anything under `Packages/` or one of the third-party folders in the project settings, which wins over the other three). Group: Runtime, Editor, Tests, Plugins or Packages. Weight is how much the project leans on the assembly (fan-in counted from the project's own assemblies only, since packages referencing each other would drown everything else out) plus a point per fifty scripts, so curation keeps both hubs and heavyweights. Evidence points at the `.asmdef`. |
| Edge | One per reference, labelled `references`, deduplicated, never to itself, never to an assembly outside the scan (a note counts those). |
| Metrics | Assemblies, project, plugin and package assemblies, project scripts, scripts outside any assembly definition and their share, references, highest fan-in. |
| `assembly.outside-definition` | The predefined `Assembly-CSharp` family: scripts no `.asmdef` claims. Warning at half the project's scripts or more, Advice at a tenth, Info below that. |
| `assembly.hub` | Info. A project assembly referenced by at least the hub threshold of the project's own assemblies (five by default); a change there recompiles all of them. |
| `assembly.large` | Advice. A project assembly with at least the large-assembly threshold of scripts (250 by default). |

Plugin and package assemblies are drawn but never judged: they are not the project's to split. Which folders hold plugins and where the two thresholds sit are project settings (below). The name rules behind the Data, Service and Messaging kinds arrive with later settings.

## Diagrams

The Architecture scan writes three diagram files next to the report, and renders one of them:

| File | What it is |
|---|---|
| `assemblies.architecture.json` | The curated graph as an archify `architecture` diagram, schema version 1, validated by archify's `standard` profile. Components are typed through a fixed legend (Runtime code, Editor tools, Data and content, Backend services, Events and messaging, Tests, Packages and plugins), tagged with their group, and placed one per row in column order so no connection ever runs through another node. A folded connection is dashed, and its line grows with the references it stands for; labels would collide at this density. One guided view per group. A card lists the findings above Info. |
| `assemblies.dot` | The whole graph for Graphviz: clusters per group, a fill colour per kind. |
| `assemblies.mmd` | The whole graph as a Mermaid flowchart, which GitHub renders inline. |
| `assemblies.html` | archify's self-contained interactive render of the JSON, written only when archify ran; Render opens it. |

archify is an external tool, never a dependency (ADR-0002, ADR-0005). The package looks for it in the folder picked with the window's `Export > Locate archify` (a per-user preference), then `ARCHIFY_HOME`, then the folders the agent skill installers use under the user profile; Node.js must be on the path. It runs `validate` and then `deliver` through Node without a shell and reports the outcome in the console; without archify it logs the command to run by hand.

Two limits of archify shape the export. Its `showcase` profile forbids crossings, which a dependency graph with hubs cannot avoid, so generated diagrams target `standard`. Its source capsules (`sources`) are verified against git and need a public GitHub repository at a pinned revision in `meta.repository`; the exporter writes them only when the caller supplies one, so a private project gets a diagram without them while `report.json` keeps every evidence path.

The `Diagrams` workflow fetches archify at the commit pinned in `Tests/Fixtures/archify/archify-version.json`, checks that the mirrored schemas under `Tests/Fixtures/archify/schemas` still match it, and validates every fixture under `Tests/Fixtures/archify/architecture`. Those fixtures are the exporter's own output for a ten-assembly test project; `ArchifyArchitectureExporterTests` fails when they drift, and `Unity -batchmode -projectPath DevProject~ -executeMethod ClarityGameOptimizer.Tests.Core.DiagramFixtures.Update -quit` regenerates them after an intentional change.

## Settings

Project Settings > Clarity Game Optimizer holds what a team shares, in `ProjectSettings/ClarityGameOptimizer.asset`, written as text so it diffs; commit it with the project. Per-user choices, such as where archify lives, are `EditorPrefs` and never leave the machine.

| Setting | Default | Effect |
|---|---|---|
| Third-party folders | `Assets/Plugins` | One project-relative folder per line. An assembly whose `.asmdef` sits under one of them is a plugin: drawn in the Plugins group, never judged, never counted as the project's own code or as a referrer that weighs. A folder matches itself and its contents, not a sibling that shares its prefix; case and slash direction do not matter. Add the folders of store assets that ship with source, such as `Assets/Feel`, to keep them out of the findings. |
| Hub fan-in | 5 | The `assembly.hub` threshold. |
| Large assembly, scripts | 250 | The `assembly.large` threshold. |

Every field saves on change; Reset to defaults restores the three.

## From a report to a diagram

A report's graph holds every node; a diagram cannot. Two pure steps in Core sit between them, and the diagram exporters call both:

| Step | What it does |
|---|---|
| `GraphCuration` | Keeps the twelve heaviest nodes (weight is the area's metric), with the nodes the diagram is about ranked first when the caller says which those are and a few slots held back for the heaviest others, folds every other node into one `Other <group> (n)` node per group with the majority kind and the summed weight, remaps the edges and merges the ones that now coincide into a counted edge such as `12 references`, and offers one view per group, five at most. The full graph is untouched and still goes out as DOT and Mermaid. |
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
