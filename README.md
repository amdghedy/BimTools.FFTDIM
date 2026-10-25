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

### What FFTDIM does

FFTDIM adds a ribbon tab **FF Tools** to Revit with three panels of commands:

| Panel | Commands |
|---|---|
| **Dimension QA** | Audit Dimensions · Clean Redundant · Coverage Check · Tolerance Check |
| **Quick Dimension** | Spacing Chain · Clearance |
| **Output** | Export Report |

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
| Export Report | FF Tools › Output | Active view | No | CSV or HTML file |

### Supported environment

| Item | Value |
|---|---|
| Host application | Autodesk Revit 2020 (build 20.x) |
| Target framework | .NET Framework 4.8 |
| Language / toolchain | C# 7.3 · Visual Studio 2019/2022 or `dotnet build` (≥ .NET SDK 6) |
| Platform | Windows x64 |
| API references | `RevitAPI.dll`, `RevitAPIUI.dll` from the Revit 2020 install (not redistributed) |
| Install scope | Per-user (`%APPDATA%\Autodesk\Revit\Addins\2020`) |
| UI language | English |

Revit 2021–2024 load 2020-built `.NET Framework 4.8` add-ins **only if** they ship a matching `*.addin` manifest per version; FFTDIM is currently packaged for 2020 only. Multi-version support is on the roadmap.

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
install.cmd
```

The script copies the manifest and the built DLL into `%APPDATA%\Autodesk\Revit\Addins\2020\`.

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
  - *Zero-value dimensions* — value < 0.5 mm, typically a snapped-to-nothing reference (Error).
  - *Duplicate dimensions* — two dimensions sharing the exact same reference set (Warning; the one with the lower element id is treated as the keeper).
  - *Orphaned references* — a reference points at an element id that no longer resolves (Warning).
  - *Zero-length chain segments* — a multi-segment dimension contains a dead segment (Error).
- **Outputs:** results window with severity, type, element, formatted value and detail; double-click a row to select and zoom to the element; CSV/HTML export.
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
- **Selection:** one or more point-based fire-fighting families in a **plan view**.
- **Processing:** per element, finds the nearest grid line or wall **in the active view** (wall interior face preferred, exterior face fallback; grid centre line otherwise), then creates a dimension from the element centre to that target along the perpendicular direction. Elements within 50 mm of the target are skipped.
- **Outputs:** all created dimensions are selected and zoomed to; skipped elements are listed with reasons.
- **Failure handling:** one transaction for the whole batch; per-element failures are reported individually and do not abort the batch.

#### FFD-07 · Export Report
- **Purpose:** produce reviewable evidence of the audit state of a view.
- **Processing:** runs the same audit as FFD-01 and writes a timestamped file (`FFTDIM_Audit_yyyyMMdd_HHmm.csv|html`). CSV is UTF-8 with BOM (Excel-safe); HTML is a self-contained styled table.

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

### Recommended user workflow

1. Open a **copy** of the model or a controlled test project.
2. Open the fire-fighting plan view; make the relevant categories visible.
3. Run **Audit Dimensions** — review, then run **Clean Redundant** if duplicates exist.
4. Run **Coverage Check** — dimension the reported gaps (Spacing Chain / Clearance help here).
5. Run **Tolerance Check** with project-adapted rules.
6. **Export Report** for the QA record; note tool version + Revit build + view name.

### Quality assurance and verification

**Verified for this release:**
- The source targets the Revit 2020 API surface (`Dimension`, `DimensionSegment`, `FilteredElementCollector`, `UnitFormatUtils`/`SpecTypeId`, `FamilyInstance.GetReferences`, `HostObjectUtils.GetSideFaces`, `Creation.Document.NewDimension`) and compiles as C# 7.3 / net48 with the SDK-style project.

**Requires live verification in Revit 2020 (first run):**
- Ribbon tab/panel creation and button loading on a clean machine.
- Behaviour on real projects: family reference availability varies between sprinkler families; the fire-element heuristics (see below) may need tuning to project naming conventions.
- Full end-to-end runs of FFD-05/FFD-06 on representative sprinkler families.

Report results (and screenshots) per release in this section — do not claim a check passed without evidence.

### Known limitations

- **Linked models:** dimensions referencing geometry in linked Revit files are not analysed (their references resolve to the link instance, not the nested element).
- **Spot dimensions** (`SpotDimension`) and radial/arc dimensions are not collected.
- **Fire-element detection** is heuristic: category + piping-system classification + connector system + name keywords (`sprinkler`, `hydrant`, `hose reel`, `fire`, `spk`). Unusual naming can cause false positives/negatives; results are spot-checkable in the UI.
- **Clearance** does not yet dimension linear pipes to walls (point families only); pipe runs are still covered by Coverage Check and Tolerance Check.
- **One view at a time:** no batch view/project-wide mode yet.
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
├── App.cs                           Ribbon: FF Tools tab, 3 panels, 7 buttons
├── CommandBase.cs                   Shared command plumbing + view guards
├── Commands/
│   ├── AuditDimensionsCommand.cs
│   ├── CleanRedundantCommand.cs
│   ├── CoverageCheckCommand.cs
│   ├── ToleranceCheckCommand.cs
│   ├── SpacingDimensionCommand.cs
│   ├── ClearanceDimensionCommand.cs
│   └── ExportReportCommand.cs
├── Core/
│   ├── Models.cs                    DimensionIssue / FireElementInfo / AuditResult
│   ├── UnitHelper.cs                Feet ↔ m/mm + project-unit formatting
│   ├── FireFightingFilter.cs        Fire-element detection heuristics
│   ├── DimensionScanner.cs          Dimension collection + reference keys
│   ├── RedundancyFinder.cs          Duplicate reference-set grouping
│   ├── DimensionAuditor.cs          Audit / coverage / tolerance engines
│   ├── ToleranceRule.cs             Rule model + JSON store + defaults
│   └── ReportExporter.cs            CSV / HTML writers
└── UI/
    ├── AuditResultsWindow.cs        WPF results grid (code-built, no XAML)
    └── ToleranceSettingsWindow.cs
```

---

## Verification

GitHub Actions builds the CLI project (`src/BimTools.FFTDIM/`) and runs the included sample audit on pushes and pull requests. The workflow checks that both reports are produced and that the expected element count and duplicate-ID finding appear in the CSV report. A successful CI run verifies compilation and this smoke test only; it does not verify integration with a live BIM application, and it does **not** build the Revit 2020 add-in (which requires the Revit API assemblies that are not available on Linux CI runners).

## Planned next capabilities

These are potential next steps, not implemented features:

1. A native BIM host adapter to extract model elements and parameters (CLI track).
2. More advanced rule operators, severity configuration, exclusions, and rule profiles (CLI track).
3. Excel/CSV import and mapping templates (CLI track).
4. HTML dashboard and summary by discipline, level, system, and category (CLI track).
5. Regression tests and larger sample datasets (CLI track).
6. Optional geometry, spatial, and coordination checks once a host adapter and reliable geometry source are defined.
7. Multi-version build (2020–2024) with per-version `.addin` manifests (Revit add-in track).
8. Linked-model dimension analysis (Revit add-in track).
9. Spot-dimension support in the scanner (Revit add-in track).
10. Pipe clearance dimensioning (linear runs to walls) (Revit add-in track).
11. Batch mode: run audits across selected views and merge reports (Revit add-in track).
12. BCF/issue round-trip for findings (both tracks).
13. Localized UI (Arabic) behind a language setting (Revit add-in track).

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
