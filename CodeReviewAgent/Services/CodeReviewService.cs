using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using System.Threading.Tasks;

namespace ReviewAgent.Services;

public class CodeReviewService
{
    private readonly IChatClient _llmClient;
    private readonly FileAnalysisService _fileAnalysisService;
    private readonly Utils.IgnorePatternMatcher _ignorePatterns;
    private readonly CodeReviewOptions _options;
    
    public CodeReviewService(IChatClient llmClient, FileAnalysisService fileAnalysisService, Utils.IgnorePatternMatcher ignorePatterns, CodeReviewOptions options)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _fileAnalysisService = fileAnalysisService ?? throw new ArgumentNullException(nameof(fileAnalysisService));
        _ignorePatterns = ignorePatterns ?? throw new ArgumentNullException(nameof(ignorePatterns));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
    
    public async Task<FileReviewResult[]> ReviewFilesAsync(List<GitDiffResult> files, string reviewRules)
    {
        var results = new List<FileReviewResult>();
        
        foreach (var file in files)
        {
            try
            {
                // Check if the file should be ignored based on patterns
                if (_ignorePatterns.ShouldIgnore(file.FilePath))
                {
                    Console.WriteLine($"Skipping ignored file: {file.FilePath}");
                    continue;
                }
                
                // Get dependencies for this file (e.g., related class definitions)
                var dependenciesDict = await _fileAnalysisService.GetRelatedDependenciesAsync(file.FilePath, file.FullContent);
                
                // Prepare the review prompt
                var reviewPrompt = BuildReviewPrompt(file, dependenciesDict, reviewRules);
                
                Console.WriteLine($"Reviewing {file.FilePath}...");
                
                // Get LLM response for code review
                var messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.User, reviewPrompt)
                };
                
                try
                {
                    var chatOptions = new ChatOptions 
                    { 
                        ModelId = _options.ModelName,
                        Temperature = (float)_options.Temperature,
                        // AdditionalProperties is of type AdditionalPropertiesDictionary, not Dictionary<string, object>
                        AdditionalProperties = new Microsoft.Extensions.AI.AdditionalPropertiesDictionary()
                        {
                            ["max_tokens"] = 2000
                        }
                    };
                    ReviewComments comments = new ReviewComments();
                    for (int retryAttempt = 0; retryAttempt < 3; retryAttempt++)
                    {
                        try
                        {
                            // Get review response from LLM
                            var response = await _llmClient.GetResponseAsync(messages, chatOptions);

                            // Parse the response to extract structured comments - use ToString() for now as a fallback
                            var content = "";
                            if (response is ChatResponse chatResponse)
                            {
                                content = string.Join("", chatResponse.Messages.Select(m => m.Text ?? ""));
                            }
                            else
                            {
                                content = response.ToString();
                            }

                            comments = JsonSerializer.Deserialize<ReviewComments>(content, JsonSerializerOptions.Web);
                        }
                        catch
                        {
                            // Только последняя попытка идёт в список ошибок
                            if (retryAttempt == 2)
                            {
                                throw;
                            }
                        }
                    }

                    // Calculate an overall score (0-100)
                    var overallScore = CalculateOverallScore(comments.Comments.ToArray());
                    
                    results.Add(new FileReviewResult 
                    {
                        FilePath = file.FilePath,
                        Comments = comments.Comments,
                        OverallScore = overallScore,
                        DiffContent = file.DiffContent,
                        FullContent = file.FullContent,
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reviewing {file.FilePath}: {ex.Message}");
                    
                    // Add error as a comment in the review result
                    results.Add(new FileReviewResult 
                    {
                        FilePath = file.FilePath,
                        Comments = 
                        [ 
                            new ReviewComment 
                            { 
                                Line = 1, 
                                Severity = "Error", 
                                Message = $"Could not complete code review: {ex.Message}" 
                            } 
                        ],
                        OverallScore = 0
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {file.FilePath}: {ex.Message}");
                
                // Add error as a comment in the review result
                results.Add(new FileReviewResult 
                {
                    FilePath = file.FilePath,
                    Comments =
                        [ 
                            new ReviewComment 
                            { 
                                Line = 1, 
                                Severity = "Error", 
                                Message = $"Could not process file: {ex.Message}" 
                            } 
                        ],
                    OverallScore = 0
                });
            }
        }
        
        return results.ToArray();
    }
    
    private string BuildReviewPrompt(GitDiffResult file, Dictionary<string, string> dependencies, string reviewRules)
    {
        var sb = new StringBuilder();
        
        // Add system context and rules
        sb.AppendLine("=== CODE REVIEW CONTEXT ===");
        sb.AppendLine($"REPOSITORY: {System.IO.Path.GetDirectoryName(file.FilePath)}");
        sb.AppendLine($"FILE: {file.FilePath}");
        sb.AppendLine("LANGUAGE: " + System.IO.Path.GetExtension(file.FilePath).TrimStart('.')?.ToUpper() ?? "UNKNOWN");
        if (dependencies.Count > 0)
        {
            sb.AppendLine("DEPENDENCIES FOUND:");
            foreach (var dep in dependencies.Take(5)) // Limit to first 5 for context
                sb.AppendLine($" - {dep.Key}: {dep.Value}");
            if (dependencies.Count > 5)
                sb.AppendLine($" ... and {dependencies.Count - 5} more dependencies");
        }
        sb.AppendLine();
        
        // Add review rules
        sb.AppendLine("=== REVIEW RULES ===");
        sb.AppendLine(reviewRules);
        sb.AppendLine();
        
        // Add git diff context
        if (file.Changes.Count != 0)
        {
            sb.AppendLine("=== GIT DIFF CONTEXT ===");
            sb.AppendLine("The following changes were made to this file:");
            sb.AppendLine("```diff");
            sb.AppendLine(string.Join("\n", file.Changes));
            sb.AppendLine("```");
            sb.AppendLine();
        }
        
        // Add full file content
        sb.AppendLine("=== FILE CONTENT ===");
        if (!string.IsNullOrEmpty(file.FullContent))
        {
            var lines = file.FullContent.Split('\n');
            
            if (lines.Length > 0)
            {
                // Find line numbers for the diff context to highlight
                int startLine = 1;
                int endLine = lines.Length;
                
                if (file.Changes.Count > 0)
                {
                    try
                    {
                        // Try to extract line ranges from diff header like @@ -10,5 +12,7 @@
                        var diffHeaderPattern = new System.Text.RegularExpressions.Regex(@"@@ \-(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@", 
                            System.Text.RegularExpressions.RegexOptions.Multiline);
                        
                        var match = diffHeaderPattern.Match(string.Join("\n", file.Changes));
                        if (match.Success)
                        {
                            startLine = int.Parse(match.Groups[2].Value); // New file line number
                            endLine = Math.Min(startLine + 10, lines.Length); // Show context around changes
                        }
                    }
                    catch { /* Ignore parsing errors */ }
                }
                
                // Show relevant context from the file
                sb.AppendLine("```csharp");
                for (int i = Math.Max(0, startLine - 3); i < Math.Min(lines.Length, endLine + 3); i++)
                {
                    // Add line numbers
                    var lineNumber = i + 1;
                    sb.AppendLine($"{lineNumber:D4}: {lines[i]}");
                }
                sb.AppendLine("```");
            }
        }
        
        // Review request
        sb.AppendLine();
        sb.AppendLine("=== REVIEW REQUEST ===");
        sb.AppendLine("Please provide a detailed code review of this file based on the rules provided above.");
        sb.AppendLine("Focus on:");
        sb.AppendLine("- Code quality and best practices");
        sb.AppendLine("- Potential security issues");
        sb.AppendLine("- Performance considerations");
        sb.AppendLine("- Error handling");
        sb.AppendLine("- Maintainability");
        sb.AppendLine();
        sb.AppendLine("Format your response as follows (JSON):");
        sb.AppendLine(@"{");
        sb.AppendLine("  \"comments\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"line\": <line_number>,");
        sb.AppendLine("      \"severity\": \"Info|Warning|Error|Critical\",");
        sb.AppendLine("      \"message\": \"<specific description of the issue>\",");
        sb.AppendLine("      \"suggestion\": \"<how to improve or fix>\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();
        
        // Add system note about the review scope
        sb.AppendLine("=== SYSTEM NOTE ===");
        sb.AppendLine("Review only the shown code context and highlighted changes.");
        sb.AppendLine("Be constructive with your feedback - suggest specific improvements when issues are found.");
        sb.AppendLine("Focus on high-impact issues first (Critical > Error > Warning > Info).");
        
        return sb.ToString();
    }
    
    private int FindMatchingClosingBrace(ReadOnlySpan<char> span, int openIndex)
    {
        if (openIndex < 0 || openIndex >= span.Length || span[openIndex] != '{')
            throw new ArgumentException("Invalid opening brace position");
            
        var depth = 1;
        
        for (int i = openIndex + 1; i < span.Length; i++)
        {
            switch (span[i])
            {
                case '{':
                    depth++;
                    break;
                    
                case '}':
                    depth--;
                    if (depth == 0)
                        return i;
                    break;
            }
        }
        
        return -1; // No matching brace found
    }
    
    private int CalculateOverallScore(ReviewComment[] comments)
    {
        if (comments.Length == 0)
            return 100; // Perfect score for no issues
            
        var severityWeights = new Dictionary<string, double>
        {
            ["Critical"] = 10.0,
            ["Error"] = 5.0,
            ["Warning"] = 2.0,
            ["Info"] = 1.0
        };
        
        double totalPenalty = 0;
        
        foreach (var comment in comments)
        {
            if (severityWeights.TryGetValue(comment.Severity, out var weight))
                totalPenalty += weight;
                
            // Each issue is at least a penalty of 1
            totalPenalty = Math.Max(totalPenalty, comments.Length);
        }
        
        // Base score of 100, minus normalized penalties
        var maxPossibleScore = 100.0;
        var minPossibleScore = 20.0; // Even with many issues, don't go to zero
        
        var score = maxPossibleScore - Math.Min(totalPenalty * 2, maxPossibleScore - minPossibleScore);
        
        return (int)Math.Max(minPossibleScore, Math.Round(score));
    }
}
