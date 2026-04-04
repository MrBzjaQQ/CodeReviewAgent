using System.ComponentModel;
using CodeReviewAgent.Context;
using CodeReviewAgent.Factories;
using CodeReviewAgent.Tools;
using CodeReviewAgent.Utils;
using Microsoft.Extensions.AI;

namespace CodeReviewAgent.Services;

public class CodeReviewService
{
    private readonly IChatClient _chatClient;
    private readonly IContextService _contextService;
    private readonly IToolFactory _toolFactory;
    private readonly IgnorePatternMatcher _ignorePatternMatcher;
    private readonly string _startCommit;
    private readonly string _endCommit;
    private readonly string _repositoryPath;

    public CodeReviewService(
        IChatClient chatClient,
        IContextService contextService,
        IToolFactory toolFactory,
        IgnorePatternMatcher ignorePatternMatcher,
        string? startCommit = null,
        string? endCommit = null,
        string? repositoryPath = null)
    {
        _chatClient = chatClient;
        _contextService = contextService;
        _toolFactory = toolFactory;
        _ignorePatternMatcher = ignorePatternMatcher;
        _startCommit = startCommit ?? "HEAD~1";
        _endCommit = endCommit ?? "HEAD";
        _repositoryPath = repositoryPath ?? ".";
    }

    public async Task PerformCodeReviewAsync(List<string> filesToReview)
    {
        Console.WriteLine("Starting code review process...");

        foreach (var file in filesToReview)
        {
            if (_ignorePatternMatcher.ShouldIgnore(file))
            {
                Console.WriteLine($"Skipping ignored file: {file}");
                continue;
            }

            Console.WriteLine($"Reviewing file: {file}");
            await ReviewFileAsync(file).ConfigureAwait(false);
        }

        Console.WriteLine("Code review process completed.");
    }

    private async Task ReviewFileAsync(string filePath)
    {
        var gitDiffTool = (IFileGitDiffTool)_toolFactory.CreateTool("FileGitDiffTool");
        var diff = await gitDiffTool.RunToolAsync(filePath, _startCommit, _endCommit, _repositoryPath).ConfigureAwait(false);

        if (string.IsNullOrEmpty(diff))
        {
            Console.WriteLine($"No changes detected in {filePath}, skipping review.");
            return;
        }

        var prompt = BuildPrompt(filePath, diff);
        var resultFile = Path.Combine(Path.GetDirectoryName(filePath) ?? ".", "review-result.md");

        await ExecuteToolCycleAsync(prompt, resultFile).ConfigureAwait(false);
    }

    private string BuildPrompt(string filePath, string diff)
    {
        var fileTree = _contextService.GetFileTree();
        var systemPrompt = _contextService.GetSystemPrompt();
        var toolsDescription = _contextService.GetToolsDescription();
        var additionalInstructions = _contextService.GetAdditionalInstructions();
        var responsePattern = _contextService.GetResponsePattern();

        var relativeFilePath = GetRelativePath(filePath);

        return $@"
# Code Review Task

## General Task Description
{systemPrompt}

## Available Tools
{toolsDescription}

## File Tree
{string.Join(Environment.NewLine, fileTree.Take(50))}
(Showing first 50 files)

## Changed File
File: {relativeFilePath}
Diff:
{diff}

## Additional Instructions
{additionalInstructions}

## Response Pattern
{responsePattern}
";
    }

    private string GetRelativePath(string fullPath)
    {
        if (string.IsNullOrEmpty(_repositoryPath) || _repositoryPath == ".")
            return fullPath;

        try
        {
            var fullRepoPath = Path.GetFullPath(_repositoryPath);
            return Path.GetRelativePath(fullRepoPath, fullPath).Replace('\\', '/');
        }
        catch
        {
            return fullPath;
        }
    }

    private async Task ExecuteToolCycleAsync(string prompt, string resultFile)
    {
        var tools = CreateAIFunctions();
        
        var chatClient = _chatClient.AsBuilder()
            .UseFunctionInvocation()
            .Build();

        var chatOptions = new ChatOptions
        {
            MaxOutputTokens = 4000,
            Tools = tools.Cast<AITool>().ToList()
        };

        Console.WriteLine("Sending request to LLM with tools...");

        var maxIterations = 10;
        var iteration = 0;
        var lastContent = prompt;

        while (iteration < maxIterations)
        {
            iteration++;
            Console.WriteLine($"\n=== Tool Cycle Iteration {iteration} ===");

            var response = await chatClient.GetResponseAsync(lastContent, chatOptions).ConfigureAwait(false);
            
            var assistantMessage = response.Messages.LastOrDefault(m => m.Role == ChatRole.Assistant);
            if (assistantMessage == null)
            {
                Console.WriteLine("No assistant response received.");
                break;
            }

            var content = assistantMessage.Text;
            Console.WriteLine($"LLM Response: {content?.Substring(0, Math.Min(500, content.Length))}...");

            if (string.IsNullOrEmpty(content))
            {
                Console.WriteLine("Empty response. Review complete.");
                break;
            }

            var toolResults = await ParseAndExecuteToolsAsync(content).ConfigureAwait(false);
            
            if (string.IsNullOrEmpty(toolResults))
            {
                Console.WriteLine("No tool calls in response. Review complete.");
                break;
            }

            lastContent = content + "\n\n" + toolResults;
        }

        if (iteration >= maxIterations)
        {
            Console.WriteLine("Reached maximum iterations.");
        }
    }

    private async Task<string> ParseAndExecuteToolsAsync(string content)
    {
        var results = new List<string>();
        var toolCalls = ParseToolCalls(content);

        if (!toolCalls.Any())
        {
            return string.Empty;
        }

        foreach (var (toolName, args) in toolCalls)
        {
            try
            {
                var result = await ExecuteToolAsync(toolName, args).ConfigureAwait(false);
                results.Add($"[Tool Result for {toolName}]: {result}");
                Console.WriteLine($"Tool '{toolName}' executed successfully.");
            }
            catch (Exception ex)
            {
                results.Add($"[Tool Error for {toolName}]: {ex.Message}");
                Console.WriteLine($"Tool '{toolName}' failed: {ex.Message}");
            }
        }

        return string.Join("\n\n", results);
    }

    private List<(string Name, string[] Args)> ParseToolCalls(string content)
    {
        var toolCalls = new List<(string Name, string[] Args)>();
        
        var patterns = new List<(string Pattern, string Name)>
        {
            (@"ReadFileTreeTool\s*\(\s*""([^""]*)""\s*\)", "ReadFileTreeTool"),
            (@"ReadFileTool\s*\(\s*""([^""]*)""\s*\)", "ReadFileTool"),
            (@"WriteFileTool\s*\(\s*""([^""]*)""\s*,\s*(.+?)\)\s*\)", "WriteFileTool"),
            (@"SearchAndReplaceTool\s*\(\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*""([^""]*)""\s*\)", "SearchAndReplaceTool"),
            (@"FileGitDiffTool\s*\(\s*""([^""]*)""\s*\)", "FileGitDiffTool"),
            
            (@"<ReadFileTreeTool[^>]*>[\s\n]*<Directory>([^<]*)</Directory>", "ReadFileTreeTool"),
            (@"<ReadFileTreeTool[^>]*>[\s\n]*<Path>([^<]*)</Path>", "ReadFileTreeTool"),
            (@"<ReadFileTreeTool[^>]*directory[^>]*=""([^""]*)""", "ReadFileTreeTool"),
            (@"<ReadFileTool[^>]*>[\s\n]*<File>([^<]*)</File>", "ReadFileTool"),
            (@"<ReadFileTool[^>]*>[\s\n]*<FilePath>([^<]*)</FilePath>", "ReadFileTool"),
            (@"<ReadFileTool[^>]*file[^>]*=""([^""]*)""", "ReadFileTool"),
            (@"<WriteFileTool[^>]*file[^>]*=""([^""]*)""", "WriteFileTool"),
            (@"<FileGitDiffTool[^>]*file[^>]*=""([^""]*)""", "FileGitDiffTool"),
            
            (@"<tool_call>\s*(\w+)\s*\(\s*""([^""]*)""\s*\)", "ReadFileTool"),
        };

        foreach (var (pattern, defaultTool) in patterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var toolName = defaultTool;
                if (match.Groups.Count > 2)
                {
                    var possibleName = match.Groups[1].Value;
                    if (possibleName.EndsWith("Tool"))
                        toolName = possibleName;
                }
                
                var args = match.Groups.Cast<System.Text.RegularExpressions.Group>().Skip(1).Select(g => g.Value.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                
                args = args.Select(a => {
                    var cleaned = a.Replace("CodeReviewAgent/", "").Replace("CodeReviewAgent\\", "");
                    while (cleaned.StartsWith("/") || cleaned.StartsWith("\\"))
                        cleaned = cleaned.Substring(1);
                    return cleaned;
                }).ToArray();
                
                toolCalls.Add((toolName, args));
            }
        }

        return toolCalls;
    }

    private async Task<string> ExecuteToolAsync(string toolName, string[] args)
    {
        return toolName switch
        {
            "ReadFileTreeTool" => string.Join("\n", await ((IReadFileTreeTool)_toolFactory.CreateTool("ReadFileTreeTool")).RunToolAsync(args)),
            "ReadFileTool" => await ((IReadFileTool)_toolFactory.CreateTool("ReadFileTool")).RunToolAsync(args),
            "WriteFileTool" => ((IWriteFileTool)_toolFactory.CreateTool("WriteFileTool")).RunToolAsync(args).Result ? "File written successfully" : "Failed to write file",
            "SearchAndReplaceTool" => ((ISearchAndReplaceTool)_toolFactory.CreateTool("SearchAndReplaceTool")).RunToolAsync(args).Result ? "Replacements made" : "No replacements made",
            "FileGitDiffTool" => await ((IFileGitDiffTool)_toolFactory.CreateTool("FileGitDiffTool")).RunToolAsync(args),
            _ => $"Unknown tool: {toolName}"
        };
    }

    private List<AIFunction> CreateAIFunctions()
    {
        var functions = new List<AIFunction>();

        var readFileTreeTool = (IReadFileTreeTool)_toolFactory.CreateTool("ReadFileTreeTool");
        functions.Add(AIFunctionFactory.Create(
            new ReadFileTreeDelegate(path => readFileTreeTool.RunToolAsync(path)),
            "ReadFileTreeTool"));

        var readFileTool = (IReadFileTool)_toolFactory.CreateTool("ReadFileTool");
        functions.Add(AIFunctionFactory.Create(
            new ReadFileDelegate(path => readFileTool.RunToolAsync(path)),
            "ReadFileTool"));

        var writeFileTool = (IWriteFileTool)_toolFactory.CreateTool("WriteFileTool");
        functions.Add(AIFunctionFactory.Create(
            new WriteFileDelegate((file, content) => writeFileTool.RunToolAsync(file, content)),
            "WriteFileTool"));

        var searchAndReplaceTool = (ISearchAndReplaceTool)_toolFactory.CreateTool("SearchAndReplaceTool");
        functions.Add(AIFunctionFactory.Create(
            new SearchAndReplaceDelegate((file, search, replace) => searchAndReplaceTool.RunToolAsync(file, search, replace)),
            "SearchAndReplaceTool"));

        var fileGitDiffTool = (IFileGitDiffTool)_toolFactory.CreateTool("FileGitDiffTool");
        functions.Add(AIFunctionFactory.Create(
            new FileGitDiffDelegate(path => fileGitDiffTool.RunToolAsync(path)),
            "FileGitDiffTool"));

        return functions;
    }

    private delegate Task<List<string>> ReadFileTreeDelegate(string path);
    private delegate Task<string> ReadFileDelegate(string path);
    private delegate Task<bool> WriteFileDelegate(string file, string content);
    private delegate Task<bool> SearchAndReplaceDelegate(string file, string searchPattern, string replacement);
    private delegate Task<string> FileGitDiffDelegate(string path);
}
