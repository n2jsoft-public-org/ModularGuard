using System.Text.RegularExpressions;
using n2jSoft.ModularGuard.CLI.Configuration;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Services;
using n2jSoft.ModularGuard.CLI.Validation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace n2jSoft.ModularGuard.CLI.Commands;

/// <summary>
///     Command to automatically fix violations by removing invalid project references
/// </summary>
public sealed class FixCommand : Command<FixCommand.Settings>
{
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
        var configuration = configLoader.TryLoadConfiguration(absolutePath, settings.Profile)
                            ?? configLoader.CreateDefaultConfiguration();

        if (!settings.Quiet)
        {
            var modeText = settings.DryRun
                ? "[yellow]DRY RUN MODE[/] - No changes will be made"
                : "Auto-fixing violations";
            AnsiConsole.MarkupLine("[blue]{0}[/]", modeText);
            AnsiConsole.WriteLine();
        }

        // Discover and load projects
        var discoveryService = new ProjectDiscoveryService();
        var projectPaths = discoveryService.DiscoverProjects(absolutePath).ToList();

        if (projectPaths.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No .csproj files found.[/]");
            return 0;
        }

        var modules = new List<ModuleInfo>();
        var typeDetector = new ConfigurableProjectTypeDetector(configuration);

        using (var loaderService = new ProjectLoaderService())
        {
            if (!settings.Quiet)
            {
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
                                    continue;
                                }

                                var projectType =
                                    typeDetector.DetectProjectType(projectInfo.Name, projectPath, absolutePath);
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
                    });
            }
            else
            {
                foreach (var projectPath in projectPaths)
                {
                    try
                    {
                        var projectInfo = loaderService.LoadProject(projectPath);

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
        }

        // Validate projects to find violations
        if (!settings.Quiet)
        {
            AnsiConsole.MarkupLine("[blue]Validating project references...[/]");
            AnsiConsole.WriteLine();
        }

        var validationEngine = new ConfigurableValidationEngine(configuration, settings.Verbose);
        var validationResult = validationEngine.ValidateAllWithResult(modules);

        if (validationResult.Violations.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ No violations found! Nothing to fix.[/]");
            return 0;
        }

        // Filter to only fixable violations
        var fixableViolations = validationResult.Violations
            .Where(v => v.IsAutoFixable && v.Severity == ViolationSeverity.Error)
            .ToList();

        var unfixableViolations = validationResult.Violations
            .Where(v => !v.IsAutoFixable || v.Severity != ViolationSeverity.Error)
            .ToList();

        if (!settings.Quiet)
        {
            AnsiConsole.MarkupLine("[yellow]Found {0} violation(s):[/]", validationResult.Violations.Count);
            AnsiConsole.MarkupLine("  [green]Auto-fixable:[/] {0}", fixableViolations.Count);
            AnsiConsole.MarkupLine("  [dim]Manual fix required:[/] {0}", unfixableViolations.Count);
            AnsiConsole.WriteLine();
        }

        if (fixableViolations.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No auto-fixable violations found.[/]");

            if (unfixableViolations.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]The following violations require manual intervention:[/]");
                foreach (var violation in unfixableViolations.Take(5))
                {
                    AnsiConsole.MarkupLine("[dim]  - {0}: {1}[/]", violation.ProjectName, violation.Description);
                }
            }

            return 1;
        }

        // Interactive mode: ask for confirmation
        if (settings.Interactive && !settings.DryRun)
        {
            DisplayFixableViolations(fixableViolations);

            if (!AnsiConsole.Confirm($"Do you want to fix {fixableViolations.Count} violation(s)?"))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled by user.[/]");
                return 1;
            }

            AnsiConsole.WriteLine();
        }
        else if (!settings.Quiet)
        {
            DisplayFixableViolations(fixableViolations);
        }

        // Apply fixes
        var autoFixService = new AutoFixService();
        var fixResults = autoFixService.FixViolations(fixableViolations, modules, settings.DryRun);

        // Display results
        if (!settings.Quiet)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Fix Results:[/]");
            AnsiConsole.WriteLine();
        }

        var successCount = 0;
        var failureCount = 0;

        foreach (var result in fixResults)
        {
            if (result.Success)
            {
                successCount++;
                if (!settings.Quiet || settings.Verbose)
                {
                    var icon = settings.DryRun ? "👁️" : "✓";
                    AnsiConsole.MarkupLine("[green]{0}[/] {1}", icon, result.Message);
                }
            }
            else
            {
                failureCount++;
                if (!settings.Quiet)
                {
                    AnsiConsole.MarkupLine("[red]✗[/] {0}", result.Message);
                }
            }
        }

        if (!settings.Quiet)
        {
            AnsiConsole.WriteLine();

            var summaryPanel = new Panel(
                $"[green]Successful:[/] {successCount}\n" +
                $"[red]Failed:[/] {failureCount}")
            {
                Header = new PanelHeader(settings.DryRun ? "[bold]Dry Run Summary[/]" : "[bold]Fix Summary[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(failureCount > 0 ? Color.Yellow : Color.Green)
            };
            AnsiConsole.Write(summaryPanel);

            if (settings.DryRun && successCount > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Run without --dry-run to apply these fixes.[/]");
            }
        }

        return failureCount > 0 ? 1 : 0;
    }

    private static void DisplayFixableViolations(List<Violation> violations)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold]Project[/]");
        table.AddColumn("[bold]Invalid Reference[/]");
        table.AddColumn("[bold]Location[/]");
        table.AddColumn("[bold]Rule[/]");

        foreach (var violation in violations)
        {
            var location = string.Empty;
            if (!string.IsNullOrEmpty(violation.FilePath) && violation.LineNumber.HasValue)
            {
                location = violation.ColumnNumber.HasValue
                    ? $"{violation.FilePath}:{violation.LineNumber}:{violation.ColumnNumber}"
                    : $"{violation.FilePath}:{violation.LineNumber}";
            }

            table.AddRow(
                violation.ProjectName,
                $"[yellow]{violation.InvalidReference}[/]",
                $"[dim]{location}[/]",
                $"[dim]{violation.RuleName}[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
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

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<PATH>")] public string Path { get; init; } = string.Empty;

        [CommandOption("--dry-run")] public bool DryRun { get; init; }

        [CommandOption("-v|--verbose")] public bool Verbose { get; init; }

        [CommandOption("-q|--quiet")] public bool Quiet { get; init; }

        [CommandOption("-p|--profile")] public string? Profile { get; init; }

        [CommandOption("--interactive")] public bool Interactive { get; init; }
    }
}