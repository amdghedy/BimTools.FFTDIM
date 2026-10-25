# BimTools.FFTDIM

**FFTDIM starter implementation: a configurable BIM data quality auditor.** This first implementation establishes a runnable, testable foundation while the intended meaning and full scope of FFTDIM are confirmed. It is a standalone .NET 8 command-line tool that audits neutral JSON element data; it does **not** connect directly to Revit or another BIM authoring application's API yet.

[![Build and smoke test](https://github.com/amdghedy/BimTools.FFTDIM/actions/workflows/build.yml/badge.svg)](https://github.com/amdghedy/BimTools.FFTDIM/actions/workflows/build.yml)

## What works in this first implementation

- Read model element records from JSON.
- Count elements by category.
- Detect missing element IDs and categories.
- Detect duplicate IDs or other configured unique fields.
- Check required parameters by category, including wildcard rules.
- Validate numeric parameter ranges and flag non-numeric values.
- Export detailed findings to JSON and CSV.
- Return useful process exit codes for automation and CI.
- Run a GitHub Actions build and sample-data smoke test.

This is an initial foundation—not a claim that model geometry, native authoring API integration, clash detection, or direct model editing is implemented.

## Quick start

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

Clone the repository and run the included sample:

```bash
git clone https://github.com/amdghedy/BimTools.FFTDIM.git
cd BimTools.FFTDIM

dotnet build src/BimTools.FFTDIM/BimTools.FFTDIM.csproj --configuration Release

dotnet run --project src/BimTools.FFTDIM -- audit samples/sample-model.json --rules samples/audit-rules.json --out artifacts/report.json --csv artifacts/report.csv
```

The sample intentionally contains issues so that you can see findings in the report. The command returns exit code `1` if error-severity findings exist, `0` if the audit completes without errors, and `2` for invalid arguments, malformed input, or I/O failures. Warnings alone do not change the exit code.

## Input format

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

## Configurable audit rules

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

## Command options

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

## Finding reference

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

## Repository layout

```text
.github/workflows/build.yml       CI build and smoke test
samples/sample-model.json         Example model data (includes deliberate issues)
samples/audit-rules.json          Example rule configuration
src/BimTools.FFTDIM/Program.cs    CLI, data contracts, and audit engine
src/BimTools.FFTDIM/*.csproj      .NET 8 project
```

## Verification

GitHub Actions builds the project and runs the included sample audit on pushes and pull requests. The workflow checks that both reports are produced and that the expected element count and duplicate-ID finding appear in the CSV report. A successful CI run verifies compilation and this smoke test only; it does not verify integration with a live BIM application.

## Planned next capabilities

These are potential next steps, not implemented features:

1. A native BIM host adapter to extract model elements and parameters.
2. More advanced rule operators, severity configuration, exclusions, and rule profiles.
3. Excel/CSV import and mapping templates.
4. HTML dashboard and summary by discipline, level, system, and category.
5. Regression tests and larger sample datasets.
6. Optional geometry, spatial, and coordination checks once a host adapter and reliable geometry source are defined.

## Safety and limitations

- This starter reads JSON and writes reports; it does not modify a model.
- Validate rules and test with representative data before using audit results for contractual or compliance decisions.
- Numeric values are compared as supplied; the tool does not perform unit conversion. Configure consistent units in the input data.
- A generic JSON export may omit model-specific context. Findings are only as reliable as the input data and mapping.
- No native BIM integration has been verified in this initial version.

## Contributing

Open an issue describing the desired workflow, expected inputs/outputs, and a small sanitized example. Add regression coverage for each new rule or capability and report the exact build/test evidence in pull requests.

## License

A license has not yet been selected or published. Until one is added, do not assume that this repository is licensed for redistribution or commercial reuse.
