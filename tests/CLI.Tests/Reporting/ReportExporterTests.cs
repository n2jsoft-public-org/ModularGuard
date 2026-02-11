using System.Text.Json;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Reporting;
using n2jSoft.ModularGuard.CLI.Validation;

namespace n2jSoft.ModularGuard.CLI.Tests.Reporting;

public class ReportExporterTests
{
    private readonly List<ModuleInfo> _testModules;
    private readonly ValidationResult _testValidationResult;

    public ReportExporterTests()
    {
        // Setup test data
        var coreProject = new ProjectInfo(
            "TestModule.Core",
            "/path/to/TestModule.Core.csproj",
            new List<ProjectReferenceInfo>
            {
                new("../Shared.Core/Shared.Core.csproj", null, true)
            });

        var infraProject = new ProjectInfo(
            "TestModule.Infrastructure",
            "/path/to/TestModule.Infrastructure.csproj",
            new List<ProjectReferenceInfo>
            {
                new("../TestModule.Core/TestModule.Core.csproj", null, true),
                new("../InvalidReference/Invalid.csproj", null, true)
            });

        _testModules = new List<ModuleInfo>
        {
            new("TestModule", "core", coreProject),
            new("TestModule", "infrastructure", infraProject)
        };

        var violations = new List<Violation>
        {
            new(
                "TestModule.Infrastructure",
                "Invalid",
                "TestRule",
                "This is a test violation",
                ViolationSeverity.Error,
                "Remove the invalid reference",
                "https://example.com/docs")
        };

        _testValidationResult = new ValidationResult(violations);
    }

    [Fact]
    public void JsonReportExporter_ShouldProduceValidJson()
    {
        // Arrange
        var exporter = new JsonReportExporter();

        // Act
        var output = exporter.Export(_testModules, _testValidationResult);

        // Assert
        Assert.NotNull(output);
        Assert.NotEmpty(output);

        // Verify it's valid JSON
        var jsonDoc = JsonDocument.Parse(output);
        Assert.NotNull(jsonDoc);
    }

    [Fact]
    public void JsonReportExporter_ShouldIncludeSummary()
    {
        // Arrange
        var exporter = new JsonReportExporter();

        // Act
        var output = exporter.Export(_testModules, _testValidationResult);
        var jsonDoc = JsonDocument.Parse(output);

        // Assert
        var summary = jsonDoc.RootElement.GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("totalModules").GetInt32());
        Assert.Equal(2, summary.GetProperty("totalProjects").GetInt32());
        Assert.Equal(1, summary.GetProperty("errorCount").GetInt32());
        Assert.False(summary.GetProperty("isValid").GetBoolean());
    }

    [Fact]
    public void JsonReportExporter_ShouldIncludeViolationsWithSuggestions()
    {
        // Arrange
        var exporter = new JsonReportExporter();

        // Act
        var output = exporter.Export(_testModules, _testValidationResult);
        var jsonDoc = JsonDocument.Parse(output);

        // Assert
        var violations = jsonDoc.RootElement.GetProperty("violations");
        Assert.Equal(1, violations.GetArrayLength());

        var violation = violations[0];
        Assert.Equal("TestModule.Infrastructure", violation.GetProperty("projectName").GetString());
        Assert.Equal("Remove the invalid reference", violation.GetProperty("suggestion").GetString());
        Assert.Equal("https://example.com/docs", violation.GetProperty("documentationUrl").GetString());
    }

    [Fact]
    public void MarkdownReportExporter_ShouldProduceValidMarkdown()
    {
        // Arrange
        var exporter = new MarkdownReportExporter();

        // Act
        var output = exporter.Export(_testModules, _testValidationResult);

        // Assert
        Assert.NotNull(output);
        Assert.NotEmpty(output);
        Assert.Contains("# Modular Monolith Validation Report", output);
        Assert.Contains("## Summary", output);
        Assert.Contains("## Violations", output);
    }

    [Fact]
    public void MarkdownReportExporter_ShouldIncludeSuggestions()
    {
        // Arrange
        var exporter = new MarkdownReportExporter();

        // Act
        var output = exporter.Export(_testModules, _testValidationResult);

        // Assert
        Assert.Contains("**Suggestion**", output);
        Assert.Contains("Remove the invalid reference", output);
        Assert.Contains("**Documentation**", output);
        Assert.Contains("https://example.com/docs", output);
    }

    [Fact]
    public void SarifReportExporter_ShouldProduceValidSarif()
    {
        // Arrange
        var exporter = new SarifReportExporter();

        // Act
        var output = exporter.Export(_testModules, _testValidationResult);

        // Assert
        Assert.NotNull(output);
        Assert.NotEmpty(output);

        // Verify it's valid JSON
        var jsonDoc = JsonDocument.Parse(output);
        Assert.NotNull(jsonDoc);

        // Verify SARIF structure
        var root = jsonDoc.RootElement;
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        Assert.True(root.TryGetProperty("$schema", out _));
        Assert.True(root.TryGetProperty("runs", out _));
    }

    [Fact]
    public void SarifReportExporter_ShouldIncludeToolInformation()
    {
        // Arrange
        var exporter = new SarifReportExporter();

        // Act
        var output = exporter.Export(_testModules, _testValidationResult);
        var jsonDoc = JsonDocument.Parse(output);

        // Assert
        var tool = jsonDoc.RootElement.GetProperty("runs")[0].GetProperty("tool").GetProperty("driver");
        Assert.Equal("Modular Monolith Linter", tool.GetProperty("name").GetString());
        Assert.True(tool.TryGetProperty("informationUri", out _));
    }

    [Fact]
    public void SarifReportExporter_ShouldIncludeRulesAndResults()
    {
        // Arrange
        var exporter = new SarifReportExporter();

        // Act
        var output = exporter.Export(_testModules, _testValidationResult);
        var jsonDoc = JsonDocument.Parse(output);

        // Assert
        var run = jsonDoc.RootElement.GetProperty("runs")[0];
        var rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules");
        var results = run.GetProperty("results");

        Assert.Equal(1, rules.GetArrayLength());
        Assert.Equal(1, results.GetArrayLength());

        var result = results[0];
        Assert.Equal("error", result.GetProperty("level").GetString());
        Assert.True(result.TryGetProperty("properties", out var properties));
        Assert.Equal("Remove the invalid reference", properties.GetProperty("suggestion").GetString());
    }

    [Fact]
    public void CsvReportExporter_ShouldProduceValidCsv()
    {
        // Arrange
        var exporter = new CsvReportExporter();

        // Act
        var output = exporter.Export(_testModules, _testValidationResult);

        // Assert
        Assert.NotNull(output);
        Assert.NotEmpty(output);
        Assert.StartsWith("Severity,Project,InvalidReference,RuleName,Description,Suggestion,DocumentationUrl", output);
    }

    [Fact]
    public void CsvReportExporter_ShouldIncludeAllFields()
    {
        // Arrange
        var exporter = new CsvReportExporter();

        // Act
        var output = exporter.Export(_testModules, _testValidationResult);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        Assert.True(lines.Length >= 2, "CSV should have header and at least one data row");

        var dataLine = lines[1];
        Assert.Contains("Error", dataLine);
        Assert.Contains("TestModule.Infrastructure", dataLine);
        Assert.Contains("Invalid", dataLine);
        Assert.Contains("TestRule", dataLine);
    }

    [Fact]
    public void CsvReportExporter_ShouldEscapeCommasInFields()
    {
        // Arrange
        var exporter = new CsvReportExporter();
        var violations = new List<Violation>
        {
            new(
                "Test.Project",
                "Invalid.Ref",
                "TestRule",
                "Description with, comma",
                ViolationSeverity.Error,
                "Suggestion with, comma",
                "https://example.com")
        };
        var result = new ValidationResult(violations);

        // Act
        var output = exporter.Export(_testModules, result);

        // Assert
        Assert.Contains("\"Description with, comma\"", output);
        Assert.Contains("\"Suggestion with, comma\"", output);
    }

    [Fact]
    public void AllExporters_ShouldHandleEmptyViolations()
    {
        // Arrange
        var emptyResult = new ValidationResult(new List<Violation>());

        // Act & Assert
        var jsonExporter = new JsonReportExporter();
        var jsonOutput = jsonExporter.Export(_testModules, emptyResult);
        Assert.NotNull(jsonOutput);
        var jsonDoc = JsonDocument.Parse(jsonOutput);
        Assert.True(jsonDoc.RootElement.GetProperty("summary").GetProperty("isValid").GetBoolean());

        var markdownExporter = new MarkdownReportExporter();
        var markdownOutput = markdownExporter.Export(_testModules, emptyResult);
        Assert.NotNull(markdownOutput);
        Assert.Contains("✅ Valid", markdownOutput);

        var sarifExporter = new SarifReportExporter();
        var sarifOutput = sarifExporter.Export(_testModules, emptyResult);
        Assert.NotNull(sarifOutput);

        var csvExporter = new CsvReportExporter();
        var csvOutput = csvExporter.Export(_testModules, emptyResult);
        Assert.NotNull(csvOutput);
        Assert.Single(csvOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)); // Only header
    }
}