# ADR-0005: Zero third-party dependencies

- Status: accepted
- Date: 2026-09-17

## Context

A UPM package installed from a git URL cannot depend on another git URL, and OpenUPM only resolves packages that are themselves on a registry. Async libraries, reactive libraries, serializers and inspector frameworks would each add a resolution step for every consumer and a version to keep compatible. The package also needs to read files and write JSON, which is tempting to hand to a serializer.

## Decision

`package.json` lists only built-in modules and Unity-registry packages. JSON is written by a small hand-written writer in Core. Integrations with optional packages, TextMeshPro for font atlases and the Universal Render Pipeline for the render audit, are compiled only when those packages are present, through asmdef version defines. archify and Node.js are external tools located by path at run time and are never dependencies; every export except the rendered HTML works without them. Roslyn analyzers, if they arrive, are built in `Tools~` outside the package and shipped as a labelled DLL with no runtime dependency.

## Consequences

- One-step install on every supported Unity version from a git URL or OpenUPM.
- Some code is written that a library would have provided: the JSON writer, a small layout algorithm, text scanning helpers.
- Optional integrations must be tested with and without the package present.

## Alternatives considered

- Depend on `com.unity.nuget.newtonsoft-json`: it is on the Unity registry, so it is allowed by this policy, but the writer needed here is thirty lines and the reader is not needed at all. Not used.
- Vendor a library's source: a maintenance burden and a licensing surface. Rejected.
