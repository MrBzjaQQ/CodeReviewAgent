using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CodeReviewAgent.Tools;

/// <summary>
/// Tool for searching and replacing content in a file
/// </summary>
public class SearchAndReplaceTool : ISearchAndReplaceTool
{
    /// <summary>
    /// Gets the name of the tool
    /// </summary>
    public string ToolName => "SearchAndReplaceTool";

    /// <summary>
    /// Runs the tool to search and replace content in a file
    /// </summary>
    /// <param name="parameters">File path, search pattern, and replacement</param>
    /// <returns>True if successful, false otherwise</returns>
    public Task<bool> RunToolAsync(params string[] parameters)
    {
        if (parameters == null || parameters.Length < 3)
        {
            throw new ArgumentException("File path, search pattern, and replacement must be provided", nameof(parameters));
        }

        var filePath = parameters[0];
        var searchPattern = parameters[1];
        var replacement = string.Join(Environment.NewLine, parameters, 2, parameters.Length - 2);
        
        try
        {
            var resolvedPath = ResolveFilePath(filePath);
            Console.WriteLine($"[SearchAndReplaceTool] Searching and replacing in: {resolvedPath}");

            if (!File.Exists(resolvedPath))
            {
                Console.WriteLine($"[SearchAndReplaceTool] Error: File does not exist: {resolvedPath}");
                return Task.FromResult(false);
            }

            var content = File.ReadAllText(resolvedPath);
            var newContent = Regex.Replace(content, searchPattern, replacement);
            var replacementsMade = !(content == newContent);

            if (replacementsMade)
            {
                File.WriteAllText(resolvedPath, newContent);
                Console.WriteLine($"[SearchAndReplaceTool] Successfully made replacements");
            }
            else
            {
                Console.WriteLine($"[SearchAndReplaceTool] No matches found for pattern");
            }

            return Task.FromResult(replacementsMade);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SearchAndReplaceTool] Error: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    private static string ResolveFilePath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
        {
            return Path.GetFullPath(filePath);
        }

        var repoRoot = FindGitRoot(Directory.GetCurrentDirectory());
        
        var cleanedPath = filePath
            .Replace("CodeReviewAgent/", "")
            .Replace("CodeReviewAgent\\", "");
        
        return Path.GetFullPath(Path.Combine(repoRoot, cleanedPath));
    }

    private static string FindGitRoot(string startPath)
    {
        var current = startPath;
        while (!string.IsNullOrEmpty(current))
        {
            var gitDir = Path.Combine(current, ".git");
            if (Directory.Exists(gitDir) || File.Exists(gitDir))
            {
                return current;
            }
            var parent = Path.GetDirectoryName(current);
            if (parent == current)
                break;
            current = parent;
        }
        return startPath;
    }

    Task<object> ITool.RunToolAsync(params string[] parameters) => RunToolAsync(parameters).ContinueWith(t => (object)t.Result);
    Task<T> ITool.RunToolAsync<T>(params string[] parameters) => (Task<T>)(object)RunToolAsync(parameters);
}
