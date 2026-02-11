using System.Text;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Validation;

namespace n2jSoft.ModularGuard.CLI.Reporting;

public sealed class MarkdownReportExporter : IReportExporter
{
    public string Export(IReadOnlyList<ModuleInfo> modules, ValidationResult validationResult)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Modular Monolith Validation Report");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total Modules**: {modules.Select(m => m.ModuleName).Distinct().Count()}");
        sb.AppendLine($"- **Total Projects**: {modules.Count}");
        sb.AppendLine($"- **Errors**: {validationResult.ErrorCount}");
        sb.AppendLine($"- **Warnings**: {validationResult.WarningCount}");
        sb.AppendLine($"- **Status**: {(validationResult.IsValid ? "✅ Valid" : "❌ Invalid")}");
        sb.AppendLine();

        // Violations
        if (validationResult.Violations.Count > 0)
        {
            sb.AppendLine("## Violations");
            sb.AppendLine();

            foreach (var violation in validationResult.Violations.OrderBy(v => v.Severity))
            {
                var severity = violation.Severity switch
                {
                    ViolationSeverity.Error => "🔴 ERROR",
                    ViolationSeverity.Warning => "🟡 WARNING",
                    ViolationSeverity.Info => "🔵 INFO",
                    _ => violation.Severity.ToString()
                };

                sb.AppendLine($"### {severity}: {violation.RuleName}");
                sb.AppendLine();
                sb.AppendLine($"- **Project**: {violation.ProjectName}");
                sb.AppendLine($"- **Invalid Reference**: {violation.InvalidReference}");
                sb.AppendLine($"- **Description**: {violation.Description}");

                if (!string.IsNullOrEmpty(violation.Suggestion))
                {
                    sb.AppendLine($"- **Suggestion**: {violation.Suggestion}");
                }

                if (!string.IsNullOrEmpty(violation.DocumentationUrl))
                {
                    sb.AppendLine($"- **Documentation**: [{violation.DocumentationUrl}]({violation.DocumentationUrl})");
                }

                sb.AppendLine();
            }
        }

        // Modules
        sb.AppendLine("## Modules");
        sb.AppendLine();

        var groupedModules = modules.GroupBy(m => m.ModuleName).OrderBy(g => g.Key);

        foreach (var moduleGroup in groupedModules)
        {
            sb.AppendLine($"### {moduleGroup.Key}");
            sb.AppendLine();
            sb.AppendLine("| Project Type | Project Name | References |");
            sb.AppendLine("|--------------|--------------|------------|");

            foreach (var module in moduleGroup.OrderBy(m => m.Type))
            {
                sb.AppendLine(
                    $"| {module.Type} | {module.ProjectInfo.Name} | {module.ProjectInfo.ProjectReferences.Count} |");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}