# Report format

Every scan writes one `report.json`. The shape is the same for all seven areas (ADR-0001), so an agent or a CI job reads one format, and two reports over the same scope can be diffed row by row. The file starts with a format name and a version; the version follows the package's semantic versioning, with a new major when a field changes meaning.

```json
{
  "format": "clarity-game-optimizer/report",
  "formatVersion": 1,
  "area": "Architecture",
  "scope": "Project",
  "unityVersion": "6000.2.9f1",
  "buildTarget": "Android",
  "packageVersion": "0.1.0",
  "createdAt": "2026-09-17T04:00:00Z",
  "metrics": [ { "key": "assemblies", "label": "Assemblies", "value": 62, "unit": "count", "budget": "None" } ],
  "findings": [
    {
      "id": "texture.max-size:Assets/Atlas_01.png",
      "check": "texture.max-size",
      "subject": "Assets/Atlas_01.png",
      "title": "4096 atlas without a platform override",
      "severity": "Warning",
      "budget": "RuntimeRam",
      "measured": { "value": 12480102, "unit": "bytes" },
      "projected": { "value": 734003, "unit": "bytes" },
      "verdict": "Max Size 1024",
      "fixId": "texture.max-size",
      "evidence": [ { "path": "Assets/Atlas_01.png", "line": 0, "endLine": 0, "label": "texture" } ]
    }
  ],
  "graph": {
    "nodes": [ { "id": "game", "label": "Game.Runtime", "sublabel": "312 scripts", "kind": "Runtime", "group": "Runtime", "weight": 9, "evidence": [] } ],
    "edges": [ { "from": "game", "to": "cbs", "label": "references", "weight": 1 } ]
  },
  "notes": [ "Counts come from CompilationPipeline." ]
}
```

Every field is always present; empty lists are `[]`, a missing measurement or fix is `null`. Order is insertion order and is deterministic for a given project state. Numbers use a period as the decimal separator on every machine.

## Fields

| Field | Meaning |
|---|---|
| `area` | One of `Architecture`, `Performance`, `Memory`, `BuildSize`, `Assets`, `Dependencies`, `CodeQuality`. |
| `scope` | What was looked at, in words: `Project`, `Last build (Android, 2026-09-16)`, `Prefab: Managers`. |
| `buildTarget` | The active build target the numbers were measured for; empty when the area does not depend on one. Runtime sizes and stored texture formats change with it. |
| `createdAt` | UTC, ISO 8601 to the second. |
| `metrics[]` | One number each for the whole scope. `key` is stable and unique; `label` is for people. |
| `findings[]` | One row per `check` per `subject`; `id` is the two joined with a colon. `subject` is the natural key (asset path, assembly name, package id) that other areas cross-link on. |
| `findings[].severity` | `Info` (a fact), `Advice` (safe to skip), `Warning` (a real cost with a fix), `Critical` (blocks a goal). Everything above `Info` carries a `verdict`. |
| `findings[].budget` | Which budget `measured` and `projected` belong to: `Download`, `RuntimeRam`, `FrameTime`, `IterationTime`, or `None` for counts. Never add values across budgets. |
| `findings[].measured` / `projected` | What it costs now and what it would cost after the fix, in the same unit. `projected` is `null` without a fix or an estimate. |
| `findings[].fixId` | The fix that applies, or `null` when the finding is informational or needs a person. |
| `evidence[]` | Project-relative paths with forward slashes, optional 1-based `line` and `endLine` (0 when unused) and a short `label`. The same shape appears on graph nodes and becomes archify's `sources`. |
| `graph` | Nodes typed by `kind` (`Runtime`, `Editor`, `Data`, `Service`, `Messaging`, `Test`, `External`), grouped by `group` (the boundary they belong to) and ranked by `weight` (fan-in, bytes, references); directed `edges` with an optional `label` and `weight`. |
| `notes[]` | Caveats the reader must see, such as the Editor reporting an atlas at twice its device size. |

## Units

| `unit` | Shown as |
|---|---|
| `bytes` | `1023 B`, `1.5 KB`, `11.9 MB`, `1.5 GB`, in 1024 steps with one decimal at most |
| `ms` | `12.3 ms` |
| `s` | `2.5 s` |
| `count` | `1,026` |
| `percent` | `41.3 %` |
| anything else | the value and the unit as given |

## Markdown

`report.md` carries the same content for a pull request comment: an environment table, a one-line summary by severity, the metrics, then one findings table per budget in the order runtime RAM, download size, frame time, iteration time, unbudgeted. Within a table the most severe finding comes first, then the largest, then the subject in ordinal order. Two size budgets never share a table.
