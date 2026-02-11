using System.Text;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Validation;

namespace n2jSoft.ModularGuard.CLI.Reporting;

/// <summary>
///     Exports validation results in CSV format for spreadsheet analysis
/// </summary>
public sealed class CsvReportExporter : IReportExporter
{
    public string Export(IReadOnlyList<ModuleInfo> modules, ValidationResult validationResult)
    {
        var sb = new StringBuilder();

        // CSV Header
        sb.AppendLine("Severity,Project,InvalidReference,RuleName,Description,FilePath,LineNumber,ColumnNumber,Suggestion,DocumentationUrl");

        // CSV Rows
        foreach (var violation in validationResult.Violations.OrderBy(v => v.Severity).ThenBy(v => v.ProjectName))
        {
            sb.AppendLine(
                $"{EscapeCsvField(violation.Severity.ToString())}," +
                $"{EscapeCsvField(violation.ProjectName)}," +
                $"{EscapeCsvField(violation.InvalidReference)}," +
                $"{EscapeCsvField(violation.RuleName)}," +
                $"{EscapeCsvField(violation.Description)}," +
                $"{EscapeCsvField(violation.FilePath ?? string.Empty)}," +
                $"{violation.LineNumber?.ToString() ?? string.Empty}," +
                $"{violation.ColumnNumber?.ToString() ?? string.Empty}," +
                $"{EscapeCsvField(violation.Suggestion ?? string.Empty)}," +
                $"{EscapeCsvField(violation.DocumentationUrl ?? string.Empty)}");
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        // If the field contains comma, quote, or newline, wrap it in quotes and escape internal quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}