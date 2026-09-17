# ADR-0004: Unity 2022.3 LTS is the floor

- Status: accepted
- Date: 2026-09-17

## Context

The package is developed on Unity 6000.2 and is meant to be installed into client projects, many of which sit on the older long-term-support line. The APIs the analyzers need, `CompilationPipeline`, `TypeCache`, `BuildReport`, `Profiler.GetRuntimeMemorySizeLong`, `ProfilerRecorder`, texture importer platform settings and `MultiColumnListView`, all exist in 2022.3. Two things differ across the range: per-asset build sizes require `BuildOptions.DetailedBuildReport` on Unity 6, and the way the latest build report is located changed.

## Decision

`package.json` declares `"unity": "2022.3"`. Anything newer is version-guarded with `#if UNITY_6000_0_OR_NEWER` behind a thin adapter, and the CI matrix runs the EditMode tests on 2022.3, 6000.0, 6000.3 and the latest 6000.x once the matching images are confirmed. UI Toolkit trees are built in C# so 2022.3 and 6000.x share one code path.

## Consequences

- Client projects on the older LTS can adopt the package.
- The build-report reader carries one shim for locating the last report.
- New Unity features are adopted only behind guards, which keeps the matrix meaningful.

## Alternatives considered

- Floor at 6000.0: simpler build-report code and `TabView`, rejected because it excludes the projects most likely to need the tool.
- Floor at 2021.3: `MultiColumnListView` is missing, so the window would need a second implementation. Rejected unless demand appears.
