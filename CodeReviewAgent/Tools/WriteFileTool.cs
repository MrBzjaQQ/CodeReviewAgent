using System;
using System.IO;
using System.Threading.Tasks;

namespace CodeReviewAgent.Tools;

/// <summary>
/// Tool for writing content to a file
/// </summary>
public class WriteFileTool : IWriteFileTool
{
    /// <summary>
    /// Gets the name of the tool
    /// </summary>
    public string ToolName => "WriteFileTool";

    /// <summary>
    /// Runs the tool to write content to a file
    /// </summary>
    /// <param name="parameters">File path and content to write</param>
    /// <returns>True if successful, false otherwise</returns>
    public Task<bool> RunToolAsync(params string[] parameters)
    {
        if (parameters == null || parameters.Length < 2)
        {
            throw new ArgumentException("File path and content must be provided", nameof(parameters));
        }

        var filePath = parameters[0];
        var content = string.Join(Environment.NewLine, parameters, 1, parameters.Length - 1);
        
        try
        {
            var resolvedPath = ResolveFilePath(filePath);
            Console.WriteLine($"[WriteFileTool] Writing to file: {resolvedPath}");

            var directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(resolvedPath, content);
            Console.WriteLine($"[WriteFileTool] Successfully wrote {content.Length} characters");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WriteFileTool] Error: {ex.Message}");
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
