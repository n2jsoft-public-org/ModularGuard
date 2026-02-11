using System.Text.Json.Serialization;

namespace n2jSoft.ModularGuard.CLI.Serialization;

/// <summary>
///     Base class for report JSON serializer contexts with common options
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public class ReportJsonSerializerContextBase;