using Microsoft.Build.Evaluation;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Reporting;
using n2jSoft.ModularGuard.CLI.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace n2jSoft.ModularGuard.CLI.Commands;

public sealed class OptimizeCommand : Command<OptimizeCommand.Settings>
{
    public enum OutputFormat
    {
        Console,
        Json,
        Markdown
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var absolutePath = Path.GetFullPath(settings.Path);

        if (!Directory.Exists(absolutePath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Directory not found: {0}", absolutePath);
            return 1;
        }

        if (!settings.Quiet)
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

        if (!settings.Quiet)
        {
            AnsiConsole.MarkupLine("[green]Found {0} project(s)[/]", projectPaths.Count);
            AnsiConsole.WriteLine();
        }

        var projects = new List<ProjectInfo>();

        using (var loaderService = new ProjectLoaderService())
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
                            projects.Add(projectInfo);
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

        if (!settings.Quiet)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Analyzing project references...[/]");
            AnsiConsole.WriteLine();
        }

        IReadOnlyList<OptimizationResult> results;

        using (var loaderService = new ProjectLoaderService())
        {
            var analyzer = new ProjectReferenceAnalyzer(loaderService);
            results = AnsiConsole.Status()
                .Start("Analyzing references...", ctx => { return analyzer.AnalyzeProjects(projects); });
        }

        // Handle output based on format
        if (settings.Format != OutputFormat.Console || settings.OutputFile != null)
        {
            var output = ExportReport(settings.Format, results);

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
        }
        else
        {
            // Console output
            DisplayResults(results, settings.Verbose);
        }

        // Apply changes if requested
        if (settings.Apply && results.Count > 0)
        {
            if (!settings.Quiet)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Applying optimizations...[/]");
            }

            ApplyOptimizations(results, settings.Quiet);

            if (!settings.Quiet)
            {
                AnsiConsole.MarkupLine("[green]✓ Optimizations applied successfully![/]");
            }
        }

        return 0;
    }

    private static string ExportReport(OutputFormat format, IReadOnlyList<OptimizationResult> results)
    {
        return format switch
        {
            OutputFormat.Json => new OptimizationJsonReportExporter().Export(results),
            OutputFormat.Markdown => new OptimizationMarkdownReportExporter().Export(results),
            _ => throw new InvalidOperationException($"Unsupported format: {format}")
        };
    }

    private static void DisplayResults(IReadOnlyList<OptimizationResult> results, bool verbose)
    {
        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ No unnecessary references found![/]");
            return;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold]Project[/]");
        table.AddColumn("[bold]Reference[/]");
        table.AddColumn("[bold]Reason[/]");
        table.AddColumn("[bold]Details[/]");

        var totalUnused = 0;
        var totalTransitive = 0;

        foreach (var result in results.OrderBy(r => r.ProjectName))
        {
            var isFirstRow = true;

            foreach (var unnecessaryRef in result.UnnecessaryReferences.OrderBy(r => r.Reason)
                         .ThenBy(r => r.ReferenceName))
            {
                var reasonMarkup = unnecessaryRef.Reason switch
                {
                    UnnecessaryReferenceReason.Unused => "[yellow]Unused[/]",
                    UnnecessaryReferenceReason.Transitive => "[blue]Transitive[/]",
                    _ => unnecessaryRef.Reason.ToString()
                };

                var details = unnecessaryRef.Reason == UnnecessaryReferenceReason.Transitive &&
                              !string.IsNullOrEmpty(unnecessaryRef.TransitivePath)
                    ? $"[dim]{unnecessaryRef.TransitivePath}[/]"
                    : string.Empty;

                table.AddRow(
                    isFirstRow ? $"[cyan]{result.ProjectName}[/]" : string.Empty,
                    unnecessaryRef.ReferenceName,
                    reasonMarkup,
                    details);

                isFirstRow = false;

                if (unnecessaryRef.Reason == UnnecessaryReferenceReason.Unused)
                {
                    totalUnused++;
                }
                else if (unnecessaryRef.Reason == UnnecessaryReferenceReason.Transitive)
                {
                    totalTransitive++;
                }
            }
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var summaryPanel = new Panel(
            $"[blue]Projects with issues:[/] {results.Count}\n" +
            $"[yellow]Unused references:[/] {totalUnused}\n" +
            $"[blue]Transitive references:[/] {totalTransitive}\n" +
            $"[green]Total unnecessary:[/] {totalUnused + totalTransitive}")
        {
            Header = new PanelHeader("[bold]Optimization Summary[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };
        AnsiConsole.Write(summaryPanel);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Tip: Use --apply to automatically remove these references[/]");
    }

    private static void ApplyOptimizations(IReadOnlyList<OptimizationResult> results, bool quiet)
    {
        using var projectCollection = new ProjectCollection();

        foreach (var result in results)
        {
            try
            {
                var project = projectCollection.LoadProject(result.ProjectPath);
                var modified = false;

                foreach (var unnecessaryRef in result.UnnecessaryReferences)
                {
                    // Find and remove the project reference
                    var itemsToRemove = project.GetItems("ProjectReference")
                        .Where(item =>
                        {
                            var referencedProjectName = Path.GetFileNameWithoutExtension(item.EvaluatedInclude);
                            if (!referencedProjectName.Equals(unnecessaryRef.ReferenceName,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                return false;
                            }

                            // Safety check: don't remove special references (analyzers, build-time only, etc.)
                            var outputItemType = item.GetMetadataValue("OutputItemType");
                            if (!string.IsNullOrEmpty(outputItemType) &&
                                outputItemType.Equals("Analyzer", StringComparison.OrdinalIgnoreCase))
                            {
                                return false;
                            }

                            var referenceOutputAssemblyStr = item.GetMetadataValue("ReferenceOutputAssembly");
                            if (!string.IsNullOrEmpty(referenceOutputAssemblyStr) &&
                                referenceOutputAssemblyStr.Equals("false", StringComparison.OrdinalIgnoreCase))
                            {
                                return false;
                            }

                            return true;
                        })
                        .ToList();

                    foreach (var item in itemsToRemove)
                    {
                        project.RemoveItem(item);
                        modified = true;

                        if (!quiet)
                        {
                            AnsiConsole.MarkupLine("[dim]  Removed {0} from {1}[/]",
                                unnecessaryRef.ReferenceName,
                                result.ProjectName);
                        }
                    }
                }

                if (modified)
                {
                    project.Save();
                }

                projectCollection.UnloadProject(project);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[red]Failed to update {0}:[/] {1}",
                    result.ProjectName,
                    ex.Message);
            }
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<PATH>")] public string Path { get; init; } = string.Empty;

        [CommandOption("-o|--output")] public string? OutputFile { get; init; }

        [CommandOption("-f|--format")] public OutputFormat Format { get; init; } = OutputFormat.Console;

        [CommandOption("-v|--verbose")] public bool Verbose { get; init; }

        [CommandOption("-q|--quiet")] public bool Quiet { get; init; }

        [CommandOption("--apply")] public bool Apply { get; init; }
    }
}