using System.Text.RegularExpressions;
using n2jSoft.ModularGuard.CLI.Configuration;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Reporting;
using n2jSoft.ModularGuard.CLI.Services;
using n2jSoft.ModularGuard.CLI.Validation;
using Spectre.Console;
using Spectre.Console.Cli;
using ValidationResult = n2jSoft.ModularGuard.CLI.Validation.ValidationResult;

namespace n2jSoft.ModularGuard.CLI.Commands;

public sealed class CheckCommand : Command<CheckCommand.Settings>
{
    public enum OutputFormat
    {
        Console,
        Json,
        Markdown,
        Sarif,
        Csv
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var absolutePath = Path.GetFullPath(settings.Path);

        if (!Directory.Exists(absolutePath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Directory not found: {0}", absolutePath);
            return 1;
        }

        // Load configuration
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(absolutePath, settings.Profile);
        var hasConfigFile = configuration != null;
        configuration ??= configLoader.CreateDefaultConfiguration();

        var shouldShowConsoleOutput = !settings.Quiet && (settings.Format == OutputFormat.Console || settings.Verbose);

        if (shouldShowConsoleOutput)
        {
            var configSource = hasConfigFile
                ? "Found configuration file"
                : "Using default configuration";

            if (!string.IsNullOrWhiteSpace(settings.Profile))
            {
                configSource += $" (profile: {settings.Profile})";
            }

            AnsiConsole.MarkupLine("[dim]{0}[/]", configSource);
            AnsiConsole.WriteLine();

            if (!settings.NoHeader)
            {
                DisplayConfigurationSummary(configuration, settings.Verbose);
                AnsiConsole.WriteLine();
            }
        }

        if (shouldShowConsoleOutput)
        {
            AnsiConsole.MarkupLine("[blue]Scanning for projects in:[/] {0}", absolutePath);
            AnsiConsole.WriteLine();
        }

        var discoveryService = new ProjectDiscoveryService();
        var projectPaths = discoveryService.DiscoverProjects(absolutePath).ToList();

        if (projectPaths.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No .csproj files found.[/]");
            return 0;
        }

        if (shouldShowConsoleOutput)
        {
            AnsiConsole.MarkupLine("[green]Found {0} project(s)[/]", projectPaths.Count);

            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine("[dim]Discovered projects:[/]");
                foreach (var projectPath in projectPaths)
                {
                    AnsiConsole.MarkupLine("[dim]  - {0}[/]", projectPath);
                }
            }

            AnsiConsole.WriteLine();
        }

        var modules = new List<ModuleInfo>();
        var typeDetector = new ConfigurableProjectTypeDetector(configuration);

        using (var loaderService = new ProjectLoaderService())
        {
            if (settings.Quiet)
            {
                // Load projects silently
                foreach (var projectPath in projectPaths)
                {
                    try
                    {
                        var projectInfo = loaderService.LoadProject(projectPath);

                        // Skip ignored projects
                        if (IsProjectIgnored(projectInfo.Name, configuration.IgnoredProjects))
                        {
                            continue;
                        }

                        var projectType = typeDetector.DetectProjectType(projectInfo.Name, projectPath, absolutePath);
                        var moduleName = typeDetector.ExtractModuleName(projectInfo.Name, projectType);

                        modules.Add(new ModuleInfo(moduleName, projectType, projectInfo));
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine("[red]Failed to load {0}:[/] {1}",
                            Path.GetFileName(projectPath),
                            ex.Message);
                    }
                }
            }
            else
            {
                // Load projects with status indicator
                AnsiConsole.Status()
                    .Start("Loading projects...", ctx =>
                    {
                        foreach (var projectPath in projectPaths)
                        {
                            ctx.Status($"Loading {Path.GetFileName(projectPath)}...");

                            try
                            {
                                var projectInfo = loaderService.LoadProject(projectPath);

                                // Skip ignored projects
                                if (IsProjectIgnored(projectInfo.Name, configuration.IgnoredProjects))
                                {
                                    if (settings.Verbose)
                                    {
                                        AnsiConsole.MarkupLine("[dim]Skipping ignored project: {0}[/]",
                                            projectInfo.Name);
                                    }

                                    continue;
                                }

                                var projectType =
                                    typeDetector.DetectProjectType(projectInfo.Name, projectPath, absolutePath);
                                var moduleName = typeDetector.ExtractModuleName(projectInfo.Name, projectType);

                                if (settings.Verbose)
                                {
                                    AnsiConsole.MarkupLine("[dim]Loaded: {0} → Module: {1}, Type: {2}, Refs: {3}[/]",
                                        projectInfo.Name,
                                        moduleName,
                                        projectType,
                                        projectInfo.ProjectReferences.Count);
                                }

                                modules.Add(new ModuleInfo(moduleName, projectType, projectInfo));
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine("[red]Failed to load {0}:[/] {1}",
                                    Path.GetFileName(projectPath),
                                    ex.Message);
                            }
                        }
                    });
            }
        }

        if (shouldShowConsoleOutput)
        {
            DisplayProjectSummary(modules, settings.Verbose);
        }

        if (shouldShowConsoleOutput)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Validating project references...[/]");
            AnsiConsole.WriteLine();
        }

        if (settings.Debug)
        {
            DisplayDebugInfo(configuration, modules, absolutePath);
        }

        var validationEngine = new ConfigurableValidationEngine(configuration, settings.Verbose || settings.Debug);
        var validationResult = validationEngine.ValidateAllWithResult(modules);

        // Handle output based on format
        if (settings.Format != OutputFormat.Console || settings.OutputFile != null)
        {
            var output = ExportReport(settings.Format, modules, validationResult);

            if (settings.OutputFile != null)
            {
                File.WriteAllText(settings.OutputFile, output);
                if (!settings.Quiet)
                {
                    AnsiConsole.MarkupLine("[green]Report exported to:[/] {0}", settings.OutputFile);
                }
            }
            else
            {
                Console.WriteLine(output);
            }

            return validationResult.HasErrors ? 1 : 0;
        }

        // Console output
        if (validationResult.Violations.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ No violations found![/]");
            return 0;
        }

        DisplayViolations(validationResult);

        return validationResult.HasErrors ? 1 : 0;
    }

    private static string ExportReport(OutputFormat format, IReadOnlyList<ModuleInfo> modules,
        ValidationResult validationResult)
    {
        return format switch
        {
            OutputFormat.Json => new JsonReportExporter().Export(modules, validationResult),
            OutputFormat.Markdown => new MarkdownReportExporter().Export(modules, validationResult),
            OutputFormat.Sarif => new SarifReportExporter().Export(modules, validationResult),
            OutputFormat.Csv => new CsvReportExporter().Export(modules, validationResult),
            _ => throw new InvalidOperationException($"Unsupported format: {format}")
        };
    }

    private static void DisplayProjectSummary(List<ModuleInfo> modules, bool verbose)
    {
        // Partition projects into Modules and Shared
        var sharedProjects = modules.Where(m => m.ModuleName == "Shared").OrderBy(m => m.Type).ToList();
        var moduleProjects = modules.Where(m => m.ModuleName != "Shared").ToList();

        // Display Modules table
        if (moduleProjects.Any())
        {
            var modulePanel = new Panel("[bold cyan]Modules[/]")
            {
                Border = BoxBorder.None,
                Padding = new Padding(0, 0, 0, 0)
            };
            AnsiConsole.Write(modulePanel);

            var moduleTable = new Table();
            moduleTable.Border(TableBorder.Rounded);
            moduleTable.AddColumn("[bold]Module[/]");
            moduleTable.AddColumn("[bold]Project Type[/]");
            moduleTable.AddColumn("[bold]Project Name[/]");
            moduleTable.AddColumn("[bold]References[/]");

            if (verbose)
            {
                moduleTable.AddColumn("[bold]File Path[/]");
            }

            var groupedByModule = moduleProjects
                .GroupBy(m => m.ModuleName)
                .OrderBy(g => g.Key);

            foreach (var moduleGroup in groupedByModule)
            {
                var isFirst = true;
                foreach (var module in moduleGroup.OrderBy(m => m.Type))
                {
                    var row = new List<string>
                    {
                        isFirst ? $"[bold cyan]{module.ModuleName}[/]" : string.Empty,
                        $"[yellow]{module.Type}[/]",
                        module.ProjectInfo.Name,
                        module.ProjectInfo.ProjectReferences.Count.ToString()
                    };

                    if (verbose)
                    {
                        row.Add($"[dim]{module.ProjectInfo.FilePath}[/]");
                    }

                    moduleTable.AddRow(row.ToArray());
                    isFirst = false;
                }
            }

            AnsiConsole.Write(moduleTable);
            AnsiConsole.WriteLine();
        }

        // Display Shared Projects table
        if (sharedProjects.Any())
        {
            var sharedPanel = new Panel("[bold green]Shared Projects[/]")
            {
                Border = BoxBorder.None,
                Padding = new Padding(0, 0, 0, 0)
            };
            AnsiConsole.Write(sharedPanel);

            var sharedTable = new Table();
            sharedTable.Border(TableBorder.Rounded);
            sharedTable.AddColumn("[bold]Project Type[/]");
            sharedTable.AddColumn("[bold]Project Name[/]");
            sharedTable.AddColumn("[bold]References[/]");

            if (verbose)
            {
                sharedTable.AddColumn("[bold]File Path[/]");
            }

            foreach (var shared in sharedProjects)
            {
                var row = new List<string>
                {
                    $"[yellow]{shared.Type}[/]",
                    shared.ProjectInfo.Name,
                    shared.ProjectInfo.ProjectReferences.Count.ToString()
                };

                if (verbose)
                {
                    row.Add($"[dim]{shared.ProjectInfo.FilePath}[/]");
                }

                sharedTable.AddRow(row.ToArray());
            }

            AnsiConsole.Write(sharedTable);
            AnsiConsole.WriteLine();
        }

        // Display summary statistics
        var moduleCount = moduleProjects.Select(m => m.ModuleName).Distinct().Count();
        var summaryPanel = new Panel(
            $"[blue]Modules:[/] {moduleCount} ({moduleProjects.Count} projects)\n" +
            $"[blue]Shared:[/] {sharedProjects.Count} projects\n" +
            $"[blue]Total:[/] {modules.Count} projects")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };
        AnsiConsole.Write(summaryPanel);
    }

    private static void DisplayViolations(ValidationResult validationResult)
    {
        foreach (var violation in validationResult.Violations.OrderBy(v => v.ProjectName).ThenBy(v => v.Severity))
        {
            var severityMarkup = violation.Severity switch
            {
                ViolationSeverity.Error => "[red]⚠️ ERROR[/]",
                ViolationSeverity.Warning => "[yellow]⚡ WARNING[/]",
                ViolationSeverity.Info => "[blue]ℹ️ INFO[/]",
                _ => violation.Severity.ToString()
            };

            var locationInfo = string.Empty;
            if (!string.IsNullOrEmpty(violation.FilePath) && violation.LineNumber.HasValue)
            {
                var location = violation.ColumnNumber.HasValue
                    ? $"{violation.FilePath}:{violation.LineNumber}:{violation.ColumnNumber}"
                    : $"{violation.FilePath}:{violation.LineNumber}";
                locationInfo = $"\n[bold]Location:[/] [dim]{location}[/]";
            }

            var violationPanel = new Panel(
                $"[bold]Project:[/] [cyan]{violation.ProjectName}[/]\n" +
                $"[bold]Invalid Reference:[/] [yellow]{violation.InvalidReference}[/]\n" +
                $"[bold]Description:[/] {violation.Description}" +
                locationInfo +
                (violation.Suggestion != null ? $"\n[bold]Suggestion:[/] [dim]{violation.Suggestion}[/]" : "") +
                (violation.DocumentationUrl != null
                    ? $"\n[bold]Documentation:[/] [link]{violation.DocumentationUrl}[/]"
                    : ""))
            {
                Header = new PanelHeader($"{severityMarkup} {violation.RuleName}"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(violation.Severity == ViolationSeverity.Error ? Color.Red : Color.Yellow)
            };

            AnsiConsole.Write(violationPanel);
            AnsiConsole.WriteLine();
        }

        var summaryPanel = new Panel(
            $"[red]Errors:[/] {validationResult.ErrorCount}\n" +
            $"[yellow]Warnings:[/] {validationResult.WarningCount}")
        {
            Header = new PanelHeader("[bold]Validation Summary[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(validationResult.HasErrors ? Color.Red : Color.Yellow)
        };
        AnsiConsole.Write(summaryPanel);

        if (validationResult.HasErrors)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red bold]✗ Validation failed with {0} error(s)[/]", validationResult.ErrorCount);
        }
    }

    private static bool IsProjectIgnored(string projectName, List<string> ignoredPatterns)
    {
        foreach (var pattern in ignoredPatterns)
        {
            if (MatchesGlobPattern(projectName, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesGlobPattern(string projectName, string pattern)
    {
        try
        {
            var regexPattern = Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".");

            var regex = new Regex($"^{regexPattern}$",
                RegexOptions.IgnoreCase);

            return regex.IsMatch(projectName);
        }
        catch
        {
            return false;
        }
    }

    private static void DisplayConfigurationSummary(LinterConfiguration configuration, bool verbose)
    {
        var summaryPanel = new Panel("[bold cyan]Configuration Summary[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };
        AnsiConsole.Write(summaryPanel);
        AnsiConsole.WriteLine();

        // Module Patterns
        if (configuration.Modules.Patterns.Count > 0)
        {
            var moduleTable = new Table();
            moduleTable.Border(TableBorder.Rounded);
            moduleTable.Title = new TableTitle("[bold]Module Patterns[/]");
            moduleTable.AddColumn("[bold]Name[/]");
            moduleTable.AddColumn("[bold]Pattern[/]");
            moduleTable.AddColumn("[bold]Type[/]");

            foreach (var pattern in configuration.Modules.Patterns)
            {
                moduleTable.AddRow(
                    pattern.Name,
                    $"[dim]{pattern.Pattern}[/]",
                    $"[yellow]{pattern.Type}[/]");
            }

            AnsiConsole.Write(moduleTable);
            AnsiConsole.WriteLine();
        }

        // Shared Patterns
        if (configuration.Shared.Patterns.Count > 0)
        {
            var sharedTable = new Table();
            sharedTable.Border(TableBorder.Rounded);
            sharedTable.Title = new TableTitle("[bold]Shared Patterns[/]");
            sharedTable.AddColumn("[bold]Name[/]");
            sharedTable.AddColumn("[bold]Pattern[/]");
            sharedTable.AddColumn("[bold]Type[/]");

            if (!string.IsNullOrEmpty(configuration.Shared.WorkingDirectory))
            {
                sharedTable.Caption = new TableTitle($"[dim]Working Directory: {configuration.Shared.WorkingDirectory}[/]");
            }

            foreach (var pattern in configuration.Shared.Patterns)
            {
                sharedTable.AddRow(
                    pattern.Name,
                    $"[dim]{pattern.Pattern}[/]",
                    $"[yellow]{pattern.Type}[/]");
            }

            AnsiConsole.Write(sharedTable);
            AnsiConsole.WriteLine();
        }

        // Dependency Rules
        if (configuration.DependencyRules.Count > 0)
        {
            var rulesTable = new Table();
            rulesTable.Border(TableBorder.Rounded);
            rulesTable.Title = new TableTitle("[bold]Dependency Rules[/]");
            rulesTable.AddColumn("[bold]Project Type[/]");
            rulesTable.AddColumn("[bold]Allowed[/]");
            rulesTable.AddColumn("[bold]Denied[/]");

            foreach (var rule in configuration.DependencyRules)
            {
                var allowed = rule.Value.Allowed.Count > 0
                    ? string.Join("\n", rule.Value.Allowed.Select(a => $"• {a}"))
                    : "[dim]none[/]";

                var denied = rule.Value.Denied.Count > 0
                    ? string.Join("\n", rule.Value.Denied.Select(d => $"• {d}"))
                    : "[dim]none[/]";

                rulesTable.AddRow(
                    $"[cyan]{rule.Key}[/]",
                    $"[green]{allowed}[/]",
                    $"[red]{denied}[/]");
            }

            AnsiConsole.Write(rulesTable);
            AnsiConsole.WriteLine();
        }

        // Ignored Projects
        if (configuration.IgnoredProjects.Count > 0)
        {
            var ignoredPanel = new Panel(
                string.Join(", ", configuration.IgnoredProjects.Select(p => $"[yellow]{p}[/]")))
            {
                Header = new PanelHeader("[bold]Ignored Projects[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            };
            AnsiConsole.Write(ignoredPanel);
            AnsiConsole.WriteLine();
        }

        // Severity Overrides
        if (configuration.SeverityOverrides.Count > 0)
        {
            var overridesTable = new Table();
            overridesTable.Border(TableBorder.Rounded);
            overridesTable.Title = new TableTitle("[bold]Severity Overrides[/]");
            overridesTable.AddColumn("[bold]Rule[/]");
            overridesTable.AddColumn("[bold]Severity[/]");

            foreach (var @override in configuration.SeverityOverrides)
            {
                var severityColor = @override.Severity.ToLower() switch
                {
                    "error" => "red",
                    "warning" => "yellow",
                    "info" => "blue",
                    _ => "white"
                };

                overridesTable.AddRow(
                    @override.Rule,
                    $"[{severityColor}]{@override.Severity}[/]");
            }

            AnsiConsole.Write(overridesTable);
            AnsiConsole.WriteLine();
        }
    }

    private static void DisplayDebugInfo(LinterConfiguration configuration, List<ModuleInfo> modules, string basePath)
    {
        AnsiConsole.WriteLine();
        var debugPanel = new Panel("[bold yellow]DEBUG MODE - Internal Processing Details[/]")
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow)
        };
        AnsiConsole.Write(debugPanel);
        AnsiConsole.WriteLine();

        // Configuration Info
        AnsiConsole.MarkupLine("[bold]Configuration:[/]");
        AnsiConsole.MarkupLine("  Base Path: [dim]{0}[/]", basePath);
        AnsiConsole.MarkupLine("  Module Patterns: [dim]{0}[/]", configuration.Modules.Patterns.Count);
        foreach (var pattern in configuration.Modules.Patterns)
        {
            AnsiConsole.MarkupLine("    - [dim]{0} ({1})[/]", pattern.Pattern, pattern.Type);
        }

        AnsiConsole.MarkupLine("  Shared Patterns: [dim]{0}[/]", configuration.Shared.Patterns.Count);
        foreach (var pattern in configuration.Shared.Patterns)
        {
            AnsiConsole.MarkupLine("    - [dim]{0} ({1})[/]", pattern.Pattern, pattern.Type);
        }

        if (!string.IsNullOrEmpty(configuration.Shared.WorkingDirectory))
        {
            AnsiConsole.MarkupLine("  Shared Working Directory: [dim]{0}[/]", configuration.Shared.WorkingDirectory);
        }

        AnsiConsole.MarkupLine("  Ignored Projects: [dim]{0}[/]", configuration.IgnoredProjects.Count);
        if (configuration.IgnoredProjects.Count > 0)
        {
            foreach (var ignored in configuration.IgnoredProjects)
            {
                AnsiConsole.MarkupLine("    - [dim]{0}[/]", ignored);
            }
        }

        AnsiConsole.WriteLine();

        // Dependency Rules
        AnsiConsole.MarkupLine("[bold]Dependency Rules:[/]");
        foreach (var rule in configuration.DependencyRules)
        {
            AnsiConsole.MarkupLine("  [cyan]{0}[/]", rule.Key);
            AnsiConsole.MarkupLine("    Allowed: [green]{0}[/]", string.Join(", ", rule.Value.Allowed));
            if (rule.Value.Denied.Count > 0)
            {
                AnsiConsole.MarkupLine("    Denied: [red]{0}[/]", string.Join(", ", rule.Value.Denied));
            }
        }

        AnsiConsole.WriteLine();

        // Module Statistics
        var moduleGroups = modules.GroupBy(m => m.ModuleName).OrderBy(g => g.Key);
        AnsiConsole.MarkupLine("[bold]Module Statistics:[/]");
        foreach (var moduleGroup in moduleGroups)
        {
            var projectTypes = moduleGroup.GroupBy(m => m.Type).Select(g => $"{g.Key} ({g.Count()})");
            AnsiConsole.MarkupLine("  [cyan]{0}[/]: {1} projects - [dim]{2}[/]",
                moduleGroup.Key,
                moduleGroup.Count(),
                string.Join(", ", projectTypes));
        }

        AnsiConsole.WriteLine();

        // Reference Analysis
        AnsiConsole.MarkupLine("[bold]Reference Analysis:[/]");
        var totalReferences = modules.Sum(m => m.ProjectInfo.ProjectReferences.Count);
        var avgReferences = modules.Count > 0 ? (double)totalReferences / modules.Count : 0;
        AnsiConsole.MarkupLine("  Total References: [dim]{0}[/]", totalReferences);
        AnsiConsole.MarkupLine("  Average References per Project: [dim]{0:F2}[/]", avgReferences);

        var mostReferenced = modules
            .SelectMany(m => m.ProjectInfo.ProjectReferences.Select(r => Path.GetFileNameWithoutExtension(r.Path)))
            .GroupBy(name => name)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToList();

        if (mostReferenced.Any())
        {
            AnsiConsole.MarkupLine("  Most Referenced Projects:");
            foreach (var item in mostReferenced)
            {
                AnsiConsole.MarkupLine("    - [dim]{0} ({1} references)[/]", item.Key, item.Count());
            }
        }

        AnsiConsole.WriteLine();
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<PATH>")] public string Path { get; init; } = string.Empty;

        [CommandOption("-o|--output")] public string? OutputFile { get; init; }

        [CommandOption("-f|--format")] public OutputFormat Format { get; init; } = OutputFormat.Console;

        [CommandOption("-v|--verbose")] public bool Verbose { get; init; }

        [CommandOption("-q|--quiet")] public bool Quiet { get; init; }

        [CommandOption("-d|--debug")] public bool Debug { get; init; }

        [CommandOption("-p|--profile")] public string? Profile { get; init; }

        [CommandOption("--no-header")] public bool NoHeader { get; init; }
    }
}