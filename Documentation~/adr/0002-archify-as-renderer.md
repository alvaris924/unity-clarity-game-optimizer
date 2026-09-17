# ADR-0002: archify renders the diagrams; the package owns layout and curation

- Status: accepted
- Date: 2026-09-17

## Context

Every area produces a graph worth drawing: assemblies and their references, packages and who uses them, what a prefab drags into memory, how a texture's size changes from source to build. Drawing it inside the Editor means writing and maintaining a graph renderer. `UnityEditor.Experimental.GraphView` has been experimental since 2018, draws only bezier edges and has no future; the newer Graph Toolkit is above the package's Unity floor and was still experimental when this was decided. An in-Editor diagram is also invisible to anyone without the project open, while the people who need to see these graphs are often producers, clients and reviewers.

[archify](https://github.com/tt-a1i/archify) is an MIT diagramming compiler: a typed JSON document validated against JSON Schemas and rendered deterministically to a self-contained, interactive HTML file with export to PNG, SVG and WebM. It ships as an agent skill and as a Node command line, so the same file renders during an AI session or in CI.

## Decision

The package writes diagrams in archify's intermediate representation, one file per curated view, and renders them by invoking the archify command line when Node.js and the skill are found on the machine; otherwise the file and the command to run are left for the user or an agent. Neither archify nor Node.js is a dependency of the package. Because archify has no automatic layout for architecture diagrams and expects about twelve primary nodes, the package computes a layered layout itself (column by dependency depth, row by rank) and curates before drawing: top-N by the area's metric, the rest collapsed into one node per boundary, at most five views per diagram. The full graph always goes out as DOT and Mermaid as well. The archify JSON Schemas are mirrored in the test fixtures, pinned to the archify version they came from, and CI runs the real command line against every file the exporters produce.

## Consequences

- No graph renderer to maintain; diagrams are shareable HTML with a viewer the package did not write.
- Rendering needs Node.js; the package stays fully useful without it through tables, JSON, DOT and Mermaid.
- The exporters are coupled to archify's schema version. The mirrored schemas and the CI job turn a schema change into a failing test rather than a broken user.
- An in-Editor graph panel remains possible later, reading the same files, because layout is computed in the package.

## Alternatives considered

- Draw graphs in the Editor with UI Toolkit: feasible for the static look, but a second renderer to maintain and not shareable. Deferred to a later phase as a panel over the same files.
- GraphView or Graph Toolkit: experimental, version-bound, bezier-only or above the floor. Rejected.
- Vendor archify's renderer into the package to avoid Node.js: a JavaScript port to keep in step with a fast-moving upstream. Rejected.
- Mermaid only: no validation, no evidence links, weaker layout control. Kept as a fallback export, not the primary one.
