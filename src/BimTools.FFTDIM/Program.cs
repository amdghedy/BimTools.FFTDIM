using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BimTools.FFTDIM;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        if (!string.Equals(args[0], "audit", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unknown command '{args[0]}'. Expected 'audit'.");
            PrintUsage();
            return 2;
        }

        var options = ParseOptions(args.Skip(1).ToArray());
        if (options.InputPath is null)
        {
            Console.Error.WriteLine("Missing input JSON path.");
            PrintUsage();
            return 2;
        }

        try
        {
            var inputPath = Path.GetFullPath(options.InputPath);
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Input JSON file was not found.", inputPath);

            var inputText = await File.ReadAllTextAsync(inputPath, Encoding.UTF8);
            var model = JsonSerializer.Deserialize<ModelData>(inputText, JsonOptions)
                ?? throw new InvalidDataException("Input JSON was empty or did not contain a model object.");
            if (model.Elements is null)
                throw new InvalidDataException("Input JSON must contain an 'elements' array.");

            var rules = options.RulesPath is null
                ? AuditRules.Default
                : await LoadRulesAsync(Path.GetFullPath(options.RulesPath));

            var report = Auditor.Run(model, rules);
            var json = JsonSerializer.Serialize(report, JsonOptions);
            var jsonPath = Path.GetFullPath(options.JsonOutputPath ?? "fftdim-audit-report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
            await File.WriteAllTextAsync(jsonPath, json + Environment.NewLine, new UTF8Encoding(false));

            string? csvPath = null;
            if (options.CsvOutputPath is not null)
            {
                csvPath = Path.GetFullPath(options.CsvOutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);
                await WriteCsvAsync(csvPath, report);
            }

            Console.WriteLine($"Model: {model.ModelName ?? Path.GetFileName(inputPath)}");
            Console.WriteLine($"Elements scanned: {report.Summary.TotalElements}");
            Console.WriteLine($"Findings: {report.Summary.TotalFindings} (errors: {report.Summary.Errors}, warnings: {report.Summary.Warnings}, info: {report.Summary.Infos})");
            Console.WriteLine($"JSON report: {jsonPath}");
            if (csvPath is not null) Console.WriteLine($"CSV report:  {csvPath}");
            return report.Summary.Errors > 0 ? 1 : 0;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Invalid JSON: {ex.Message}");
            return 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            Console.Error.WriteLine($"Audit failed: {ex.Message}");
            return 2;
        }
    }

    private static async Task<AuditRules> LoadRulesAsync(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Rules JSON file was not found.", path);
        var text = await File.ReadAllTextAsync(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<AuditRules>(text, JsonOptions)
            ?? throw new InvalidDataException("Rules JSON was empty or invalid.");
    }

    private static CliOptions ParseOptions(string[] args)
    {
        var result = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (i + 1 >= args.Length) throw new ArgumentException($"Option '{arg}' requires a value.");
                var value = args[++i];
                switch (arg)
                {
                    case "--rules": result.RulesPath = value; break;
                    case "--out": result.JsonOutputPath = value; break;
                    case "--csv": result.CsvOutputPath = value; break;
                    default: throw new ArgumentException($"Unknown option '{arg}'.");
                }
            }
            else if (result.InputPath is null) result.InputPath = arg;
            else throw new ArgumentException($"Unexpected argument '{arg}'.");
        }
        return result;
    }

    private static async Task WriteCsvAsync(string path, AuditReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("severity,ruleCode,elementId,category,message");
        foreach (var finding in report.Findings)
        {
            sb.Append(Csv(finding.Severity)).Append(',')
              .Append(Csv(finding.RuleCode)).Append(',')
              .Append(Csv(finding.ElementId)).Append(',')
              .Append(Csv(finding.Category)).Append(',')
              .Append(Csv(finding.Message)).AppendLine();
        }
        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

    private static void PrintUsage()
    {
        Console.WriteLine("""
BimTools.FFTDIM — BIM data quality audit starter

Usage:
  dotnet run --project src/BimTools.FFTDIM -- audit <model.json> [--rules rules.json] [--out report.json] [--csv report.csv]

Exit codes:
  0  Audit completed without error-severity findings
  1  Audit completed and found one or more errors
  2  Invalid arguments, invalid input, or I/O failure

The input is a neutral JSON exchange format; this starter does not connect directly to a BIM authoring API.
""");
    }

    private sealed class CliOptions
    {
        public string? InputPath { get; set; }
        public string? RulesPath { get; set; }
        public string? JsonOutputPath { get; set; }
        public string? CsvOutputPath { get; set; }
    }
}

internal sealed class ModelData
{
    public string? ModelName { get; set; }
    public List<ModelElement>? Elements { get; set; }
}

internal sealed class ModelElement
{
    public string? Id { get; set; }
    public string? Category { get; set; }
    public Dictionary<string, JsonElement>? Parameters { get; set; }
}

internal sealed class AuditRules
{
    public Dictionary<string, List<string>> RequiredParametersByCategory { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> UniqueIdFields { get; set; } = ["id"];
    public Dictionary<string, NumericRange> NumericRanges { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static AuditRules Default => new()
    {
        RequiredParametersByCategory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["*"] = ["Mark"]
        },
        UniqueIdFields = ["id"],
        NumericRanges = new Dictionary<string, NumericRange>(StringComparer.OrdinalIgnoreCase)
    };
}

internal sealed class NumericRange
{
    public double? Min { get; set; }
    public double? Max { get; set; }
}

internal sealed class AuditFinding
{
    public string Severity { get; init; } = "Warning";
    public string RuleCode { get; init; } = "";
    public string? ElementId { get; init; }
    public string? Category { get; init; }
    public string Message { get; init; } = "";
}

internal sealed class AuditSummary
{
    public int TotalElements { get; init; }
    public int TotalFindings { get; init; }
    public int Errors { get; init; }
    public int Warnings { get; init; }
    public int Infos { get; init; }
    public Dictionary<string, int> ElementsByCategory { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class AuditReport
{
    public string Tool { get; init; } = "BimTools.FFTDIM";
    public string ReportVersion { get; init; } = "1.0";
    public DateTimeOffset GeneratedUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? ModelName { get; init; }
    public AuditSummary Summary { get; init; } = new();
    public List<AuditFinding> Findings { get; init; } = [];
}

internal static class Auditor
{
    public static AuditReport Run(ModelData model, AuditRules rules)
    {
        var elements = model.Elements ?? throw new InvalidDataException("The elements array is required.");
        var findings = new List<AuditFinding>();
        var categories = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in elements)
        {
            var category = string.IsNullOrWhiteSpace(element.Category) ? "(uncategorized)" : element.Category.Trim();
            categories[category] = categories.GetValueOrDefault(category) + 1;

            if (string.IsNullOrWhiteSpace(element.Id))
                findings.Add(Find("Error", "FTD001", element, "Element ID is missing."));
            if (string.IsNullOrWhiteSpace(element.Category))
                findings.Add(Find("Warning", "FTD002", element, "Element category is missing."));
        }

        foreach (var field in rules.UniqueIdFields ?? [])
        {
            var duplicates = elements
                .Select(e => new { Element = e, Value = GetValue(e, field) })
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .GroupBy(x => x.Value!, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (var duplicateGroup in duplicates)
            foreach (var item in duplicateGroup)
                findings.Add(Find("Error", "FTD003", item.Element,
                    $"Value '{duplicateGroup.Key}' for unique field '{field}' occurs {duplicateGroup.Count()} times."));
        }

        foreach (var element in elements)
        {
            var category = element.Category ?? "";
            var required = rules.RequiredParametersByCategory
                .Where(pair => pair.Key == "*" || string.Equals(pair.Key, category, StringComparison.OrdinalIgnoreCase))
                .SelectMany(pair => pair.Value ?? [])
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var parameter in required)
            {
                if (!TryGetParameter(element, parameter, out var value) || IsBlank(value))
                    findings.Add(Find("Warning", "FTD004", element, $"Required parameter '{parameter}' is missing or empty."));
            }

            foreach (var rangeRule in rules.NumericRanges)
            {
                if (!TryGetParameter(element, rangeRule.Key, out var value) || IsBlank(value)) continue;
                if (!TryGetDouble(value, out var number))
                {
                    findings.Add(Find("Warning", "FTD005", element, $"Parameter '{rangeRule.Key}' is not numeric."));
                    continue;
                }
                if (rangeRule.Value.Min is double min && number < min)
                    findings.Add(Find("Warning", "FTD006", element, $"Parameter '{rangeRule.Key}' value {number.ToString(CultureInfo.InvariantCulture)} is below minimum {min.ToString(CultureInfo.InvariantCulture)}."));
                if (rangeRule.Value.Max is double max && number > max)
                    findings.Add(Find("Warning", "FTD007", element, $"Parameter '{rangeRule.Key}' value {number.ToString(CultureInfo.InvariantCulture)} exceeds maximum {max.ToString(CultureInfo.InvariantCulture)}."));
            }
        }

        return new AuditReport
        {
            ModelName = model.ModelName,
            Findings = findings,
            Summary = new AuditSummary
            {
                TotalElements = elements.Count,
                TotalFindings = findings.Count,
                Errors = findings.Count(f => f.Severity == "Error"),
                Warnings = findings.Count(f => f.Severity == "Warning"),
                Infos = findings.Count(f => f.Severity == "Info"),
                ElementsByCategory = categories
            }
        };
    }

    private static AuditFinding Find(string severity, string code, ModelElement element, string message) =>
        new() { Severity = severity, RuleCode = code, ElementId = element.Id, Category = element.Category, Message = message };

    private static string? GetValue(ModelElement element, string field)
    {
        if (string.Equals(field, "id", StringComparison.OrdinalIgnoreCase)) return element.Id;
        if (string.Equals(field, "category", StringComparison.OrdinalIgnoreCase)) return element.Category;
        return TryGetParameter(element, field, out var value) ? ScalarText(value) : null;
    }

    private static bool TryGetParameter(ModelElement element, string key, out JsonElement value)
    {
        if (element.Parameters is not null)
        {
            foreach (var pair in element.Parameters)
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
        }
        value = default;
        return false;
    }

    private static bool IsBlank(JsonElement value) =>
        value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
        (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()));

    private static string? ScalarText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : value.GetRawText()
    };

    private static bool TryGetDouble(JsonElement value, out double number)
    {
        if (value.ValueKind == JsonValueKind.Number) return value.TryGetDouble(out number);
        if (value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            return true;
        number = 0;
        return false;
    }
}
