# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KismetKompiler is a decompiler and compiler tool for Unreal Engine 4 blueprints, specifically designed for Shin Megami Tensei V game assets. It converts blueprint assets to/from a C#-like syntax called KisMetScript (.kms).

## Technology Stack

- **Primary Language**: C# (.NET 8.0, Windows win-x64)
- **Parser**: ANTLR4 grammar (KismetScript.g4)
- **Build System**: MSBuild/Visual Studio solution
- **Dependencies**: UAssetAPI (git submodule), CommandLineParser, Microsoft.Extensions.Logging

## Development Commands

```bash
# Build the solution
dotnet build src/KismetKompiler.sln

# Build individual projects
dotnet build src/KismetKompiler/KismetKompiler.csproj
dotnet build src/KismetKompiler.Library/KismetKompiler.Library.csproj
dotnet build src/KismetKompiler.Test/KismetKompiler.Test.csproj

# Run tests
dotnet test

# Publish for distribution
dotnet publish src/KismetKompiler/KismetKompiler.csproj -c Release --self-contained -r win-x64

# CLI usage
KismetKompiler decompile -v 4.27 -i BP_BtlCalc.uasset
KismetKompiler compile -v 4.27 -i BP_BtlCalc.kms -o BP_BtlCalc_edit.uasset --asset BP_BtlCalc.uasset
```

## Architecture Overview

### Core Components

**KismetKompiler** (main executable) - CLI interface with compile/decompile commands

**KismetKompiler.Library** (core library) contains:
- **Parser/** - ANTLR grammar and parsing logic for KisMetScript language
- **Syntax/** - AST node definitions representing C#-like code structure
- **Compiler/** - Logic for compiling KisMetScript to blueprint assets
- **Decompiler/** - Logic for decompiling blueprints to KisMetScript
- **Linker/** - Processing and linking utilities
- **Utilities/** - Common helper classes

### Key Architectural Patterns

1. **Visitor Pattern** - Extensive use of generated visitors for AST traversal (source generators in place)
2. **Grammar-Based Parsing** - ANTLR4 grammar defines the KisMetScript language structure
3. **Separated Concerns** - Clear boundaries between parsing, compilation, and decompilation
4. **External Dependency Integration** - UAssetAPI submodule handles Unreal Engine file formats

### Important Directories

- `/src/KismetKompiler/` - Main CLI executable
- `/src/KismetKompiler.Library/` - Core business logic library
- `/external/UAssetAPI/` - Git submodule for Unreal Engine file handling
- `/samples/` - Example .kms files including real game assets
- `/tests/` - Test files and syntax verification

### Development Notes

- The project targets Windows win-x64 runtime for Unreal Engine compatibility
- Uses semantic versioning (currently 0.4.0-alpha)
- Git workflow includes automated publishing on tags via GitHub Actions
- Sample files in `/samples/` serve as integration tests and reference implementations
- The ANTLR grammar file (`KismetScript.g4`) defines the complete KisMetScript language specification