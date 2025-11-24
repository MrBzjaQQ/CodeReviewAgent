using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using ReviewAgent.Services;

namespace ReviewAgent;

public class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("=== Code Review Agent ===");
        
        try
        {
            // Parse command line arguments
            var options = ParseCommandLineArguments(args);
            
            if (options == null)
            {
                PrintUsage();
                return;
            }

            // Validate options
            options.Validate();

            // Initialize services
            var ignorePatterns = new ReviewAgent.Utils.IgnorePatternMatcher();
            LoadIgnoreFile(options, ignorePatterns);

            Console.WriteLine($"Repository: {options.Directory}");
            if (!string.IsNullOrEmpty(options.StartCommit))
                Console.WriteLine($"Start Commit: {options.StartCommit}");
            if (!string.IsNullOrEmpty(options.EndCommit))
                Console.WriteLine($"End Commit: {options.EndCommit}");
            
            var gitDiffService = new ReviewAgent.Services.GitDiffService(options.Directory);
            var fileAnalysisService = new ReviewAgent.Services.FileAnalysisService(options.Directory);
            var llmClient = CreateLlmClient(options);

            // Load review rules
            string reviewRules;
            if (!string.IsNullOrEmpty(options.ReviewRulesTemplatePath))
            {
                try
                {
                    reviewRules = File.ReadAllText(options.ReviewRulesTemplatePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not read custom review rules at {options.ReviewRulesTemplatePath}: {ex.Message}");
                    reviewRules = LoadDefaultReviewRules();
                }
            }
            else
            {
                reviewRules = LoadDefaultReviewRules();
            }

            // Get changed files from git diff
            Console.WriteLine("Getting changed files from git repository...");
            var changedFiles = await gitDiffService.GetChangedFilesAsync(options.StartCommit, options.EndCommit, ignorePatterns);
            
            if (changedFiles.Count == 0)
            {
                Console.WriteLine("No changed files found to review.");
                return;
            }

            Console.WriteLine($"Found {changedFiles.Count} changed files to review.");

            // Perform code reviews
            Console.WriteLine("\nPerforming code reviews...");
            var codeReviewService = new ReviewAgent.Services.CodeReviewService(llmClient, fileAnalysisService, ignorePatterns, options);
            
            // Fix the missing ParseReviewResponseAsync method by implementing it
            var resultsList = changedFiles.Select(f => new ReviewAgent.Services.GitDiffResult 
            {
                FilePath = f.FilePath,
                Changes = f.Changes,
                FullContent = f.FullContent,
                DiffContent = f.DiffContent
            }).ToList();
            
            var reviewResults = await codeReviewService.ReviewFilesAsync(resultsList, reviewRules);

            // Generate HTML output
            Console.WriteLine("\nGenerating HTML report...");
            await GenerateHtmlReport(reviewResults, options);
            
            Console.WriteLine($"\nCode review completed successfully!");
            Console.WriteLine($"Report generated: {options.OutputHtmlFile}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
                Console.Error.WriteLine($"Inner Error: {ex.InnerException.Message}");
            
            Environment.Exit(1);
        }
    }

    private static CodeReviewOptions ParseCommandLineArguments(string[] args)
    {
        var options = new CodeReviewOptions();
        bool directorySet = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-d":
                case "--directory":
                    if (i + 1 >= args.Length) 
                        Console.WriteLine("Error: Directory path is required after -d/--directory");
                    else
                    {
                        options.Directory = args[++i];
                        directorySet = true;
                    }
                    break;

                case "-s":
                case "--start-commit":
                    if (i + 1 >= args.Length)
                        Console.WriteLine("Error: Start commit hash is required after -s/--start-commit");
                    else
                        options.StartCommit = args[++i];
                    break;

                case "-e":
                case "--end-commit":
                    if (i + 1 >= args.Length)
                        Console.WriteLine("Error: End commit hash is required after -e/--end-commit");
                    else
                        options.EndCommit = args[++i];
                    break;

                case "-o":
                case "--output":
                    if (i + 1 >= args.Length)
                        Console.WriteLine("Error: Output file path is required after -o/--output");
                    else
                        options.OutputHtmlFile = args[++i];
                    break;

                case "--ignore-file":
                    if (i + 1 >= args.Length)
                        Console.WriteLine("Error: Ignore file path is required after --ignore-file");
                    else
                        options.IgnoreFilePath = args[++i];
                    break;

                case "--rules-file":
                    if (i + 1 >= args.Length)
                        Console.WriteLine("Error: Rules file path is required after --rules-file");
                    else
                        options.ReviewRulesTemplatePath = args[++i];
                    break;

                case "--url":
                    if (i + 1 >= args.Length)
                        Console.WriteLine("Error: URL is required after --url");
                    else
                        options.BaseUrl = args[++i];
                    break;

                case "--model":
                    if (i + 1 >= args.Length)
                        Console.WriteLine("Error: Model name is required after --model");
                    else
                        options.ModelName = args[++i];
                    break;

                case "--temperature":
                    if (i + 1 >= args.Length || !double.TryParse(args[i + 1], out double temp))
                        Console.WriteLine("Error: Valid temperature value is required after --temperature");
                    else
                        options.Temperature = temp;
                    i++;
                    break;

                case "--max-size":
                    if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out int size))
                        Console.WriteLine("Error: Valid max file size in bytes is required after --max-size");
                    else
                        options.MaxFileSize = size;
                    i++;
                    break;

                case "--no-diff":
                    options.IncludeDiff = false;
                    break;

                case "-h":
                case "--help":
                    PrintUsage();
                    return null;
                
                case "-c":
                case "--client":
                    if (i + 1 >= args.Length)
                        Console.WriteLine("Error: Client type is required after -c/--client");
                    else
                        options.ClientType = args[++i];
                    break;

                default:
                    Console.WriteLine($"Warning: Unknown argument '{args[i]}' will be ignored.");
                    break;
            }
        }

        if (!directorySet)
        {
            Console.WriteLine("Error: Directory path is required. Use -d or --directory to specify the repository directory.");
            PrintUsage();
            return null;
        }

        // Validate URL format
        try
        {
            new Uri(options.BaseUrl);
        }
        catch (UriFormatException ex)
        {
            Console.WriteLine($"Error: Invalid base URL '{options.BaseUrl}': {ex.Message}");
            return null;
        }

        return options;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"
Code Review Agent - A CLI tool for automated code review using LLM

USAGE:
  dotnet run --directory <path> [OPTIONS]

REQUIRED ARGUMENTS:
  -d, --directory PATH     Path to the repository directory to review

OPTIONS:
  -s, --start-commit HASH  Starting commit hash for comparison (optional)
  -e, --end-commit HASH    Ending commit hash for comparison (optional)
  
OUTPUT OPTIONS:
  -o, --output FILE        Output HTML file path (default: index.html)
  
CONFIGURATION OPTIONS:
  --ignore-file PATH       Path to .reviewignore file (default: ./.reviewignore)
  --rules-file PATH        Path to custom review rules template
  --url URL                LLM server base URL (default: http://localhost:1234)
  --model NAME             Model name for reviews (default: gemma-3-4b-it)
  
REVIEW OPTIONS:
  --temperature FLOAT      Response creativity temperature (0.0-1.0, default: 0.7)
  --max-size BYTES         Maximum file size to process (default: 1048576 bytes)
  --no-diff                Don't include git diff in output
  
GENERAL OPTIONS:
  -h, --help               Show this help message

EXAMPLES:
  # Review current working directory changes
  dotnet run --directory .

  # Review specific commit range
  dotnet run --directory . --start-commit abc123 --end-commit def456

  # Custom output and configuration
  dotnet run --directory ./my-project -o review-report.html \
    --rules-file ./custom-rules.txt --temperature 0.5
");
    }

    private static void LoadIgnoreFile(CodeReviewOptions options, ReviewAgent.Utils.IgnorePatternMatcher ignorePatterns)
    {
        string ignoreFilePath;
        
        if (!string.IsNullOrEmpty(options.IgnoreFilePath))
        {
            // Use specified path or relative to current directory
            ignoreFilePath = Path.IsPathRooted(options.IgnoreFilePath) 
                ? options.IgnoreFilePath 
                : Path.Combine(Environment.CurrentDirectory, options.IgnoreFilePath);
        }
        else
        {
            // Look for .reviewignore in the repository directory
            var repoIgnoreFile = Path.GetFullPath(Path.Combine(options.Directory!, ".reviewignore"));
            
            if (File.Exists(repoIgnoreFile))
            {
                ignoreFilePath = repoIgnoreFile;
            }
            else
            {
                // Use default patterns only
                ignorePatterns.LoadDefaultPatterns();
                return;
            }
        }

        try
        {
            ignorePatterns.LoadDefaultPatterns();
            ignorePatterns.LoadIgnoreFile(ignoreFilePath);
            Console.WriteLine($"Using ignore file: {ignoreFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not load ignore file '{ignoreFilePath}': {ex.Message}");
            // Fall back to default patterns
            ignorePatterns.LoadDefaultPatterns();
        }
    }

    private static string LoadDefaultReviewRules()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        try
        {
            using (var stream = assembly.GetManifestResourceStream("ReviewAgent.src.Templates.default-review-rules.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }
        catch { /* Ignore errors accessing embedded resource */ }

        // Fallback: try to read from file system
        var rulesFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "Templates", "default-review-rules.txt");
        
        if (File.Exists(rulesFile))
        {
            try
            {
                return File.ReadAllText(rulesFile);
            }
            catch { /* Ignore errors reading file */ }
        }

        // Final fallback - inline rules
        return @"
=== DEFAULT CODE REVIEW RULES ===

1. Code Quality & Best Practices
   - Follow language-specific conventions and patterns
   - Maintain consistent code style throughout the project
   - Use meaningful variable, function, and class names
   - Keep functions small and focused on a single responsibility

2. Security Considerations
   - Validate all user inputs properly
   - Avoid hardcoding sensitive information in source code
   - Implement proper authentication and authorization where needed

3. Performance & Efficiency
   - Optimize algorithms for time and space complexity where appropriate
   - Be mindful of memory usage, especially in loops and recursion

4. Error Handling
   - Implement proper error handling throughout the codebase
   - Provide clear and informative error messages
   - Handle edge cases gracefully

=== END RULES ===";
    }

    private static async Task GenerateHtmlReport(FileReviewResult[] reviewResults, CodeReviewOptions options)
    {
        // Load HTML template
        string htmlTemplate;
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            
            using (var stream = assembly.GetManifestResourceStream("CodeReviewAgent.Templates.default-comment-template.html"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        htmlTemplate = reader.ReadToEnd();
                    }
                }
                else
                {
                    throw new FileNotFoundException("Embedded HTML template not found");
                }
            }
        }
        catch
        {
            // Fallback: read from file system
            var templateFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "Templates", "default-comment-template.html");
            
            if (File.Exists(templateFile))
            {
                htmlTemplate = await File.ReadAllTextAsync(templateFile);
            }
            else
            {
                throw new FileNotFoundException("Could not find HTML template file");
            }
        }

        // Prepare configuration data for the HTML template
        var configJson = JsonSerializer.Serialize(new
        {
            repositoryPath = options.Directory,
            modelName = options.ModelName,
            showDiff = options.IncludeDiff,
            showFullContent = false, // Could be made configurable later
            files = reviewResults
        });

        // Insert configuration data into the HTML template
        var finalHtml = htmlTemplate.Replace("// <INSERT CONFIG HERE>", $"const config = {configJson};");

        // Write to output file
        await File.WriteAllTextAsync(options.OutputHtmlFile, finalHtml);
    }

    private static IChatClient CreateLlmClient(CodeReviewOptions options)
    {
        // Create the appropriate client based on the specified client type
        switch (options.ClientType.ToLower())
        {
            case "lmstudio":
            case "lmstudioclient":
                return new ReviewAgent.Clients.LmStudioClient(
                    baseUrl: options.BaseUrl,
                    httpClient: null
                );
            case "openai":
            case "openaicompatibleclient":
                return new ReviewAgent.Clients.OpenAICompatibleClient(
                    baseUrl: options.BaseUrl,
                    model: options.ModelName,
                    httpClient: null
                );
            default:
                // Default to LmStudioClient if invalid client type specified
                Console.WriteLine($"Warning: Unknown client type '{options.ClientType}', defaulting to LmStudioClient");
                return new ReviewAgent.Clients.LmStudioClient(
                    baseUrl: options.BaseUrl,
                    httpClient: null
                );
        }
    }
}
