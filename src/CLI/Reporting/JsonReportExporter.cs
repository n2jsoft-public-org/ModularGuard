using System.Text.Json;
using System.Text.Json.Serialization;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Validation;

namespace n2jSoft.ModularGuard.CLI.Reporting;

[JsonSerializable(typeof(JsonReport))]
[JsonSerializable(typeof(ReportSummary))]
[JsonSerializable(typeof(ModuleReport))]
[JsonSerializable(typeof(ProjectReport))]
[JsonSerializable(typeof(ViolationReport))]
[JsonSerializable(typeof(List<ModuleReport>))]
[JsonSerializable(typeof(List<ProjectReport>))]
[JsonSerializable(typeof(List<ViolationReport>))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class JsonReportSerializerContext : JsonSerializerContext;

internal sealed class JsonReport
{
    public ReportSummary Summary { get; init; } = null!;
    public List<ModuleReport> Modules { get; init; } = null!;
    public List<ViolationReport> Violations { get; init; } = null!;
}

internal sealed class ReportSummary
{
    public int TotalModules { get; init; }
    public int TotalProjects { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public bool IsValid { get; init; }
}

internal sealed class ModuleReport
{
    public string ModuleName { get; init; } = string.Empty;
    public List<ProjectReport> Projects { get; init; } = null!;
}

internal sealed class ProjectReport
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public List<string> References { get; init; } = null!;
}

internal sealed class ViolationReport
{
    public string Severity { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string InvalidReference { get; init; } = string.Empty;
    public string RuleName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Suggestion { get; init; }
    public string? DocumentationUrl { get; init; }
    public string? FilePath { get; init; }
    public int? LineNumber { get; init; }
    public int? ColumnNumber { get; init; }
}

public sealed class JsonReportExporter : IReportExporter
{
    public string Export(IReadOnlyList<ModuleInfo> modules, ValidationResult validationResult)
    {
        var report = new JsonReport
        {
            Summary = new ReportSummary
            {
                TotalModules = modules.Select(m => m.ModuleName).Distinct().Count(),
                TotalProjects = modules.Count,
                ErrorCount = validationResult.ErrorCount,
                WarningCount = validationResult.WarningCount,
                IsValid = validationResult.IsValid
            },
            Modules = modules.GroupBy(m => m.ModuleName)
                .Select(g => new ModuleReport
                {
                    ModuleName = g.Key,
                    Projects = g.Select(m => new ProjectReport
                    {
                        Name = m.ProjectInfo.Name,
                        Type = m.Type,
                        FilePath = m.ProjectInfo.FilePath,
                        References = m.ProjectInfo.ProjectReferences.Select(r => r.Path).ToList()
                    }).ToList()
                }).ToList(),
            Violations = validationResult.Violations.Select(v => new ViolationReport
            {
                Severity = v.Severity.ToString(),
                ProjectName = v.ProjectName,
                InvalidReference = v.InvalidReference,
                RuleName = v.RuleName,
                Description = v.Description,
                Suggestion = v.Suggestion,
                DocumentationUrl = v.DocumentationUrl,
                FilePath = v.FilePath,
                LineNumber = v.LineNumber,
                ColumnNumber = v.ColumnNumber
            }).ToList()
        };

        return JsonSerializer.Serialize(report, JsonReportSerializerContext.Default.JsonReport);
    }
}