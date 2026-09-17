# AGENTS.md

Binding rules for every AI coding assistant that works in this repository: Claude Code, OpenAI Codex and ChatGPT agents, GitHub Copilot, Cursor, Gemini CLI or anything else. `CLAUDE.md` and `.github/copilot-instructions.md` defer to this file. If this text was pasted into a chat, treat it as binding for anything produced for this repository.

## The project in one paragraph

Clarity Game Optimizer is an open-source, dependency-free project analyzer for the Unity Editor, shipped as the UPM package `com.alvaris.clarity-game-optimizer`. It measures a project along seven areas (Architecture, Performance, Memory, Build Size, Assets, Dependencies, Code Quality) and turns every scan into a ranked findings table with a verdict and a safe batch fix per row, a machine-readable report (JSON, Markdown, DOT, Mermaid) and a diagram in the JSON intermediate representation of [archify](https://github.com/tt-a1i/archify), which renders it to HTML. The package sits at the repository root; the development Unity project lives in `DevProject~/`, which UPM consumers never see. Assemblies: `ClarityGameOptimizer.Core` (pure C#, no engine references: report model, graph, layout, exporters), `.Analysis` (analyzers, scopes, fixes), `.UI` (UI Toolkit window), `.Settings`, `.Extensions` (the only public API), `.Cli` (headless entry point) and `.Tests`. Unity floor 2022.3. License MIT. Design notes and decision records live in `Documentation~/`.

## Workflow, non-negotiable

1. **Ask before every git action.** Never commit, push, tag, merge or open a pull request without the user's explicit go-ahead in the current conversation turn. Approval given earlier does not carry over.
2. **Never touch `main` directly.** No direct pushes, no force-pushes, no rewriting of pushed history. `main` is protected on the server for administrators too; do not look for a way around it.
3. **One route for every change.** Update `main`, cut a branch named `type/topic` (`feat/`, `fix/`, `docs/`, `chore/`, `ci/`, `refactor/`, `perf/`, `test/`), commit, push the branch, open a pull request against `main` using `.github/PULL_REQUEST_TEMPLATE.md`, wait for the `PR title` check, report the PR URL. Squash merge only, and only with green checks.
4. **Conventional Commits everywhere.** Commit messages and PR titles are `type(scope): subject`. Types: feat, fix, docs, chore, ci, refactor, perf, test, build, revert. Scopes: core, analysis, ui, settings, extensions, cli, export, tests, samples, docs, ci, release, deps. Subject in lowercase imperative, no trailing period, header under 72 characters. Breaking changes use `type(scope)!:` plus a `BREAKING CHANGE:` footer.
5. **Local hooks stay on.** Once per clone run `git config core.hooksPath .githooks`. The hooks reject non-conforming commit messages and pushes to `main`. Never use `--no-verify`.
6. **Changelog and docs move with the code.** Every user-visible change adds a line to `CHANGELOG.md` under `[Unreleased]`; otherwise the PR gets the `skip-changelog` label. `README.md` and `Documentation~/` change whenever behaviour or public API changes.
7. **Issues first.** Features and bugs start as issues with one `area:`, one `phase:` and one `priority:` label and a milestone. PRs reference them with `Closes #N`.
8. **Releases are deliberate.** A PR titled `chore(release): vX.Y.Z` bumps `package.json` and dates the changelog section. After it merges, `vX.Y.Z` is tagged on `main`. Nothing else is ever tagged.

## Engineering rules

- Zero third-party dependencies in the package. Only built-in modules and Unity-registry packages may appear in `package.json`. TextMeshPro and URP checks sit behind asmdef version defines. archify and Node.js are optional external tools found by path at run time, never dependencies.
- Public Unity APIs only in the default path. No reflection into `UnityEditor` internals, no Harmony patching.
- Unity floor 2022.3. Newer APIs are version-guarded (`#if UNITY_6000_0_OR_NEWER`) and covered by the CI matrix.
- Public API lives only in `ClarityGameOptimizer.Extensions`. Everything else is `internal`, with `InternalsVisibleTo` for tests.
- Measured, not guessed. Runtime bytes come from the Profiler for the active build target, download bytes from the build report, texture formats from what Unity stored. Every number carries the budget it belongs to (download size, runtime RAM, frame time, iteration time) and the two size budgets are never mixed in one table.
- Scans never write. A fix is a separate object with a dry run that lists the exact files it would change, backs up any source file it rewrites, refuses to run on a dirty git tree unless told otherwise, and quarantines instead of deleting.
- Diagram exports must pass `archify validate --quality showcase` unchanged. Curate before drawing: top-N nodes by the area's metric, the rest collapsed, at most five views; the full graph goes to DOT and Mermaid.
- Editor-only assemblies. `[InitializeOnLoad]`, `ScriptableSingleton` and stateless static helpers are the sanctioned static entry points; avoid other static state. Scan results are never serialized across a domain reload.
- UI Toolkit element trees are built in C# and styled with USS. No UXML custom-element registration, so 2022.3 and 6000.x share one code path. No `GraphView`.
- Logic in `ClarityGameOptimizer.Core` ships with EditMode tests. Fixtures come from real build reports, asmdef sets and archify schema files, pinned to the archify version they were taken from.
- Style per `.editorconfig`: Allman braces, four spaces, LF, UTF-8 without BOM, `_camelCase` private fields, C# 9 as Unity compiles it (no records, no init-only setters).
- Clean room: never read, decompile or copy code from proprietary plugins, and never reproduce their UI text, icons or docs. Features come from public Unity APIs and this project's own design. If such a plugin is present in the host project, do not open its files while working on this package.

## Definition of done for a pull request

CI green on every Unity version in the matrix, tests added or updated, changelog line present or `skip-changelog` applied, docs updated, self-review done, every item of the PR template checklist ticked.

## Commands

```
git config core.hooksPath .githooks                 # once per clone
unity test DevProject~ --mode EditMode              # EditMode tests with the Unity CLI, or use the Test Runner window
gh pr create --base main --title "type(scope): subject" --body-file .github/PULL_REQUEST_TEMPLATE.md
```
