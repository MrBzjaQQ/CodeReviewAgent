using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CodeReviewAgent.Utils;

namespace CodeReviewAgent.Tools;

/// <summary>
/// Tool for reading the file tree structure
/// </summary>
public class ReadFileTreeTool : IReadFileTreeTool
{
    private readonly IgnorePatternMatcher _ignorePatternMatcher;

    /// <summary>
    /// Initializes a new instance of the ReadFileTreeTool class
    /// </summary>
    /// <param name="ignorePatternMatcher">Pattern matcher for ignoring files</param>
    public ReadFileTreeTool(IgnorePatternMatcher ignorePatternMatcher)
    {
        _ignorePatternMatcher = ignorePatternMatcher;
    }

    /// <summary>
    /// Gets the name of the tool
    /// </summary>
    public string ToolName => "ReadFileTreeTool";

    /// <summary>
    /// Runs the tool to get a list of files in the specified directory
    /// </summary>
    /// <param name="parameters">Directory path to scan</param>
    /// <returns>List of file paths</returns>
    public Task<List<string>> RunToolAsync(params string[] parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            throw new ArgumentException("Directory path must be provided", nameof(parameters));
        }

        var directoryPath = parameters[0];
        
        try
        {
            var resolvedPath = ResolveDirectoryPath(directoryPath);
            Console.WriteLine($"[ReadFileTreeTool] Reading file tree from: {resolvedPath}");

            if (!Directory.Exists(resolvedPath))
            {
                Console.WriteLine($"[ReadFileTreeTool] Error: Directory does not exist: {resolvedPath}");
                return Task.FromResult(new List<string>());
            }

            var files = new List<string>();
            var allFiles = Directory.GetFiles(resolvedPath, "*.*", SearchOption.AllDirectories);
            foreach (var file in allFiles)
            {
                if (!_ignorePatternMatcher.ShouldIgnore(file))
                {
                    files.Add(file);
                }
            }

            Console.WriteLine($"[ReadFileTreeTool] Found {files.Count} files");
            return Task.FromResult(files);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReadFileTreeTool] Error: {ex.Message}");
            return Task.FromResult(new List<string>());
        }
    }

    private static string ResolveDirectoryPath(string directoryPath)
    {
        if (Path.IsPathRooted(directoryPath))
        {
            return Path.GetFullPath(directoryPath);
        }

        var repoRoot = FindGitRoot(Directory.GetCurrentDirectory());
        
        var cleanedPath = directoryPath
            .Replace("CodeReviewAgent/", "")
            .Replace("CodeReviewAgent\\", "");
        
        while (cleanedPath.StartsWith("/") || cleanedPath.StartsWith("\\"))
            cleanedPath = cleanedPath.Substring(1);
        
        var path = Path.Combine(repoRoot, cleanedPath);
        
        if (Directory.Exists(path))
            return Path.GetFullPath(path);
        
        var altPath = Path.Combine(repoRoot, directoryPath);
        if (Directory.Exists(altPath))
            return Path.GetFullPath(altPath);
        
        var withCodeReview = Path.Combine(repoRoot, "CodeReviewAgent", cleanedPath);
        if (Directory.Exists(withCodeReview))
            return Path.GetFullPath(withCodeReview);
        
        return Path.GetFullPath(path);
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
