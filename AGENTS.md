# AGENTS.md

This document describes the AI agents and automation used in the ModularGuard project.

## Overview

ModularGuard is a CLI tool that validates project references in a modular monolith architecture. It ensures that C#
projects follow the architectural rules defined for the modular monolith structure.

## Development Principles

This project adheres to the following development standards:

- **C#/.NET Best Practices**: Follow established C# and .NET conventions, including naming conventions, code
  organization, and design patterns
- **Code Readability**: All code should be human-readable and easy to understand, with clear intent and minimal
  complexity
- **Maintainability**: Write code that is easy to modify, extend, and debug
- **Clean Code**: Use meaningful names, small focused methods, and clear abstractions

## Primary Agent: Project Reference Validator

### Purpose

Validates that project references between modules comply with the modular monolith architecture rules.

### Validation Rules

The agent enforces the following reference constraints based on project type:

#### Core Projects (`*.Core`)

- **Allowed references**: `Shared.Core`
- **Prohibited**: References to Infrastructure, App, or Endpoints projects

#### Infrastructure Projects (`*.Infrastructure`)

- **Allowed references**:
    - `Shared.Infrastructure`
    - Same module's `*.Core` project
- **Prohibited**: References to App or Endpoints projects

#### App Projects (`*.[Admin|Private|Public].App`)

- **Allowed references**:
    - `Shared.App.[Admin|Private|Public]` (matching the app type)
    - Same module's `*.Core` project
    - Same module's `*.Infrastructure` project
    - Other modules' `*.Shared.Events` projects
    - Other modules' `*.Shared.Messages` projects
- **Prohibited**: References to Endpoints projects

#### Endpoints Projects (`*.[Admin|Private|Public].Endpoints`)

- **Allowed references**:
    - Same module's `*.[Admin|Private|Public].App` project (matching the endpoint type)
- **Prohibited**: Direct references to Core or Infrastructure projects

#### Shared Events Projects (`*.Shared.Events`)

- **Allowed references**: `Shared.Events.Abstractions`

#### Shared Messages Projects (`*.Shared.Messages`)

- **Allowed references**: Message abstraction projects

### Technical Implementation

The CLI is built using:

- **Spectre.Console.Cli**: Command-line interface framework for parsing commands and arguments
- **Microsoft.Build.Evaluation.Project**: MSBuild API for reading and analyzing `.csproj` files

### Usage

```bash
dotnet run -- check <path-to-solution-directory>
```

The agent scans all `.csproj` files in the directory structure using `Microsoft.Build.Evaluation.Project` to parse
project references, then validates them according to the rules above.

### Error Reporting

The agent reports violations with:

- Source project path
- Invalid reference
- Reason for violation
- Suggested corrections

## Architecture Principles Enforced

1. **Separation of Concerns**: Core domain logic is isolated from infrastructure and application layers
2. **Dependency Direction**: Dependencies flow inward (Endpoints → App → Infrastructure → Core)
3. **Module Independence**: Modules can only communicate through defined contracts (Events, Messages)
4. **Shared Code Management**: Common code is centralized in the Shared module with specific usage patterns

## Future Agents

Potential agents for future implementation:

- **Circular Dependency Detector**: Identifies circular references between modules
- **Dependency Graph Visualizer**: Generates visual representations of module dependencies
- **Migration Assistant**: Helps refactor legacy code to comply with modular monolith rules
- **Test Coverage Validator**: Ensures all modules meet unit testing requirements
