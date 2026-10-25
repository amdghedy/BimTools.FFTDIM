using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Autodesk.Revit.DB;

namespace BimTools.FFTDIM.Core
{
    /// <summary>
    /// Writes an <see cref="AuditResult"/> to CSV (with UTF-8 BOM so Excel opens it
    /// correctly), to a self-contained HTML page, or to a machine-readable JSON file.
    /// Dispatched by file extension.
    /// </summary>
    public static class ReportExporter
    {
        public static string Write(string path, AuditResult result)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".html" || extension == ".htm")
            {
                File.WriteAllText(path, BuildHtml(result), new UTF8Encoding(true));
            }
            else if (extension == ".json")
            {
                File.WriteAllText(path, BuildJson(result), new UTF8Encoding(false));
            }
            else
            {
                File.WriteAllText(path, BuildCsv(result), new UTF8Encoding(true));
            }
            return path;
        }

        public static string BuildCsv(AuditResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Severity,Type,Element,Value,View,Detail");
            foreach (DimensionIssue issue in result.Issues)
            {
                builder.AppendLine(string.Join(",",
                    Escape(issue.Severity.ToString()),
                    Escape(issue.Type.ToString()),
                    Escape(ElementText(issue)),
                    Escape(issue.ValueText ?? "-"),
                    Escape(issue.ViewName ?? "-"),
                    Escape(issue.Detail ?? "-")));
            }
            return builder.ToString();
        }

        public static string BuildHtml(AuditResult result)
        {
            var rows = new StringBuilder();
            foreach (DimensionIssue issue in result.Issues)
            {
                string color = "#333333";
                if (issue.Severity == IssueSeverity.Error) color = "#B3261E";
                else if (issue.Severity == IssueSeverity.Warning) color = "#9A6700";

                rows.AppendLine(string.Format(
                    "<tr><td style=\"color:{0};font-weight:600;\">{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td></tr>",
                    color,
                    Encode(issue.Severity.ToString()),
                    Encode(issue.Type.ToString()),
                    Encode(ElementText(issue)),
                    Encode(issue.ValueText ?? "-"),
                    Encode(issue.Detail ?? "-")));
            }

            return string.Format(
@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<title>FFTDIM audit report — {0}</title>
<style>
  body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 32px; color: #1f2328; }}
  h1 {{ font-size: 20px; margin-bottom: 4px; }}
  p.meta {{ color: #57606a; font-size: 13px; margin-top: 0; }}
  table {{ border-collapse: collapse; width: 100%; font-size: 13px; }}
  th, td {{ text-align: left; padding: 6px 10px; border-bottom: 1px solid #d8dee4; vertical-align: top; }}
  th {{ background: #f6f8fa; }}
</style>
</head>
<body>
<h1>FFTDIM — Dimension audit report</h1>
<p class=""meta"">{1}<br />Document: {2} · View: {3} · Generated: {4:yyyy-MM-dd HH:mm} by BimTools.FFTDIM</p>
<table>
<tr><th>Severity</th><th>Type</th><th>Element</th><th>Value</th><th>Detail</th></tr>
{5}
</table>
</body>
</html>",
                Encode(result.ViewName ?? "-"),
                Encode(result.SummaryText),
                Encode(result.DocumentTitle ?? "-"),
                Encode(result.ViewName ?? "-"),
                result.Timestamp,
                rows.ToString());
        }

        /// <summary>
        /// Builds a machine-readable JSON report. Written by hand (no serializer
        /// dependencies) so the shape stays stable and friendly to Power BI / Excel Power
        /// Query / CI scripts. Strings are JSON-escaped including control characters.
        /// </summary>
        public static string BuildJson(AuditResult result)
        {
            var builder = new StringBuilder();
            builder.Append("{\n  \"document\": ").Append(JsonString(result.DocumentTitle ?? "-"));
            builder.Append(",\n  \"view\": ").Append(JsonString(result.ViewName ?? "-"));
            builder.Append(",\n  \"generated\": \"").Append(result.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")).Append('"');
            builder.Append(",\n  \"tool\": \"BimTools.FFTDIM\"");
            builder.Append(",\n  \"totalDimensions\": ").Append(result.TotalDimensions);
            builder.Append(",\n  \"totalFireElements\": ").Append(result.TotalFireElements);
            builder.Append(",\n  \"coveredFireElements\": ").Append(result.CoveredFireElements);
            builder.Append(",\n  \"errors\": ").Append(result.CountBySeverity(IssueSeverity.Error));
            builder.Append(",\n  \"warnings\": ").Append(result.CountBySeverity(IssueSeverity.Warning));
            builder.Append(",\n  \"info\": ").Append(result.CountBySeverity(IssueSeverity.Info));
            builder.Append(",\n  \"issues\": [\n");

            bool first = true;
            foreach (DimensionIssue issue in result.Issues)
            {
                if (!first) builder.Append(",\n");
                first = false;
                builder.Append("    { \"severity\": ").Append(JsonString(issue.Severity.ToString()));
                builder.Append(", \"type\": ").Append(JsonString(issue.Type.ToString()));
                builder.Append(", \"element\": ").Append(JsonString(ElementText(issue)));
                builder.Append(", \"elementId\": ").Append(
                    issue.ElementId != null && issue.ElementId != ElementId.InvalidElementId
                        ? issue.ElementId.IntegerValue.ToString()
                        : "null");
                builder.Append(", \"value\": ").Append(JsonString(issue.ValueText ?? "-"));
                builder.Append(", \"view\": ").Append(JsonString(issue.ViewName ?? "-"));
                builder.Append(", \"detail\": ").Append(JsonString(issue.Detail ?? "-"));
                builder.Append(" }");
            }

            builder.Append("\n  ]\n}");
            return builder.ToString();
        }

        private static string JsonString(string value)
        {
            var escaped = new StringBuilder();
            escaped.Append('"');
            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': escaped.Append("\\\""); break;
                    case '\\': escaped.Append("\\\\"); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\t': escaped.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            escaped.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            escaped.Append(c);
                        }
                        break;
                }
            }
            escaped.Append('"');
            return escaped.ToString();
        }

        private static string ElementText(DimensionIssue issue)
        {
            string label = issue.ElementLabel ?? "-";
            if (issue.ElementId != null && issue.ElementId != ElementId.InvalidElementId)
            {
                label += " (id " + issue.ElementId.IntegerValue + ")";
            }
            return label;
        }

        private static string Escape(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private static string Encode(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
