using System.Text.Json;
using System.Text.Json.Serialization;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Validation;

namespace n2jSoft.ModularGuard.CLI.Reporting;

[JsonSerializable(typeof(SarifLog))]
[JsonSerializable(typeof(SarifRun))]
[JsonSerializable(typeof(SarifTool))]
[JsonSerializable(typeof(SarifToolDriver))]
[JsonSerializable(typeof(SarifRule))]
[JsonSerializable(typeof(SarifRuleConfiguration))]
[JsonSerializable(typeof(SarifResult))]
[JsonSerializable(typeof(SarifMessage))]
[JsonSerializable(typeof(SarifLocation))]
[JsonSerializable(typeof(SarifPhysicalLocation))]
[JsonSerializable(typeof(SarifArtifactLocation))]
[JsonSerializable(typeof(SarifRegion))]
[JsonSerializable(typeof(SarifLogicalLocation))]
[JsonSerializable(typeof(List<SarifRun>))]
[JsonSerializable(typeof(List<SarifRule>))]
[JsonSerializable(typeof(List<SarifResult>))]
[JsonSerializable(typeof(List<SarifLocation>))]
[JsonSerializable(typeof(List<SarifLogicalLocation>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class SarifSerializerContext : JsonSerializerContext;

internal sealed class SarifLog
{
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("$schema")] public string Schema { get; init; } = string.Empty;

    public List<SarifRun> Runs { get; init; } = null!;
}

internal sealed class SarifRun
{
    public SarifTool Tool { get; init; } = null!;
    public List<SarifResult> Results { get; init; } = null!;
}

internal sealed class SarifTool
{
    public SarifToolDriver Driver { get; init; } = null!;
}

internal sealed class SarifToolDriver
{
    public string Name { get; init; } = string.Empty;
    public string InformationUri { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public List<SarifRule> Rules { get; init; } = null!;
}

internal sealed class SarifRule
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public SarifMessage ShortDescription { get; init; } = null!;
    public SarifMessage FullDescription { get; init; } = null!;
    public string? HelpUri { get; init; }
    public SarifRuleConfiguration DefaultConfiguration { get; init; } = null!;
}

internal sealed class SarifRuleConfiguration
{
    public string Level { get; init; } = string.Empty;
}

internal sealed class SarifResult
{
    public string RuleId { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public SarifMessage Message { get; init; } = null!;
    public List<SarifLocation> Locations { get; init; } = null!;
    public Dictionary<string, string>? Properties { get; init; }
}

internal sealed class SarifMessage
{
    public string Text { get; init; } = string.Empty;
}

internal sealed class SarifLocation
{
    public SarifPhysicalLocation PhysicalLocation { get; init; } = null!;
    public List<SarifLogicalLocation> LogicalLocations { get; init; } = null!;
}

internal sealed class SarifPhysicalLocation
{
    public SarifArtifactLocation ArtifactLocation { get; init; } = null!;
    public SarifRegion? Region { get; init; }
}

internal sealed class SarifArtifactLocation
{
    public string Uri { get; init; } = string.Empty;
}

internal sealed class SarifRegion
{
    public int? StartLine { get; init; }
    public int? StartColumn { get; init; }
}

internal sealed class SarifLogicalLocation
{
    public string Name { get; init; } = string.Empty;
    public string FullyQualifiedName { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
}

/// <summary>
///     Exports validation results in SARIF (Static Analysis Results Interchange Format) 2.1.0
///     This format is supported by many IDEs and CI/CD systems including GitHub, Azure DevOps, and Visual Studio
/// </summary>
public sealed class SarifReportExporter : IReportExporter
{
    public string Export(IReadOnlyList<ModuleInfo> modules, ValidationResult validationResult)
    {
        var sarifLog = new SarifLog
        {
            Version = "2.1.0",
            Schema = "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json",
            Runs = new List<SarifRun>
            {
                new()
                {
                    Tool = new SarifTool
                    {
                        Driver = new SarifToolDriver
                        {
                            Name = "Modular Monolith Linter",
                            InformationUri = "https://github.com/n2jsoft/modularguard",
                            Version = "1.0.0",
                            Rules = CreateRules(validationResult)
                        }
                    },
                    Results = CreateResults(validationResult)
                }
            }
        };

        return JsonSerializer.Serialize(sarifLog, SarifSerializerContext.Default.SarifLog);
    }


    private static List<SarifRule> CreateRules(ValidationResult validationResult)
    {
        var uniqueRules = validationResult.Violations
            .GroupBy(v => v.RuleName)
            .Select(g => g.First())
            .Select(v => new SarifRule
            {
                Id = v.RuleName,
                Name = v.RuleName,
                ShortDescription = new SarifMessage
                {
                    Text = v.Description
                },
                FullDescription = new SarifMessage
                {
                    Text = v.Description
                },
                HelpUri = v.DocumentationUrl,
                DefaultConfiguration = new SarifRuleConfiguration
                {
                    Level = v.Severity switch
                    {
                        ViolationSeverity.Error => "error",
                        ViolationSeverity.Warning => "warning",
                        ViolationSeverity.Info => "note",
                        _ => "warning"
                    }
                }
            })
            .ToList();

        return uniqueRules;
    }

    private static List<SarifResult> CreateResults(ValidationResult validationResult)
    {
        return validationResult.Violations.Select(v => new SarifResult
        {
            RuleId = v.RuleName,
            Level = v.Severity switch
            {
                ViolationSeverity.Error => "error",
                ViolationSeverity.Warning => "warning",
                ViolationSeverity.Info => "note",
                _ => "warning"
            },
            Message = new SarifMessage
            {
                Text = v.Description
            },
            Locations = new List<SarifLocation>
            {
                new()
                {
                    PhysicalLocation = new SarifPhysicalLocation
                    {
                        ArtifactLocation = new SarifArtifactLocation
                        {
                            Uri = v.FilePath ?? (v.ProjectName + ".csproj")
                        },
                        Region = v.LineNumber.HasValue ? new SarifRegion
                        {
                            StartLine = v.LineNumber,
                            StartColumn = v.ColumnNumber
                        } : null
                    },
                    LogicalLocations = new List<SarifLogicalLocation>
                    {
                        new()
                        {
                            Name = v.ProjectName,
                            FullyQualifiedName = v.ProjectName,
                            Kind = "project"
                        }
                    }
                }
            },
            Properties = CreateResultProperties(v)
        }).ToList();
    }

    private static Dictionary<string, string>? CreateResultProperties(Violation violation)
    {
        var properties = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(violation.InvalidReference))
        {
            properties["invalidReference"] = violation.InvalidReference;
        }

        if (!string.IsNullOrEmpty(violation.Suggestion))
        {
            properties["suggestion"] = violation.Suggestion;
        }

        return properties.Count > 0 ? properties : null;
    }

    // SARIF 2.1.0 Schema Classes
}