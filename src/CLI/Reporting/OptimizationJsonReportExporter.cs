using System.Text.Json;
using System.Text.Json.Serialization;
using n2jSoft.ModularGuard.CLI.Models;

namespace n2jSoft.ModularGuard.CLI.Reporting;

[JsonSerializable(typeof(OptimizationReport))]
[JsonSerializable(typeof(OptimizationSummary))]
[JsonSerializable(typeof(OptimizationProjectResult))]
[JsonSerializable(typeof(UnnecessaryReferenceInfo))]
[JsonSerializable(typeof(List<OptimizationProjectResult>))]
[JsonSerializable(typeof(List<UnnecessaryReferenceInfo>))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OptimizationReportSerializerContext : JsonSerializerContext;

internal sealed class OptimizationReport
{
    public DateTime Timestamp { get; init; }
    public OptimizationSummary Summary { get; init; } = null!;
    public List<OptimizationProjectResult> Results { get; init; } = null!;
}

internal sealed class OptimizationSummary
{
    public int TotalProjects { get; init; }
    public int TotalUnnecessaryReferences { get; init; }
    public int UnusedReferences { get; init; }
    public int TransitiveReferences { get; init; }
}

internal sealed class OptimizationProjectResult
{
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectPath { get; init; } = string.Empty;
    public List<UnnecessaryReferenceInfo> UnnecessaryReferences { get; init; } = null!;
}

internal sealed class UnnecessaryReferenceInfo
{
    public string ReferenceName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public List<string>? TransitivePath { get; init; }
}

public sealed class OptimizationJsonReportExporter
{
    public string Export(IReadOnlyList<OptimizationResult> results)
    {
        var report = new OptimizationReport
        {
            Timestamp = DateTime.UtcNow,
            Summary = new OptimizationSummary
            {
                TotalProjects = results.Count,
                TotalUnnecessaryReferences = results.Sum(r => r.UnnecessaryReferences.Count),
                UnusedReferences = results.Sum(r =>
                    r.UnnecessaryReferences.Count(u => u.Reason == UnnecessaryReferenceReason.Unused)),
                TransitiveReferences = results.Sum(r =>
                    r.UnnecessaryReferences.Count(u => u.Reason == UnnecessaryReferenceReason.Transitive))
            },
            Results = results.Select(r => new OptimizationProjectResult
            {
                ProjectName = r.ProjectName,
                ProjectPath = r.ProjectPath,
                UnnecessaryReferences = r.UnnecessaryReferences.Select(u => new UnnecessaryReferenceInfo
                {
                    ReferenceName = u.ReferenceName,
                    Reason = u.Reason.ToString().ToLowerInvariant(),
                    TransitivePath = u.TransitivePath != null ? [u.TransitivePath] : null
                }).ToList()
            }).ToList()
        };

        return JsonSerializer.Serialize(report,
            OptimizationReportSerializerContext.Default.OptimizationReport);
    }
}