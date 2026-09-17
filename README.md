# Clarity Game Optimizer

Measure a Unity project along seven areas, get a verdict and a safe fix per row, and draw the result.

> **Status: pre-alpha.** `Window > Clarity Game Optimizer` scans the first area, Architecture: the project's assemblies as findings with a verdict each, `report.json` and `report.md`, and the assembly map as archify JSON, DOT and Mermaid, rendered to HTML when archify is on the machine. The other six areas are on the rail and say which release they are planned for. Follow the [milestones](https://github.com/alvaris924/unity-clarity-game-optimizer/milestones).

## What it will do

- **Seven areas, one window**: Architecture, Performance, Memory, Build Size, Assets, Dependencies, Code Quality; the rules that need a project's own knowledge, such as which folders hold third-party code, live in Project Settings and travel with the project
- **Measured, not guessed**: runtime bytes from the Profiler for the active build target, download bytes from the build report, texture formats read back from what Unity actually stored
- **Two budgets that never share a table**: download size and runtime RAM, because most intuitive fixes move one and not the other
- **A verdict and a batch fix per row**: dry run first, backups for anything rewritten, quarantine instead of delete, a receipt after every apply
- **Reports an agent or a CI job can read**: JSON, Markdown, DOT and Mermaid, plus a headless entry point
- **Diagrams**: every area exports [archify](https://github.com/tt-a1i/archify) JSON that renders to interactive HTML with one command; archify and Node.js stay optional
- Public Unity APIs only, UI Toolkit, zero third-party dependencies, MIT

## Requirements

Unity 2022.3 LTS or newer. Development happens on Unity 6000.2; the CI matrix in `.github/workflows/ci.yml` lists every version the tests run on.

## Install

Not published yet. Once `v0.1.0` is tagged, add the package from its git URL in the Package Manager:

```
https://github.com/alvaris924/unity-clarity-game-optimizer.git#v0.1.0
```

or through OpenUPM:

```
openupm add com.alvaris.clarity-game-optimizer
```

To render diagrams, put [archify](https://github.com/tt-a1i/archify) and Node.js 18 or newer on the machine (optional; the JSON, DOT and Mermaid files are written either way). Either install it as an agent skill, which the package finds on its own:

```
npx skills add tt-a1i/archify -g
```

or clone it anywhere and point the package at it once with the window's `Export > Locate archify`, or set `ARCHIFY_HOME`. The package writes diagrams for archify's `standard` quality profile and is tested against the release pinned in `Tests/Fixtures/archify/archify-version.json`.

## Roadmap

| Version | Scope |
|---|---|
| v0.1.0 | Read-only areas: Architecture (assembly map, layer rules, entry points), Dependencies (manifest, packages, plugins, collisions), Build Size (build report ranking, silent-uncompressed textures, output audit); scopes, settings page, JSON and Markdown reports, CLI entry point |
| v0.5.0 | Memory (textures, audio, read/write copies, fonts, scene exclusive bytes), Assets (reachability, duplicates, importer drift, leftovers), the fix framework with dry run, backups, git-clean gate and receipts, Build Size fixes, dataflow diagrams and before/after comparisons |
| v1.0.0 | Performance (render audit, settings with cost, play-session sampler, boot timeline), Code Quality (iteration-time metrics, reflection checks, text heuristics), extension API and sample, sequence and lifecycle diagrams, benchmarks and GIFs |

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) first. AI assistants follow [AGENTS.md](AGENTS.md). Design notes and decision records live in [Documentation~](Documentation~/index.md).

## License

[MIT](LICENSE.md). No third-party software is bundled; see [Third Party Notices.md](Third%20Party%20Notices.md).
