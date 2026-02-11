# ModularGuard

A CLI tool that validates project references in a modular monolith architecture. It ensures that C# projects follow
architectural rules to maintain clean boundaries between modules.

## Features

- ✅ **Project Discovery**: Automatically scans directories for `.csproj` files
- ✅ **Reference Validation**: Validates project references against modular monolith rules
- ✅ **Configurable Rules**: Customize project structure and dependency rules via configuration file
- ✅ **Multiple Output Formats**: Console, JSON, and Markdown reports
- ✅ **CI/CD Ready**: Exit codes and quiet mode for automation
- ✅ **Rich Console Output**: Color-coded tables and panels with Spectre.Console

## Installation

### Quick Install (Recommended)

Install ModularGuard with a single command:

#### Linux/macOS

```bash
curl -fsSL https://raw.githubusercontent.com/n2jsoft-public-org/ModularGuard/main/install.sh | bash
```

Or with wget:

```bash
wget -qO- https://raw.githubusercontent.com/n2jsoft-public-org/ModularGuard/main/install.sh | bash
```

#### Windows (PowerShell)

```powershell
irm https://raw.githubusercontent.com/n2jsoft-public-org/ModularGuard/main/install.ps1 | iex
```

The installation script will:
- Detect your platform and architecture automatically
- Download the latest release
- Install to `~/.local/bin` (Linux/macOS) or `%USERPROFILE%\.local\bin` (Windows)
- Configure your PATH if needed

### Alternative Installation Methods

#### Option 1: Docker Image

Pull the latest Docker image from GitHub Container Registry:

```bash
docker pull ghcr.io/n2jsoft-public-org/modularguard:latest
```

#### Option 2: Manual Binary Download

Download the latest release for your platform from the [releases page](https://github.com/n2jsoft-public-org/ModularGuard/releases):

- **Linux (x64)**: `modularguard-linux-x64.tar.gz`
- **Linux (ARM64)**: `modularguard-linux-arm64.tar.gz`
- **macOS (x64)**: `modularguard-osx-x64.tar.gz`
- **macOS (ARM64)**: `modularguard-osx-arm64.tar.gz`
- **Windows (x64)**: `modularguard-win-x64.zip`
- **Windows (ARM64)**: `modularguard-win-arm64.zip`

Extract and optionally add to your PATH:

```bash
# Linux/macOS
tar -xzf modularguard-*.tar.gz
chmod +x modularguard
mkdir -p ~/.local/bin
mv modularguard ~/.local/bin/
# Add ~/.local/bin to your PATH if needed

# Windows (PowerShell)
Expand-Archive modularguard-win-x64.zip
# Move to a directory in your PATH
```

#### Option 3: Build from Source

```bash
# Clone the repository
git clone https://github.com/n2jsoft-public-org/ModularGuard.git
cd ModularGuard

# Build the project
dotnet build

# Run from source
dotnet run --project src/CLI -- check <path-to-your-project>
```

## Usage

### Using Docker

```bash
# Validate current directory
docker run --rm -v $(pwd):/workspace ghcr.io/n2jsoft-public-org/modularguard:latest check /workspace

# Validate specific directory
docker run --rm -v /path/to/your/project:/workspace ghcr.io/n2jsoft-public-org/modularguard:latest check /workspace

# With output file
docker run --rm -v $(pwd):/workspace ghcr.io/n2jsoft-public-org/modularguard:latest check /workspace --format json --output /workspace/report.json
```

### Using Pre-built Binary

```bash
# Validate current directory
modularguard check .

# Validate specific directory
modularguard check /path/to/your/project
```

### Using Source Code

```bash
# Validate current directory
dotnet run --project src/CLI -- check .

# Validate specific directory
dotnet run --project src/CLI -- check /path/to/your/project
```

### Output Formats

```bash
# Docker
docker run --rm -v $(pwd):/workspace ghcr.io/n2jsoft-public-org/modularguard:latest check /workspace --format json --output /workspace/report.json
docker run --rm -v $(pwd):/workspace ghcr.io/n2jsoft-public-org/modularguard:latest check /workspace --format markdown --output /workspace/report.md

# Binary
modularguard check . --format json --output report.json
modularguard check . --format markdown --output report.md
modularguard check . --format json

# Source
dotnet run --project src/CLI -- check . --format json --output report.json
dotnet run --project src/CLI -- check . --format markdown --output report.md
```

### Verbosity Options

```bash
# Docker - Quiet mode (minimal output, good for CI/CD)
docker run --rm -v $(pwd):/workspace ghcr.io/n2jsoft-public-org/modularguard:latest check /workspace --quiet

# Binary - Verbose mode (includes file paths)
modularguard check . --verbose

# Source - Quiet mode
dotnet run --project src/CLI -- check . --quiet
```

## Architectural Rules

ModularGuard enforces the following rules for modular monolith projects:

### Core Projects (`*.Core`)

- ✅ Can reference: `Shared.Core`
- ❌ Cannot reference: Infrastructure, App, or Endpoints projects

### Infrastructure Projects (`*.Infrastructure`)

- ✅ Can reference: `Shared.Infrastructure`, same module's Core project
- ❌ Cannot reference: App or Endpoints projects

### App Projects (`*.[Admin|Private|Public].App`)

- ✅ Can reference:
    - Matching `Shared.App.[Admin|Private|Public]`
    - Same module's Core and Infrastructure projects
    - Other modules' `*.Shared.Events` and `*.Shared.Messages`
- ❌ Cannot reference: Endpoints projects

### Endpoints Projects (`*.[Admin|Private|Public].Endpoints`)

- ✅ Can reference: Same module's matching App project only
- ❌ Cannot reference: Core or Infrastructure projects directly

### Shared Events/Messages Projects

- ✅ Events can reference: `Shared.Events.Abstractions`
- ✅ Messages can reference: Message abstraction projects

## Configuration

The tool can be customized using a configuration file. Create a `.modularguard.yml` or `.modularguard.json` file in your
project root.

### Configuration File Discovery

ModularGuard automatically searches for configuration files in the following order:

1. `.modularguard.yml`
2. `.modularguard.yaml`
3. `.modularguard.json`
4. `modularguard.yml`
5. `modularguard.yaml`
6. `modularguard.json`

If no configuration file is found, ModularGuard uses default modular monolith rules.

### Configuration Example

```yaml
# .modularguard.yml
projectStructure:
  patterns:
    - name: "Core"
      pattern: "*.Core"
      type: "core"
      moduleExtraction: "^(.+)\\.Core$"

    - name: "Infrastructure"
      pattern: "*.Infrastructure"
      type: "infrastructure"
      moduleExtraction: "^(.+)\\.Infrastructure$"

    - name: "CustomLayer"
      pattern: "*.Custom"
      type: "custom"
      moduleExtraction: "^(.+)\\.Custom$"

dependencyRules:
  core:
    allowed:
      - "Shared.Core"
    denied:
      - "*.Infrastructure"
      - "*.App"
      - "*.Endpoints"

  infrastructure:
    allowed:
      - "Shared.Infrastructure"
      - "{module}.Core"  # Same module's Core project
    denied:
      - "*.App"
      - "*.Endpoints"

  custom:
    allowed:
      - "*"  # Allow all
    denied: []

ignoredProjects:
  - "*.Tests"
  - "*.TestHelpers"

severityOverrides:
  - rule: "ConfigurableRule[core]"
    severity: "Warning"  # Downgrade from Error to Warning
```

### Configuration Options

#### Project Structure

Define custom project patterns and types:

- **name**: Display name for the project type
- **pattern**: Glob pattern to match project names (e.g., `*.Core`, `*.Custom`)
- **type**: Unique identifier for the project type
- **moduleExtraction**: Regex pattern to extract module name (optional)

#### Dependency Rules

Define allowed and denied dependencies for each project type:

- **allowed**: List of allowed dependency patterns
    - Exact match: `Shared.Core`
    - Wildcard: `*.Shared.Events`
    - Module-scoped: `{module}.Core` (references same module)
- **denied**: List of prohibited dependency patterns

#### Ignored Projects

Use glob patterns to ignore specific projects:

```yaml
ignoredProjects:
  - "*.Tests"
  - "*.Benchmarks"
  - "TestHelpers.*"
```

#### Severity Overrides

Override severity levels for specific rules:

```yaml
severityOverrides:
  - rule: "ConfigurableRule[core]"
    severity: "Warning"  # Options: Error, Warning, Info
```

## Exit Codes

- `0`: No violations found
- `1`: Violations found (errors detected)

## Example Output

### Console Output

```
Scanning for projects in: /path/to/project

Found 15 project(s)

╭─────────────────┬─────────────────┬──────────────────────────┬────────────╮
│ Module          │ Project Type    │ Project Name             │ References │
├─────────────────┼─────────────────┼──────────────────────────┼────────────┤
│ UserManagement  │ Core            │ UserManagement.Core      │ 1          │
│                 │ Infrastructure  │ UserManagement.Infra...  │ 2          │
╰─────────────────┴─────────────────┴──────────────────────────┴────────────╯

Validating project references...

✓ No violations found!
```

### JSON Output

```json
{
  "summary": {
    "totalModules": 3,
    "totalProjects": 12,
    "errorCount": 0,
    "warningCount": 0,
    "isValid": true
  },
  "modules": [...],
  "violations": []
}
```

## Development

### Running Tests

```bash
dotnet test
```

### Project Structure

```
src/
├── CLI/
│   ├── Commands/          # CLI commands
│   ├── Models/            # Data models
│   ├── Services/          # Business logic
│   ├── Validation/        # Validation rules
│   └── Reporting/         # Export formats
tests/
└── CLI.Tests/            # Unit tests
```

## Architecture

The tool is built using:

- **Spectre.Console.Cli**: Command-line interface framework
- **Microsoft.Build**: MSBuild API for reading `.csproj` files
- **xUnit**: Testing framework

## Contributing

See [AGENTS.md](./AGENTS.md) for development principles and guidelines.

## Roadmap

See the `/roadmap` directory for detailed implementation phases.

## License

[Add your license here]
