# Changelog

All notable changes to ModularGuard will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-02-10

### Added

#### Core Features
- **Project Discovery**: Automatic scanning and discovery of `.csproj` files using MSBuild API
- **Reference Validation**: Validates project references against architectural rules
- **Rich Console Output**: Color-coded tables and panels with progress indicators
- **Multiple Export Formats**: JSON and Markdown report generation
- **CI/CD Integration**: Exit codes and quiet mode for automation

#### Configuration System
- **Configuration File Support**: YAML and JSON configuration files (`.modularguard.yml`, `.modularguard.json`)
- **Configurable Project Structure**: Define custom project patterns and types
- **Configurable Dependency Rules**: Allow/deny lists per project type with wildcard and module-scoped patterns
- **Ignore Patterns**: Exclude specific projects from validation
- **Severity Overrides**: Downgrade errors to warnings or info
- **Auto-discovery**: Automatically finds configuration files in project root
- **Default Configuration**: Falls back to modular monolith rules when no config file is found

#### Validation Rules
- Core project validation
- Infrastructure project validation
- App project validation (Admin/Private/Public)
- Endpoints project validation (Admin/Private/Public)
- Shared Events/Messages validation

#### Command Line Options
- `--output, -o`: Export report to file
- `--format, -f`: Output format (Console, Json, Markdown)
- `--verbose, -v`: Show detailed information
- `--quiet, -q`: Minimal output
- `--help, -h`: Display help

#### Testing
- 47 unit tests covering core functionality
- xUnit test framework
- Test coverage for:
  - Project type detection
  - Validation rules
  - Validation engine
  - Configuration system

### Technical Details

#### Dependencies
- .NET 10.0
- Spectre.Console.Cli 0.53.1
- Microsoft.Build 18.0.2
- Microsoft.Build.Locator 1.11.2
- YamlDotNet 16.3.0

#### Architecture
- Clean architecture with separation of concerns
- Configurable validation engine
- Dynamic rule generation from configuration
- Pattern matching with regex and glob support

### Documentation
- Comprehensive README with usage examples
- Configuration documentation with examples
- Example configuration files for different scenarios
- Developer documentation (AGENTS.md)
- Roadmap with implementation phases

## [Unreleased]

### Changed
- **Output Behavior**: Non-Console formats (`json`, `markdown`, `sarif`, `csv`) now suppress verbose console output (project tables, scanning messages) by default for clean, parseable output. Use `--verbose` flag to see progress messages with structured formats.

### Planned Features
- Circular dependency detection
- Dependency graph visualization
- Auto-fix capabilities
- Watch mode for real-time validation
- IDE integration (LSP)
- Performance optimizations with caching

---

## Release Notes

### v1.0.0 - Initial Release

This is the initial release of ModularGuard, a powerful CLI tool for enforcing architectural boundaries in modular monolith applications.

**Key Highlights:**
- ✅ Full configuration system for custom architectures
- ✅ Rich console output with Spectre.Console
- ✅ Multiple export formats (JSON, Markdown)
- ✅ CI/CD ready with proper exit codes
- ✅ 47 unit tests with comprehensive coverage

**Installation:**
```bash
dotnet tool install --global ModularGuard
```

**Quick Start:**
```bash
# Validate current directory
modularguard check .

# With custom configuration
modularguard check . --config .modularguard.yml

# Export to JSON
modularguard check . --format json --output report.json
```

For detailed documentation, see [README.md](README.md).
