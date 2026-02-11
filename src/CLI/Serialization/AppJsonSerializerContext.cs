using System.Text.Json.Serialization;
using n2jSoft.ModularGuard.CLI.Configuration;

namespace n2jSoft.ModularGuard.CLI.Serialization;

/// <summary>
///     JSON serializer context for NativeAOT and trimming compatibility
/// </summary>
[JsonSerializable(typeof(LinterConfiguration))]
[JsonSerializable(typeof(ModuleConfiguration))]
[JsonSerializable(typeof(SharedConfiguration))]
[JsonSerializable(typeof(ProjectPattern))]
[JsonSerializable(typeof(DependencyRuleConfiguration))]
[JsonSerializable(typeof(SeverityOverride))]
[JsonSerializable(typeof(ConfigurationProfile))]
[JsonSerializable(typeof(Dictionary<string, DependencyRuleConfiguration>))]
[JsonSerializable(typeof(Dictionary<string, ConfigurationProfile>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<ProjectPattern>))]
[JsonSerializable(typeof(List<SeverityOverride>))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
public partial class AppJsonSerializerContext : JsonSerializerContext;