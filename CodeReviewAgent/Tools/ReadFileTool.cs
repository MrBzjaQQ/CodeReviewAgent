using System;
using System.IO;
using System.Threading.Tasks;

namespace CodeReviewAgent.Tools;

/// <summary>
/// Tool for reading file content
/// </summary>
public class ReadFileTool : IReadFileTool
{
    /// <summary>
    /// Gets the name of the tool
    /// </summary>
    public string ToolName => "ReadFileTool";

    /// <summary>
    /// Runs the tool to read the content of a file
    /// </summary>
    /// <param name="parameters">File path to read</param>
    /// <returns>File content as string</returns>
    public Task<string> RunToolAsync(params string[] parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            throw new ArgumentException("File path must be provided", nameof(parameters));
        }

        var filePath = parameters[0];
        
        try
        {
            var resolvedPath = ResolveFilePath(filePath);
            Console.WriteLine($"[ReadFileTool] Reading file: {resolvedPath}");

            if (!System.IO.File.Exists(resolvedPath))
            {
                Console.WriteLine($"[ReadFileTool] Error: File does not exist: {resolvedPath}");
                return Task.FromResult(string.Empty);
            }

            var content = System.IO.File.ReadAllText(resolvedPath);
            Console.WriteLine($"[ReadFileTool] Successfully read {content.Length} characters");
            return Task.FromResult(content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReadFileTool] Error: {ex.Message}");
            return Task.FromResult(string.Empty);
        }
    }

    private static string ResolveFilePath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
        {
            return Path.GetFullPath(filePath);
        }

        var repoRoot = FindGitRoot(Directory.GetCurrentDirectory());
        var possiblePaths = new[]
        {
            Path.Combine(repoRoot, filePath),
            Path.Combine(repoRoot, "CodeReviewAgent", filePath),
            Path.Combine(repoRoot, filePath.Replace("/", "\\").Replace("CodeReviewAgent\\", "CodeReviewAgent\\"))
        };

        foreach (var path in possiblePaths)
        {
            if (System.IO.File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        return Path.GetFullPath(possiblePaths[0]);
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
