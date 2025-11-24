# Code Review Agent

A CLI tool for automated code review using Language Model (LLM) that analyzes changes in Git repositories and generates detailed HTML reports.

## Features

- Analyzes git diff changes between commits
- Uses LLM to perform code reviews based on configurable rules
- Generates comprehensive HTML report with colored comments by severity
- Ignores common directories like node_modules, bin, obj
- Configurable review rules and settings
- Support for multiple programming languages

## Requirements

- .NET 9.0 SDK
- Git installed 
- LLM server (e.g., LM Studio)

## Installation

1. Clone or download this repository
2. Build the project using:
   ```bash
   dotnet build
   ```

## Usage

```bash
dotnet run --directory <path> [OPTIONS]
```

### Required Arguments
- `-d, --directory PATH` - Path to the repository directory to review

### Options
- `-s, --start-commit HASH` - Starting commit hash for comparison (optional)
- `-e, --end-commit HASH` - Ending commit hash for comparison (optional)
- `-o, --output FILE` - Output HTML file path (default: index.html)
- `--ignore-file PATH` - Path to .reviewignore file (default: ./.reviewignore)
- `--rules-file PATH` - Path to custom review rules template
- `--url URL` - LLM server base URL (default: http://localhost:1234)
- `--model NAME` - Model name for reviews (default: gemma-3-4b-it)
- `--temperature FLOAT` - Response creativity temperature (0.0-1.0, default: 0.7)
- `--max-size BYTES` - Maximum file size to process (default: 1048576 bytes)
- `--no-diff` - Don't include git diff in output
- `-h, --help` - Show help message

## Examples

Review current working directory changes:
```bash
dotnet run --directory .
```

Review specific commit range:
```bash
dotnet run --directory . --start-commit abc123 --end-commit def456
```

Custom output and configuration:
```bash
dotnet run --directory ./my-project -o review-report.html \
  --rules-file ./custom-rules.txt --temperature 0.5
```

## Configuration

The application looks for a `.reviewignore` file in the repository directory to exclude files from analysis:

```
# .reviewignore example
bin/
obj/
node_modules/
.git/
*.dll
*.exe
*.pdb
```

## Review Rules

By default, the tool uses comprehensive code review rules that cover:
- Code Quality & Best Practices
- Security Considerations  
- Performance & Efficiency
- Error Handling
- Testing & Maintainability

Custom rules can be provided via `--rules-file`.

## Output Format

The HTML report includes:
- Summary statistics 
- File-by-file breakdown with comments
- Severity-based color coding (Critical, Error, Warning, Info)
- Full file content and diff context
- Suggested improvements for each issue

## Architecture

```
Program.cs                 - Main entry point and CLI argument parsing
CodeReviewOptions.cs       - Configuration options model
GitDiffService.cs          - Git operations to get changed files
FileAnalysisService.cs     - Dependency analysis of code files
CodeReviewService.cs       - LLM integration for code review logic
LmStudioClient.cs          - Client implementation for LM Studio API
Templates/                 - HTML template and default rules
Utils/                     - Utility classes like ignore pattern matching
```

## Implementation Details

The application follows a multi-step process:
1. Parse CLI arguments and configuration
2. Get changed files from Git with diff information  
3. Analyze dependencies using file parsing techniques
4. Prepare review prompt with context
5. Send to LLM for code analysis
6. Parse structured response into comments
7. Generate HTML report

## Known Issues & Limitations

- Currently only supports LM Studio as the LLM provider (can be extended)
- The .NET 9.0 compilation may have some compatibility issues depending on environment
- Some complex parsing logic in dependency analysis could be improved for edge cases

## Future Improvements

- Support multiple LLM providers beyond LM Studio
- Enhanced code understanding through AST parsing  
- More sophisticated review rules and machine learning models
- Integration with popular IDEs (VS Code, JetBrains)
- Real-time collaboration features
