using Microsoft.Build.Evaluation;
using n2jSoft.ModularGuard.CLI.Models;

namespace n2jSoft.ModularGuard.CLI.Services;

public sealed class ProjectLoaderService : IDisposable
{
    private readonly ProjectCollection _projectCollection;
    private bool _disposed;

    public ProjectLoaderService()
    {
        _projectCollection = new ProjectCollection();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _projectCollection.Dispose();
        _disposed = true;
    }

    public ProjectInfo LoadProject(string projectPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var project = _projectCollection.LoadProject(projectPath);

            var projectReferences = project.GetItems("ProjectReference")
                .Select(item =>
                {
                    var outputItemType = item.GetMetadataValue("OutputItemType");
                    var referenceOutputAssemblyStr = item.GetMetadataValue("ReferenceOutputAssembly");

                    // ReferenceOutputAssembly defaults to true if not specified
                    var referenceOutputAssembly = string.IsNullOrEmpty(referenceOutputAssemblyStr) ||
                                                  !referenceOutputAssemblyStr.Equals("false",
                                                      StringComparison.OrdinalIgnoreCase);

                    // Extract location information from the XML element
                    var location = item.Xml.Location;
                    var filePath = location.File;
                    var lineNumber = location.Line;
                    var columnNumber = location.Column;

                    return new ProjectReferenceInfo(
                        item.EvaluatedInclude,
                        string.IsNullOrEmpty(outputItemType) ? null : outputItemType,
                        referenceOutputAssembly,
                        string.IsNullOrEmpty(filePath) ? null : filePath,
                        lineNumber > 0 ? lineNumber : null,
                        columnNumber > 0 ? columnNumber : null);
                })
                .ToList();

            return new ProjectInfo(
                project.GetPropertyValue("ProjectName") is { Length: > 0 } name
                    ? name
                    : Path.GetFileNameWithoutExtension(projectPath),
                projectPath,
                projectReferences);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load project: {projectPath}{Environment.NewLine}Error: {ex.Message}", ex);
        }
    }
}