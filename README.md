# BimTools.FFTDIM

**A dual-track toolkit for fire-fighting (FF) dimension QA in BIM models.** This repository contains two complementary components that can be used independently:

1. **`src/BimTools.FFTDIM/`** — A host-neutral .NET 8 command-line auditor that validates BIM element data exchanged as JSON. Runnable in CI without a BIM host.
2. **`FFDimAddin2020/`** — A Revit 2020 C# add-in that audits, cleans and re-creates dimensions for fire-fighting content directly inside the model.

[![Build and smoke test](https://github.com/amdghedy/BimTools.FFTDIM/actions/workflows/build.yml/badge.svg)](https://github.com/amdghedy/BimTools.FFTDIM/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

| Component | Language / framework | Host | Modifies model? | CI-built? |
|---|---|---|---|---|
| `src/BimTools.FFTDIM/` | .NET 8 / C# | None (CLI on JSON) | No | Yes |
| `FFDimAddin2020/` | .NET Framework 4.8 / C# 7.3 | Revit 2020 | Some commands do | No (requires Revit API) |

---

## Component 1 — BimTools.FFTDIM CLI auditor

**FFTDIM starter implementation: a configurable BIM data quality auditor.** This component is a standalone .NET 8 command-line tool that audits neutral JSON element data; it does **not** connect directly to Revit or another BIM authoring application's API.

### What works in this first implementation

- Read model element records from JSON.
- Count elements by category.
- Detect missing element IDs and categories.
- Detect duplicate IDs or other configured unique fields.
- Check required parameters by category, including wildcard rules.
- Validate numeric parameter ranges and flag non-numeric values.
- Export detailed findings to JSON and CSV.
- Return useful process exit codes for automation and CI.
- Run a GitHub Actions build and sample-data smoke test.

This is an initial foundation — not a claim that model geometry, native authoring API integration, clash detection, or direct model editing is implemented.

### Quick start

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

Clone the repository and run the included sample:

```bash
git clone https://github.com/amdghedy/BimTools.FFTDIM.git
cd BimTools.FFTDIM

dotnet build src/BimTools.FFTDIM/BimTools.FFTDIM.csproj --configuration Release

dotnet run --project src/BimTools.FFTDIM -- audit samples/sample-model.json --rules samples/audit-rules.json --out artifacts/report.json --csv artifacts/report.csv
```

The sample intentionally contains issues so that you can see findings in the report. The command returns exit code `1` if error-severity findings exist, `0` if the audit completes without errors, and `2` for invalid arguments, malformed input, or I/O failures. Warnings alone do not change the exit code.

### Input format

Create a JSON file containing a model name and an `elements` array. Each element has an `id`, `category`, and a `parameters` object. Parameter values may be strings, numbers, booleans, or null.

```json
{
  "modelName": "Example coordination model",
  "elements": [
    {
      "id": "W-001",
      "category": "Walls",
      "parameters": {
        "Mark": "W01",
        "HeightMm": 3000,
        "FireRatingMinutes": 120
      }
    }
  ]
}
```

The JSON exchange format is deliberately host-neutral. You can generate it from a spreadsheet, an export pipeline, or a future adapter to a BIM application. This version does not read native model files.

### Configurable audit rules

The example configuration is in [samples/audit-rules.json](samples/audit-rules.json).

```json
{
  "requiredParametersByCategory": {
    "*": ["Mark"],
    "Walls": ["HeightMm", "FireRatingMinutes"],
    "Doors": ["HeightMm"]
  },
  "uniqueIdFields": ["id", "Mark"],
  "numericRanges": {
    "HeightMm": { "min": 100, "max": 15000 },
    "FireRatingMinutes": { "min": 0, "max": 240 }
  }
}
```

- `requiredParametersByCategory`: required fields for a specific category. The `*` key applies to every element.
- `uniqueIdFields`: fields that should be unique across the input elements. Use `id`, `category`, or a parameter name.
- `numericRanges`: optional minimum and maximum limits for numeric parameters. Numeric strings are accepted using invariant culture.

Rule and parameter names are compared case-insensitively. A parameter that is absent, null, or an empty string is treated as missing for required-parameter checks.

### Command options

```text
dotnet run --project src/BimTools.FFTDIM -- audit <model.json> [--rules rules.json] [--out report.json] [--csv report.csv]
```

| Option | Purpose |
|---|---|
| First positional argument | Input model JSON file (required) |
| `--rules` | Rule configuration JSON; defaults to built-in rules if omitted |
| `--out` | JSON report path; defaults to `fftdim-audit-report.json` |
| `--csv` | Optional CSV export of findings |

The output directory is created when it does not exist. CSV values are escaped for commas, quotes, and line breaks.

### Finding reference

| Code | Severity | Meaning |
|---|---|---|
| FTD001 | Error | Element ID is missing |
| FTD002 | Warning | Element category is missing |
| FTD003 | Error | Configured unique field contains duplicate values |
| FTD004 | Warning | Required parameter is missing or empty |
| FTD005 | Warning | A parameter configured for numeric validation is not numeric |
| FTD006 | Warning | Numeric value is below the configured minimum |
| FTD007 | Warning | Numeric value is above the configured maximum |

Each finding includes the rule code, severity, element ID/category when available, and an actionable message. The JSON report also includes totals and element counts by category.

---

## Component 2 — FFDimAddin2020 Revit 2020 add-in

> **Fire-Fighting Tools — Dimension QA suite for Autodesk Revit 2020**

FFTDIM (**FF** = Fire-Fighting, **DIM** = Dimension QA) is a Revit 2020 add-in that audits, cleans and creates dimensions for fire-fighting content: sprinklers, fire-protection piping, fittings and fire equipment. The boundary between build-verified facts and items that require live verification in Revit is stated explicitly (see *Quality assurance and verification* below).

**What's new in v1.2:**

- **FFD-10 Linked-Model Audit** — deep analysis of dimensions that reference linked Revit models: per-link inventory (load status, fire-element count), resolution of every cross-link reference down to the element inside the linked document, broken cross-link detection, zero-value/segment checks across the boundary, link-aware duplicate detection and optional fire-fighting coverage inside each loaded link.
- **FFD-11 BCF Export** — writes audit findings as a **BCF 2.1** package (`*.bcf`) for issue round-trips with BIMcollab, Revizto, Autodesk Construction Cloud, Solibri and Trimble Connect: one topic per finding or one merged topic, each with description, comments, a viewpoint camera (3D orientation or synthesized top-down for plans) and the involved Revit elements as components (`AuthoringToolId`).
- **FFD-12 BCF Import** — reads any BCF 2.1 package back, lists its topics in a preview window (status, author, date, component counts) and resolves the referenced element ids against the current model; one click selects the resolved elements for re-verification.
- **3 new options** (Settings & Options › *Linked models & BCF*): linked-model coverage check on/off, BCF topic granularity, BCF viewpoint cameras. Persisted in `settings.json`.
- All new windows and ribbon captions localise (English / العربية, right-to-left aware).

**What's new in v1.1:**

- **FFD-08 Settings & Options** — one dialog for every tunable: zero-value threshold, spacing-chain offset/extension, clearance skip threshold and search radius, walls-over-grids preference, default report format, interface language. Persisted to `%APPDATA%\BimTools\FFTDIM\settings.json`.
- **FFD-09 Batch Audit Views** — tick any number of plans/sections/elevations/drafting/3D views and audit them in one run; findings are merged and tagged with their view.
- **Pipe clearance (FFD-06)** — linear fire pipes are now dimensioned to the nearest wall face/grid at the middle of the run, alongside point families.
- **Spot dimensions** — opt-in inclusion in scans (spot elevations/coordinates stay out by default to avoid noise).
- **Linked-model flagging** — dimensions whose references resolve into a linked Revit model are reported (Info) instead of being silently skipped.
- **JSON reports** — machine-readable export alongside CSV and HTML (Power BI / CI friendly).
- **Richer results window** — severity filter, live text search, View column, CSV/HTML/JSON export buttons.
- **Arabic UI** — captions of windows and the ribbon localise through a built-in string table (English / العربية, right-to-left aware).
- **Multi-version install** — `install.cmd 2020|2021|2022|2023|2024` writes the manifest to the matching per-user add-ins folder; the same net48 assembly serves all five versions.
- Fixed a latent compile defect: `DimensionScanner.cs` used LINQ without `using System.Linq;`.

### What FFTDIM does

FFTDIM adds a ribbon tab **FF Tools** to Revit with three panels of commands:

| Panel | Commands |
|---|---|
| **Dimension QA** | Audit Dimensions · Clean Redundant · Coverage Check · Tolerance Check · Batch Audit Views · Linked Model Audit |
| **Quick Dimension** | Spacing Chain · Clearance |
| **Output** | Export Report · BCF Export · BCF Import · Settings & Options |

The problem it addresses: on fire-fighting documentation, dimension sets drift out of sync with the model — dimensions snap to deleted geometry, duplicate copies pile up, sprinkler layouts get modified without their dimensions following, and spacing rules are only checked by eye. FFTDIM turns those checks into one-click commands and produces exportable evidence (CSV/HTML) of what was checked and what failed.

### Feature catalogue

| Command | Ribbon entry | Selection scope | Model changes? | Output |
|---|---|---|---|---|
| Audit Dimensions | FF Tools › Dimension QA | Active view | No | Results window, CSV/HTML export |
| Clean Redundant | FF Tools › Dimension QA | Active view | Deletes dimensions | Confirmation dialog, transaction log |
| Coverage Check | FF Tools › Dimension QA | Active view | No | Results window, CSV/HTML export |
| Tolerance Check | FF Tools › Dimension QA | Active view | No (edits user settings file) | Rules editor + results window |
| Spacing Chain | FF Tools › Quick Dimension | Selected elements | Creates one dimension | New dimension, auto-selected |
| Clearance | FF Tools › Quick Dimension | Selected elements | Creates dimensions | New dimensions, auto-selected |
| Export Report | FF Tools › Output | Active view | No | CSV / HTML / JSON file |
| BCF Export | FF Tools › Output | Active view | No | BCF 2.1 package (`.bcf`) |
| BCF Import | FF Tools › Output | — (file) | No | Topic preview window + selection |
| Linked Model Audit | FF Tools › Dimension QA | Active view + all links | No | Results window, CSV/HTML/JSON export |
| Settings & Options | FF Tools › Output | — (application level) | No | `settings.json` |

### Supported environment

| Item | Value |
|---|---|
| Host application | Autodesk Revit 2020 (build 20.x) |
| Target framework | .NET Framework 4.8 |
| Language / toolchain | C# 7.3 · Visual Studio 2019/2022 or `dotnet build` (≥ .NET SDK 6) |
| Platform | Windows x64 |
| API references | `RevitAPI.dll`, `RevitAPIUI.dll` from the Revit 2020 install (not redistributed) |
| Install scope | Per-user (`%APPDATA%\Autodesk\Revit\Addins\2020`) |
| UI language | English / العربية (Arabic, right-to-left aware) |

Revit 2021–2024 load 2020-built `.NET Framework 4.8` add-ins **only if** they ship a matching `*.addin` manifest per version. `install.cmd` accepts a version argument (`install.cmd 2021`) and copies the same manifest + assembly into that version's per-user add-ins folder; the API surface used by FFTDIM is stable across 2020–2024, but each version should still be verified live before production use.

### Build

Prerequisites:

1. Visual Studio 2019 or 2022 **or** the .NET SDK with `dotnet` CLI.
2. .NET Framework 4.8 Developer Pack / targeting pack.
3. Revit 2020 installed (provides `RevitAPI.dll` / `RevitAPIUI.dll`).

Build in Visual Studio: open `FFDimAddin2020.sln`, select **Release**, build.

Build with the CLI:

```bash
dotnet build FFDimAddin2020.sln -c Release
```

If Revit 2020 is not at the default location, point the build at the API folder:

```bash
dotnet build FFDimAddin2020.sln -c Release -p:RevitApiPath="D:\Autodesk\Revit 2020"
```

Output: `FFDimAddin2020/bin/Release/FFDimAddin2020.dll`.

### Installation

**Option A — script (recommended):**

```bat
install.cmd          REM Revit 2020 (default)
install.cmd 2022     REM Revit 2022
```

The script copies the manifest and the built DLL into `%APPDATA%\Autodesk\Revit\Addins\<version>\`. Supported versions: 2020–2024.

**Option B — manual:**

1. Copy `FFDimAddin2020/FFDimAddin2020.addin` to `%APPDATA%\Autodesk\Revit\Addins\2020\`.
2. Create the folder `%APPDATA%\Autodesk\Revit\Addins\2020\FFDimAddin2020\`.
3. Copy `FFDimAddin2020/bin/Release/FFDimAddin2020.dll` into it.

Restart Revit. Verify: the **FF Tools** tab appears in the ribbon.

### Command reference

#### FFD-01 · Audit Dimensions
- **Purpose:** find broken, duplicated and orphaned dimensions before issuing drawings.
- **Selection scope:** every `Dimension` element visible in the active view (plan, section, elevation, drafting). Schedules, sheets and legends are rejected with a message.
- **Checks performed:**
  - *Zero-value dimensions* — value below the configurable zero-value threshold (default 0.5 mm), typically a snapped-to-nothing reference (Error).
  - *Duplicate dimensions* — two dimensions sharing the exact same reference set (Warning; the one with the lower element id is treated as the keeper).
  - *Orphaned references* — a reference points at an element id that no longer resolves (Warning).
  - *Zero-length chain segments* — a multi-segment dimension contains a dead segment (Error).
  - *Linked-model references* — a reference resolves into a linked Revit file; the nested geometry is outside the scope of the detailed checks (Info, can be disabled).
  - *Spot dimensions* — spot elevations and spot coordinates are scanned only when enabled in Settings & Options (they are excluded by default because their fixed values would flood the zero-value and duplicate checks with noise).
- **Outputs:** results window with severity filter, live text search, severity/type/element/value/view/detail columns; double-click a row to select and zoom to the element; CSV/HTML/JSON export.
- **Failure handling:** read-only; no transaction is opened. Errors surface through the Revit failure message.

#### FFD-02 · Clean Redundant
- **Purpose:** one-click cleanup of the two defect classes above that are safe to auto-fix.
- **Selection scope:** active view.
- **Processing:** finds duplicate groups and zero-value dimensions → shows a confirmation with exact counts → deletes inside a single transaction named `FFTDIM: Clean redundant dimensions`.
- **Outputs:** count of deleted elements (Revit may also remove dependents, which is included in the count).
- **Failure handling:** transaction rolls back on exception; always undoable with Ctrl+Z.

#### FFD-03 · Coverage Check
- **Purpose:** catch fire-fighting elements that exist in the view but are referenced by no dimension.
- **Selection scope:** active view. Builds the set of every element referenced by any dimension, then lists fire-fighting elements not in that set (Warning per element).
- **Outputs:** results window and export, same as FFD-01.
- **Failure handling:** read-only.

#### FFD-04 · Tolerance Check
- **Purpose:** verify dimension values against configurable layout rules (e.g. sprinkler spacing envelope).
- **Inputs:** user-edited rules (name, scope, min/max in metres, on/off) persisted in `%APPDATA%\BimTools\FFTDIM\tolerance_rules.json`.
- **Processing:** for every dimension whose references include elements matching a rule's scope — `Sprinkler`, `Pipe` or `All` — each value (single value or every chain segment) is compared: below minimum → **Error**; above maximum → **Warning**. Each violated rule produces one finding; the rule name appears in the detail column.
- **Failure handling:** read-only; unreadable settings file falls back to defaults.

#### FFD-05 · Spacing Chain
- **Purpose:** create a spacing dimension across a run of sprinklers in seconds.
- **Selection:** two or more point-based fire-fighting families (sprinklers, point fittings) in a **plan view**.
- **Processing:** orders the elements along the dominant axis of the view, then creates one multi-segment dimension through each family's centre reference planes (`Center (Left/Right)` preferred, `Center (Front/Back)`, then strong/weak fallbacks), offset 1 m perpendicular to the chain.
- **Outputs:** the new dimension is created, selected and zoomed to.
- **Failure handling:** single transaction, rolled back entirely if Revit rejects the dimension; families without usable centre references are skipped and listed.

#### FFD-06 · Clearance
- **Purpose:** dimension each selected fire-fighting element to its nearest constraint.
- **Selection:** one or more point-based fire-fighting families **or linear fire pipes** in a **plan view**.
- **Processing:** per element, finds the nearest grid line or wall **in the active view** within the configured search radius (default 10 m; wall interior face preferred, exterior face fallback; grid centre line otherwise), then creates a dimension from the element centre — or, for pipes, from the middle of the run's centreline — to that target along the perpendicular direction. Elements within the skip threshold (default 50 mm) of the target are skipped. The walls-over-grids preference (Settings & Options) makes the closest wall win even when a grid line is marginally closer.
- **Outputs:** all created dimensions are selected and zoomed to; skipped elements are listed with reasons.
- **Failure handling:** one transaction for the whole batch; per-element failures are reported individually and do not abort the batch. Pipe clearance uses the pipe's location-curve reference first and the element centreline reference as fallback; unsupported pipe shapes are skipped with the reason.

#### FFD-07 · Export Report
- **Purpose:** produce reviewable evidence of the audit state of a view.
- **Processing:** runs the same audit as FFD-01 and writes a timestamped file (`FFTDIM_Audit_yyyyMMdd_HHmm.csv|html|json`). CSV is UTF-8 with BOM (Excel-safe); HTML is a self-contained styled table; JSON is machine-readable (Power BI / CI friendly). The format chosen in Settings & Options is marked as the default in the chooser.

#### FFD-08 · Settings & Options
- **Purpose:** one place to tune every threshold and default the add-in uses.
- **Scope:** application level — works without an open document, never touches the model.
- **Options:** zero-value threshold (mm), include spot dimensions, flag linked-model references, linked-model coverage check, BCF topic granularity, BCF viewpoint cameras, spacing-chain offset and end extension (m), clearance skip threshold (mm), clearance search radius (m), walls-over-grids preference, default report format (CSV/HTML/JSON), interface language (English/العربية).
- **Persistence:** `%APPDATA%\BimTools\FFTDIM\settings.json`. Missing entries in an older file fall back to defaults, so upgrades are safe. Language changes apply to new windows immediately and to the ribbon after the next Revit start.

#### FFD-09 · Batch Audit Views
- **Purpose:** audit a whole document (or a selected subset of views) in one run instead of repeating FFD-01 view by view.
- **Selection:** a checklist of every auditable view (floor/ceiling/structural plans, sections, elevations, drafting, 3D) — templates, schedules, sheets and legends are excluded automatically.
- **Processing:** runs the FFD-01 audit per selected view and merges the findings; every issue keeps the name of its view. Read-only.
- **Outputs:** the shared results window (filter by severity, search, export) with the View column populated; a single CSV/HTML/JSON export covers all audited views.

#### FFD-10 · Linked-Model Audit
- **Purpose:** deep analysis of dimensions that measure geometry in linked Revit models — the step beyond the FFD-01 "linked reference" flag.
- **Selection scope:** the active view (for dimensions) plus **every** `RevitLinkInstance` of the host document (for inventory and coverage).
- **Checks performed:**
  - *Link inventory* — one Info finding per link: model name, load status, number of fire-fighting elements found inside the linked document (whole-document scan, not view-scoped).
  - *Unloaded links* — a link whose document cannot be opened is reported (Warning); dimensions referencing it cannot be resolved and its coverage cannot be checked.
  - *Cross-link reference resolution* — each reference with a `LinkedElementId` is resolved to the element inside the link document and described (category, name, id, link name, fire-fighting classification).
  - *Broken cross-link references* — the reference names an element id that no longer exists inside the linked model (Error; the dimension is dead and safe to delete).
  - *Zero-value / zero-length-segment* across the boundary — same thresholds as FFD-01, with a hint when the dimension also references linked geometry.
  - *Link-aware duplicates* — the duplicate key mixes host ids (`H:id`) and link pairs (`L:link:element`), so a host-only dimension and a linked-geometry dimension never collide, while two dimensions measuring the same linked element do.
  - *Linked fire-element coverage* (optional, on by default) — fire-fighting elements living inside each loaded link that no dimension of the active view references (Warning per element). Selecting such a finding highlights the link instance: Revit cannot select nested link elements from a host selection.
- **Outputs:** shared results window + CSV/HTML/JSON export; the summary line includes the number of links scanned.
- **Failure handling:** read-only; unloaded or unreadable links degrade to warnings, never to a failed command.

#### FFD-11 · BCF Export
- **Purpose:** hand audit findings to collaborators and BCF platforms as industry-standard BIM Collaboration Format issues.
- **Processing:** runs the FFD-01 audit of the active view and writes a BCF **2.1** ZIP package:
  - `bcf.version` (2.1) + `project.bcfp` (project name = document title);
  - one topic folder per finding, or a single merged topic — per the *topic granularity* option;
  - `markup.bcf` with topic title `[IssueType] Element`, description (severity/element/value/view/detail), an Open status, FFTDIM labels and one comment per finding;
  - `viewpoint.bcfv` (when the camera option is on): a perspective camera taken from the 3D view orientation, or synthesized above the crop-box centre for 2D views, plus the involved Revit elements listed as `Component` entries with `AuthoringToolId` = Revit element id (max 50 per topic).
- **Units:** camera coordinates are written in metres (BCF SI expectation; Revit internal feet × 0.3048).
- **Snapshots:** intentionally omitted — reliable screen capture is not available inside a modal command; BCF viewers fall back to the camera.
- **Outputs:** timestamped `FFTDIM_Audit_yyyyMMdd_HHmm.bcf`; the dialog offers to locate the file in Explorer.
- **Failure handling:** read-only on the model; write errors (file locked, read-only folder) surface with a clear message.

#### FFD-12 · BCF Import
- **Purpose:** close the round-trip — review topics coming back from (or written by) any BCF tool and locate the affected geometry in the current model.
- **Inputs:** a `.bcf` / `.bcfzip` package. Validation rejects non-BCF ZIPs (no `bcf.version`) with a clear message; parsing is namespace-tolerant so packages from other tools import as long as they are BCF 2.x.
- **Processing:** lists every topic (title, status, author, creation date) in a preview window; component references (`AuthoringToolId`, decimal Revit element ids, from viewpoint selection and visibility exceptions) are resolved against the current document; unresolved references are counted, not failed — they may belong to a linked model or another project.
- **Outputs:** topic list + detail pane (description and comments); *Select in model* highlights and zooms to the resolved elements of the selected topic for re-verification.
- **Failure handling:** read-only; unreadable viewpoint files are skipped silently.

### Configuration — tolerance rules

File: `%APPDATA%\BimTools\FFTDIM\tolerance_rules.json`
Format (edited via the FFD-04 dialog; fields are metres, `0` = no limit):

```json
[
  {
    "Name": "Sprinkler spacing (layout envelope)",
    "Scope": "Sprinkler",
    "MinMeters": 1.8,
    "MaxMeters": 4.5,
    "Enabled": true
  }
]
```

> The shipped defaults are **layout envelopes, not code requirements**. Adapt them to the standard governing your project (e.g. NFPA 13, EN 12845, local civil defence requirements) before relying on the results.

### Configuration — options (`settings.json`)

File: `%APPDATA%\BimTools\FFTDIM\settings.json` (edited via the FFD-08 dialog):

```json
{
  "ZeroValueThresholdMm": 0.5,
  "IncludeSpotDimensions": false,
  "FlagLinkedReferences": true,
  "LinkedCoverageCheck": true,
  "BcfTopicPerIssue": true,
  "BcfIncludeCamera": true,
  "ChainOffsetMeters": 1.0,
  "ChainExtensionMeters": 0.3,
  "ClearanceSkipMm": 50.0,
  "ClearanceSearchRadiusMeters": 10.0,
  "PreferWallsOverGrids": false,
  "DefaultReportFormat": 0,
  "Language": 0
}
```

`DefaultReportFormat`: 0 = CSV, 1 = HTML, 2 = JSON. `Language`: 0 = English, 1 = Arabic. `LinkedCoverageCheck` controls the fire-element coverage phase of FFD-10. `BcfTopicPerIssue` (true = one topic per finding) and `BcfIncludeCamera` shape FFD-11. The file is shared by every installed Revit version; entries written by an older release keep their defaults until edited.

### Interface language

FFTDIM ships with an English and an Arabic (العربية) UI. Window titles, column headers, buttons and option labels are localised through a built-in string table; windows switch to right-to-left layout under Arabic. Ribbon captions follow the language saved in `settings.json` on the next Revit start. Command tooltips and TaskDialog messages remain English in this release — they are documented in this README instead.

### Recommended user workflow

1. Open a **copy** of the model or a controlled test project.
2. Open the fire-fighting plan view; make the relevant categories visible.
3. Run **Audit Dimensions** — review, then run **Clean Redundant** if duplicates exist.
4. Run **Coverage Check** — dimension the reported gaps (Spacing Chain / Clearance help here).
5. Run **Tolerance Check** with project-adapted rules.
6. **Batch Audit Views** before issuing: audit every FF plan of the document in one pass and export the merged report.
7. **Linked-Model Audit** on federated views: resolve and fix cross-link dimensions, then dimension the reported linked fire-element gaps (host side or via the linked model's own views).
8. **BCF Export** the findings for the coordination meeting, **BCF Import** the resolved package afterwards and re-verify each topic with *Select in model*.
9. **Export Report** for the QA record; note tool version + Revit build + view name.

### Quality assurance and verification

**Verified for this release:**
- The source targets the Revit 2020 API surface (`Dimension`, `DimensionSegment`, `FilteredElementCollector`, `UnitFormatUtils`/`SpecTypeId`, `FamilyInstance.GetReferences`, `HostObjectUtils.GetSideFaces`, `Creation.Document.NewDimension`, `RevitLinkInstance.GetLinkDocument`, `Reference.LinkedElementId`, `View3D.GetOrientation`, `View.CropBox`) plus .NET Framework 4.8 platform APIs for BCF packaging (`System.IO.Compression.ZipArchive`, `System.Xml.Linq`) and compiles as C# 7.3 / net48 with the SDK-style project.

**Requires live verification in Revit 2020 (first run):**
- Ribbon tab/panel creation and button loading on a clean machine (now 12 buttons, including Linked-Model Audit, BCF Export/Import, Settings & Options and Batch Audit Views).
- Behaviour on real projects: family reference availability varies between sprinkler families; the fire-element heuristics (see below) may need tuning to project naming conventions.
- Full end-to-end runs of FFD-05/FFD-06 on representative sprinkler families.
- FFD-06 pipe path: dimensioning from the pipe location-curve/centreline reference to wall faces on real pipe runs (curved runs, vertical pipes and insulated pipes may need refinement).
- FFD-09 batch run across a multi-view document (performance on large projects, view list completeness).
- FFD-10: cross-link reference resolution and linked fire-element coverage on real federated models (link transforms, nested links and unloaded links included).
- FFD-11: the exported `.bcf` opens correctly in at least one third-party BCF platform (BIMcollab / Revizto / Solibri / Trimble Connect) and in FFD-12.
- FFD-12: component resolution against a model that was modified since export (expect unresolved references, not crashes).
- Arabic UI: layout and text direction of both dialogs on a machine with Arabic locale.

Report results (and screenshots) per release in this section — do not claim a check passed without evidence.

### Known limitations

- **Linked models:** dimensions referencing geometry in linked Revit files are fully analysed by FFD-10 (resolution, broken references, coverage, duplicates). What remains out of scope: dimensions *inside* the linked document itself, links nested more than one level deep, and creating dimensions on linked geometry.
- **BCF round-trip:** components are matched by decimal Revit element id (`AuthoringToolId`) — packages whose component ids come from IFC exports (IfcGuid only) import as topics without resolvable elements. Viewpoint snapshots are never written. Camera coordinates assume shared/project coordinates; heavily moved/rotated links may yield offset cameras.
- **Spot dimensions** (`SpotDimension`) are excluded from scans by default and only included when opted in via Settings & Options; radial/arc dimensions are always collected normally.
- **Fire-element detection** is heuristic: category + piping-system classification + connector system + name keywords (`sprinkler`, `hydrant`, `hose reel`, `fire`, `spk`). Unusual naming can cause false positives/negatives; results are spot-checkable in the UI.
- **Clearance for pipes** dimensions straight runs at the midpoint only; curved segments, vertical pipes and insulated runs are skipped or approximated. Point families and pipes are covered; other curve categories are not.
- **Batch Audit** runs the FFD-01 checks per view; coverage and tolerance checks remain per-view commands.
- **Arabic UI** covers window captions, headers, buttons and option labels; command tooltips and TaskDialog messages remain English in this release.
- **Tolerance Check semantics:** any dimension referencing a sprinkler is checked against sprinkler rules — a sprinkler-to-wall clearance dimension is also subject to a spacing rule if one is enabled. Disable rules you do not want applied.

### Troubleshooting

| Symptom | Recommended checks |
|---|---|
| FF Tools tab does not appear | `install.cmd` was run as the same Windows user that starts Revit; manifest at `%APPDATA%\Autodesk\Revit\Addins\2020\FFDimAddin2020.addin`; DLL at `...\Addins\2020\FFDimAddin2020\FFDimAddin2020.dll`. |
| Add-in load error at startup | Confirm the DLL was built for net48 x64 and Revit 2020 (not another version's addins folder). |
| "Not enough dimensionable elements" | The selected families expose no centre reference planes — use families with standard centre refs, or dimension manually. |
| Spacing Chain reports Revit rejection | The family's centre references are not dimension-compatible; try a different sprinkler family. |
| Coverage Check finds nothing | The view may hide the elements (visibility/Graphics), or detection heuristics missed the naming — check README limitations. |
| Tolerance Check flags everything | Scope rule matches more dimensions than expected (see limitations) — narrow the rule scope or disable it. |

### Security and model safety

- Only FFD-02, FFD-05 and FFD-06 modify the model; each runs in a named, undoable transaction.
- FFD-02 always asks for confirmation with exact counts before deleting.
- Test model-changing commands on a copy before using them on a live project.
- The tool never connects to the network and never reads/writes project files beyond the user-selected report path and the settings JSON in `%APPDATA%`.
- Do not commit credentials, private models or confidential client data to this repository.

---

## Repository layout

```text
.github/workflows/build.yml           CI build and smoke test for the CLI auditor
samples/sample-model.json            Example model data (CLI; includes deliberate issues)
samples/audit-rules.json             Example rule configuration (CLI)
src/BimTools.FFTDIM/Program.cs       CLI, data contracts, and audit engine
src/BimTools.FFTDIM/*.csproj         .NET 8 project (CLI)
FFDimAddin2020.sln                   Visual Studio solution for the Revit 2020 add-in
install.cmd                          Per-user installer for the Revit 2020 add-in
LICENSE                              MIT
README.md                            This file
FFDimAddin2020/                      Revit 2020 add-in source
├── FFDimAddin2020.csproj            SDK-style, net48, RevitAPI refs via $(RevitApiPath)
├── FFDimAddin2020.addin             Revit manifest (AddIn Type="Application")
├── App.cs                           Ribbon: FF Tools tab, 3 panels, 12 buttons
├── Localization.cs                  EN/AR string table (L.T), right-to-left support
├── CommandBase.cs                   Shared command plumbing + view guards
├── Commands/
│   ├── AuditDimensionsCommand.cs
│   ├── CleanRedundantCommand.cs
│   ├── CoverageCheckCommand.cs
│   ├── ToleranceCheckCommand.cs
│   ├── BatchAuditCommand.cs         FFD-09 multi-view audit
│   ├── LinkedModelAuditCommand.cs   FFD-10 linked-model deep audit
│   ├── SpacingDimensionCommand.cs
│   ├── ClearanceDimensionCommand.cs
│   ├── ExportReportCommand.cs
│   ├── BcfExportCommand.cs          FFD-11 BCF 2.1 export
│   ├── BcfImportCommand.cs          FFD-12 BCF 2.1 import
│   └── SettingsCommand.cs           FFD-08 options dialog (no document required)
├── Core/
│   ├── Models.cs                    DimensionIssue / FireElementInfo / AuditResult
│   ├── UnitHelper.cs                Feet ↔ m/mm + project-unit formatting
│   ├── FireFightingFilter.cs        Fire-element detection heuristics (+ whole-document scan)
│   ├── DimensionScanner.cs          Dimension collection + reference keys + link detection
│   ├── RedundancyFinder.cs          Duplicate reference-set grouping
│   ├── DimensionAuditor.cs          Audit / coverage / tolerance engines
│   ├── BatchAuditor.cs              Multi-view audit runner + auditable-view collector
│   ├── LinkedModelAuditor.cs        FFD-10 cross-link resolution, coverage, link-aware keys
│   ├── BcfExporter.cs               FFD-11 BCF 2.1 writer (markup + viewpoints, ZipArchive)
│   ├── BcfImporter.cs               FFD-12 BCF 2.1 reader (namespace-tolerant parsing)
│   ├── ToleranceRule.cs             Rule model + JSON store + defaults
│   ├── AppSettings.cs               Options model + %APPDATA% JSON store
│   └── ReportExporter.cs            CSV / HTML / JSON writers
└── UI/
    ├── AuditResultsWindow.cs        WPF results grid + filter/search/export (no XAML)
    ├── ToleranceSettingsWindow.cs
    ├── SettingsWindow.cs            FFD-08 options dialog (code-built WPF)
    ├── ViewPickerWindow.cs          FFD-09 view checklist (code-built WPF)
    └── BcfPreviewWindow.cs          FFD-12 BCF topic preview (code-built WPF)
```

---

## Verification

GitHub Actions builds the CLI project (`src/BimTools.FFTDIM/`) and runs the included sample audit on pushes and pull requests. The workflow checks that both reports are produced and that the expected element count and duplicate-ID finding appear in the CSV report. A successful CI run verifies compilation and this smoke test only; it does not verify integration with a live BIM application, and it does **not** build the Revit 2020 add-in (which requires the Revit API assemblies that are not available on Linux CI runners).

## Planned next capabilities

Done in v1.1 (Revit add-in track):

- ~~Multi-version install (2020–2024) with per-version `.addin` manifests~~ — `install.cmd <version>`
- ~~Spot-dimension support in the scanner~~ — opt-in via Settings & Options
- ~~Pipe clearance dimensioning (linear runs to walls)~~ — FFD-06 now covers pipes
- ~~Batch mode: run audits across selected views and merge reports~~ — FFD-09
- ~~Localized UI (Arabic) behind a language setting~~ — Settings & Options

Done in v1.2 (Revit add-in track):

- ~~Full analysis of dimensions that reference linked-model geometry (nested elements)~~ — FFD-10 (one level deep: nested links and dimensions inside the linked document remain out of scope)
- ~~BCF/issue round-trip for findings (Revit add-in track)~~ — FFD-11 export + FFD-12 import (CLI track round-trip remains open)

Still planned:

1. A native BIM host adapter to extract model elements and parameters (CLI track).
2. More advanced rule operators, severity configuration, exclusions, and rule profiles (CLI track).
3. Excel/CSV import and mapping templates (CLI track).
4. HTML dashboard and summary by discipline, level, system, and category (CLI track).
5. Regression tests and larger sample datasets (CLI track).
6. Optional geometry, spatial, and coordination checks once a host adapter and reliable geometry source are defined.
7. Deeper link scenarios: dimensions inside the linked document, links nested more than one level deep, link-transform-aware camera placement (Revit add-in track).
8. BCF round-trip for the CLI track; BCF snapshot capture; comment replies written back to Revit (Revit add-in track).

## Contributing

Open an issue describing the desired workflow, expected inputs/outputs, and a small sanitized example. Add regression coverage for each new rule or capability and report the exact build/test evidence in pull requests.

For Revit add-in changes specifically:
1. Open an issue describing the problem or improvement.
2. Create a feature branch; keep unrelated changes separate.
3. Follow the existing architecture: commands stay thin, logic lives in `Core/`, UI in `UI/`.
4. Build with `-c Release` and verify in Revit 2020 before opening a PR.
5. Summarize the change, compatibility impact and any known limitations.

### Pull request checklist (Revit add-in)
- [ ] Clear purpose and scope
- [ ] Revit 2020 API only (no 2021+ only APIs without conditional compilation)
- [ ] Model-changing commands run in named transactions with failure handling
- [ ] README updated for any user-facing option or default
- [ ] No binaries, credentials or unrelated files committed

## License

MIT — see [LICENSE](LICENSE). Third-party notice: the Revit 2020 add-in builds against `RevitAPI.dll` / `RevitAPIUI.dll`, which remain the property of Autodesk, Inc. and are not redistributed by this repository.
