# Clarity Game Optimizer documentation

Clarity Game Optimizer is an open-source, dependency-free project analyzer for the Unity Editor, shipped as the UPM package `com.alvaris.clarity-game-optimizer`. It measures a project along seven areas and turns every scan into a ranked findings table, a machine-readable report and a diagram.

## The seven areas

| Area | Question it answers | First ships in |
|---|---|---|
| Architecture | What is this project made of, and what depends on what? | v0.1.0 |
| Dependencies | What did we pull in, from where, and is it pinned? | v0.1.0 |
| Build Size | Where does the download weight sit, and which rows are free wins? | v0.1.0 (scan), v0.5.0 (fixes) |
| Memory | What does the device actually hold, and what is the biggest lever? | v0.1.0 (the first checks), v0.5.0 (scene and prefab scopes, fixes) |
| Assets | What is in the project that should not be, and who pulls each asset in? | v0.5.0 |
| Performance | Where does the frame, and the first eight seconds, go? | v0.1.0 (settings with cost), v1.0.0 (play-session sampler, boot timeline) |
| Code Quality | What will slow the team down or the frame? | v0.1.0 (per-frame heuristics), v1.0.0 (iteration-time metrics, analyzers) |

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

`Window > Clarity Game Optimizer` is one window for every area. The rail on the left lists the seven areas with the finding count of each one scanned this session; the four that cannot scan yet are dimmed and say which release they are planned for. The toolbar holds the scope of the selected area (Architecture and Dependencies have one, the project; Build Size offers the last build or the whole project), Scan, the Export menu and Render. Under it, summary cards show the area's headline metrics, and the findings list shows every finding most severe first, then the largest, with severity colours; selecting a row shows its full title, verdict, measurement with budget and evidence, and double-clicking it shows the first evidence in the Project window when it is an asset. The status bar says what the last action did and where archify was found.

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

The files are named after what the nodes are: `assemblies.*` for Architecture, `packages.*` for Dependencies. Build Size has no graph yet, so it writes the two report files only.

archify is an external tool, never a dependency (ADR-0002, ADR-0005). The package looks for it in the folder picked with the window's `Export > Locate archify` (a per-user preference), then `ARCHIFY_HOME`, then the folders the agent skill installers use under the user profile; Node.js must be on the path. It runs `validate` and then `deliver` through Node without a shell and reports the outcome in the console; without archify it logs the command to run by hand.

Two limits of archify shape the export. Its `showcase` profile forbids crossings, which a dependency graph with hubs cannot avoid, so generated diagrams target `standard`. Its source capsules (`sources`) are verified against git and need a public GitHub repository at a pinned revision in `meta.repository`; the exporter writes them only when the caller supplies one, so a private project gets a diagram without them while `report.json` keeps every evidence path.

The `Diagrams` workflow fetches archify at the commit pinned in `Tests/Fixtures/archify/archify-version.json`, checks that the mirrored schemas under `Tests/Fixtures/archify/schemas` still match it, and validates every fixture under `Tests/Fixtures/archify/architecture`. Those fixtures are the exporter's own output for a ten-assembly test project; `ArchifyArchitectureExporterTests` fails when they drift, and `Unity -batchmode -projectPath DevProject~ -executeMethod ClarityGameOptimizer.Tests.Core.DiagramFixtures.Update -quit` regenerates them after an intentional change.

## Build Size

The third area, and the first whose every byte is download size. Two scopes: **Last build** reads the report of the last player build made on this machine (`Library/LastBuild.buildreport`) and, when that build recorded per-asset sizes, the textures and models it packed; **Project** reads every texture and model under `Assets`, shipped or not.

| Piece | What it holds |
|---|---|
| Build metrics | The shipped size (the package at the output path, or the output folder without its do-not-ship folders), the build's own total (which counts intermediates such as IL2CPP sources), native libraries across ABIs, managed assemblies (shipped as is with Mono, the input of IL2CPP otherwise), serialized asset data, what sits under `Resources` folders, StreamingAssets, and the packed asset data when the build recorded it. |
| `build.top-asset`, `build.top-folder` | Info. The packed assets and the first two folder levels ranked by what they cost in the build, 25 and 10 rows. Needs a build made with `BuildOptions.DetailedBuildReport`; Unity 6 records per-asset sizes only then. |
| `build.report-missing`, `build.assets-not-detailed` | Advice. No build on this machine, or a build without per-asset sizes, with what to build to get them. The report file's write time dates the build; the summary's own end time is not dependable. |
| `texture.stored-uncompressed` | A texture whose importer asked for compression but which Unity stored raw (RGBA32, RGB24, ARGB32 or BGRA32) because its dimensions defeated the block compressor: not a multiple of 4, or, for the older formats, not a power of two. The importer and the build report both name the format Unity wanted; only the imported object shows the fallback. The verdict names the fix that applies: turning mipmaps off when the texture is not a power of two and has mipmaps (such a texture stays raw whatever the format override, so that comes first), an ASTC platform override when the target compresses to ASTC (Android, iOS), which takes any size and so leaves tiled and nine-sliced sprites without padding seams, a platform format override when the dimensions are already legal for the newer formats, padding the source by up to 3 pixels when the source is what imported, raising Max Size when its proportional downscale produced the illegal size (it does not round to a multiple of 4), or a re-export when lifting the clamp would ship more. Measured is what the build attributed to the texture, or the raw estimate; projected applies the fix's ratio. Warning when the saving is a megabyte or more, Advice below. |
| `mesh.uncompressed` | Advice. A model of 256 KB or more with mesh compression off and no skinning. The verdict names Medium and the worst-case position error at the mesh's largest extent; the ratios were measured on one modular pack and are projections. |
| `build.stripping-low`, `build.mono-backend` | Advice. Managed stripping at Disabled, Minimal or Low with IL2CPP, or the Mono backend on a mobile platform, read from the player settings of the built platform. |
| `build.native-library-outlier`, `build.unexpected-file` | Warning. A shared library present under some ABI folders but not all (a plugin missing an ABI crashes those devices, a library that belongs nowhere is dead weight), or a file under an ABI folder that is not a shared library, such as a desktop binary a plugin shipped along. |
| `build.debug-symbols` | Advice. Symbol files inside what ships. The unstripped copies Unity keeps under a `symbols` folder beside the Gradle project, and the symbols archive beside a package, are not inside it. |
| `build.resources-heavy` | Advice. `Resources` folders hold a quarter or more of the asset payload; everything under them ships whether referenced or not. |

Stored formats and mesh settings are read as imported for the Editor's active build target, which the notes name; when the last build was for another platform, the notes say so. Reading thousands of textures means loading each one: a project of 12,000 textures and 4,000 models scans in under a minute, with a progress bar. Textures whose importer says Uncompressed are described without loading; they can never be a fallback.

## Memory

Runtime RAM, every byte. One scope, **Build scenes and Resources**: the enabled build scenes, everything under a `Resources` folder, and all they reference (scripts and Editor folders left out). Addressables and asset bundles are not followed. For each texture, mesh, audio clip and TextMesh Pro font asset in that set the scan records what the Editor's Profiler reports for the object as imported for the active build target, with the import settings that change it.

| Piece | What it holds |
|---|---|
| Metrics | Texture, mesh, audio and font atlas memory and their total; the counts, and the vertices of the referenced meshes. |
| `texture.read-write` | A texture with Read/Write on keeps a CPU copy as large as the GPU one. Warning from a megabyte, Advice below; projected is half. |
| `texture.npot-mipmaps-raw` | Warning. A texture that is not a power of two and has mipmaps, stored raw: such a texture stays uncompressed whatever the format override, ETC2 and ASTC alike, until the mipmaps go (or it becomes a power of two). For a sprite the verdict is to turn mipmaps off. |
| `texture.sprite-mipmaps` | Advice. A sprite with mipmaps that does compress: the chain adds a third, and UI and 2D sprites draw near their own size. |
| `texture.top-runtime` | Info. The 15 largest textures with size, stored format, Max Size and whether the active target has a platform override. |
| `mesh.read-write` | Advice. A referenced mesh of 2,000 vertices or more whose model has Read/Write on: the vertices are kept on the CPU too. |
| `mesh.top-vertices` | Info. The 10 meshes with the most vertices. Meshes are counted one by one, the ones something in the set references, never a whole model file: a model can carry dozens of meshes nothing uses, and summing the file overstates it (a character model of 182,000 vertices that shipped 2,100). A model under `Resources`, or one whose referrers are not text-serialized, counts all of its meshes. |
| `audio.decompress-on-load` | Warning. A clip of 10 s or more set to Decompress On Load holds the whole clip as 16-bit PCM. The verdict is Streaming from 30 s (music), Compressed In Memory below. |
| `font.duplicate-bake` | Advice. Two font assets baked from the same font file with the same point size, padding, atlas size and population mode: the second atlas is a copy. A static asset with a dynamic one of the same font as its fallback is the intended pair and is not reported. The verdict names the asset to keep and the material-preset route (assign the font first, then the preset). |
| `font.readable-static-atlas` | Advice. A static atlas never adds glyphs, so Read/Write on it is never used. |

The Profiler's numbers in the Editor rank well but are not a device measurement: the Editor keeps extra copies of some textures and of Read/Write data. Fonts are read through their serialized fields, so the package needs no TextMesh Pro reference.

## Performance

Settings with a cost, read from the project and the scenes open in the Editor. Mobile targets (Android, iOS) get the mobile thresholds; each row says what the setting costs and what to try, since some costs buy a look the game wants.

| Check | When it fires |
|---|---|
| `render.scale-native` | Advice, mobile. Universal Render Pipeline render scale 1 or more: the world renders at the phone's full resolution. Overlay cameras share the scale, so the verdict says to check UI text. |
| `render.shadow-distance`, `render.soft-shadows` | Advice, mobile. Main light shadows beyond 30 m, or soft shadows on. |
| `render.additional-lights-per-pixel` | Advice, mobile. Additional lights per pixel while an open scene keeps more than one realtime directional light. |
| `render.hdr-mobile`, `render.msaa-mobile` | Advice, mobile. HDR on, or MSAA above 2x. |
| `camera.post-processing-twice` | Warning. An overlay camera with post-processing on: the pipeline applies it to everything rendered before it, so the stack runs it twice. |
| `camera.depth-texture-forced` | Info. A camera that forces the depth texture on; keep it only if a shader reads the scene depth (soft particles, depth fade, water). |
| `light.realtime-directionals` | Advice. An open scene with more than one active, non-baked directional light. |
| `player.frame-pacing-off` | Advice, Android. Optimized Frame Pacing off. |
| `player.graphics-api-order` | Info. A manual graphics API list with OpenGL ES before Vulkan; the verdict mentions the Vulkan deny list for GPUs with driver trouble. |
| `player.log-stack-traces` | Advice. Log or Warning stack traces other than None: every `Debug.Log` in a player walks the stack and builds a string. |
| `time.maximum-allowed-timestep` | Advice. Above 0.1 s; the title says how many physics steps one late frame may run. |
| `physics.auto-sync-transforms` | Advice. On. |
| `quality.async-upload-persistent` | Advice, mobile. The starting quality level keeps the async upload buffer all session (runtime RAM). |

Pipeline and camera fields are read as serialized properties, so the package needs no Universal Render Pipeline reference; with another pipeline those rows stay silent.

## Code Quality

Heuristics over the project's own scripts: every C# file under `Assets` except Editor folders, the third-party folders from Project Settings, and the `Demo`, `Example` and `Sample` folders of asset packs. No compiler: comments and literal contents are blanked first, with line numbers kept, then the Unity messages that run every frame (`Update`, `LateUpdate`, `FixedUpdate`, `OnGUI`, the `Stay` collision and trigger messages, `OnWillRenderObject`, `OnRenderObject`) are found by their headers and their bodies by brace matching. A method they call is not followed.

| Check | What it means |
|---|---|
| `code.per-frame.get-component`, `.find`, `.linq`, `.allocation`, `.instantiate`, `.physics-allocation`, `.send-message`, `.resources-load` | Warning. Component and scene lookups, LINQ (only in files that use `System.Linq`), new collections and arrays, Instantiate and Destroy, the allocating physics queries, SendMessage, `Resources.Load`, inside a per-frame message. |
| `code.per-frame.string`, `.log`, `.tag-compare` | Advice. Interpolation, concatenation, `ToString` and `string.Format`; `Debug.Log` and `LogWarning`; tags compared as strings. |
| `code.per-frame.camera-main` | Info. Cheap since Unity 2020.2, which caches it. |
| `code.empty-unity-message` | Advice. An empty `Update`, `LateUpdate`, `FixedUpdate` or `OnGUI`: Unity still calls it on every enabled instance. |
| `code.physics-query-no-mask` | Advice. `OverlapSphere`, `OverlapBox` or `OverlapCapsule` (and their NonAlloc forms) called anywhere without a layer mask: it collects every collider in range. |

One row per pattern per method, measured as the number of occurrences, with up to eight lines as evidence. A cost in a script on many objects (enemies, bullets, pickups) is paid once per instance; treat the rows as places to look.

## Dependencies

The second area. The scan reads every package the Package Manager resolved, the plugins in the third-party folders, the assembly names more than one file claims, and the scripting defines of the active build target.

| Piece | What it holds |
|---|---|
| Package node | One per resolved package, labelled with its display name, kind `External`, grouped by source: Registry, Git, Local, Embedded, Tarball or Built-in. The sublabel is the version (for git, the pinned commit or the tag or branch followed and the commit it resolved to) and how many of the project's own assembly definitions reference it. Weight: referrers plus dependant packages plus one when the manifest names it. Evidence: the package's `package.json`, and the manifest for a direct dependency. |
| Plugin node | One per plugin: a folder named `Plugins` among the third-party folders holds one plugin per subfolder, any other third-party folder is one plugin. Sublabel: assembly definitions, libraries, scripts, and how many scripts no definition claims. |
| Edge | One per package dependency, labelled `depends on`. |
| Metrics | Packages in total, named in the manifest, by source, git packages not pinned to a commit, bytes on disk, plugins, their libraries and scripts, the scripts outside any definition, name collisions, scripting defines. |
| `package.git-floating` | A git package that follows a tag (Advice, tags rarely move) or a branch or the default branch (Warning, every fresh checkout can differ). The verdict names the commit to pin. |
| `package.local` | Info. A package resolved from a `file:` path or a tarball; other machines need it in the same place. |
| `package.unreferenced` | Info. A registry or git package the manifest names that no assembly definition of the project references. The predefined assemblies reference every package automatically, so scripts outside assembly definitions may still use it; this is a fact, never a verdict that the package is unused. |
| `plugin.outside-definition` | Advice. A plugin whose scripts compile into the predefined assemblies with the project's own code, so a change anywhere recompiles them and a change in them recompiles everything. |
| `assembly.name-collision` | Warning. An assembly name that an `.asmdef` and a `.dll`, or two `.dll` files, both claim; Unity warns of hard-to-diagnose failures and crashes. Evidence lists every file. |

Package sizes are the resolved folders on disk, a footprint on the machine and not a build size. Registry and git packages sit in content-addressed cache folders, so their size is measured once per Editor session and kept in session state; local, embedded and tarball packages are walked on every scan.

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
