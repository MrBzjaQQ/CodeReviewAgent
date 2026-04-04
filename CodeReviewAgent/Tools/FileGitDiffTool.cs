using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeReviewAgent.Tools;

public class FileGitDiffTool : IFileGitDiffTool
{
    public string ToolName => "FileGitDiffTool";

    public Task<string> RunToolAsync(params string[] parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            throw new ArgumentException("File path must be provided", nameof(parameters));
        }

        var filePath = parameters[0];
        var startCommit = parameters.Length > 1 && !string.IsNullOrEmpty(parameters[1]) ? parameters[1] : "HEAD~1";
        var endCommit = parameters.Length > 2 && !string.IsNullOrEmpty(parameters[2]) ? parameters[2] : "HEAD";
        var repoPath = parameters.Length > 3 && !string.IsNullOrEmpty(parameters[3]) ? parameters[3] : ".";
        
        try
        {
            var repoRoot = FindGitRoot(Path.GetFullPath(repoPath));
            
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.GetFullPath(Path.Combine(repoRoot, filePath));
            }
            
            var relativePath = Path.GetRelativePath(repoRoot, filePath).Replace('\\', '/');
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"diff {startCommit}..{endCommit} -- {relativePath}",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"[FileGitDiffTool] Error: {error}");
            }
            
            Console.WriteLine($"[FileGitDiffTool] Got git diff for {relativePath} ({output.Length} characters)");
            return Task.FromResult(output);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FileGitDiffTool] Error: {ex.Message}");
            return Task.FromResult(string.Empty);
        }
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

    private static string FilterDiffForFile(string fullDiff, string fileName)
    {
        if (string.IsNullOrEmpty(fullDiff))
            return string.Empty;

        var lines = fullDiff.Split('\n').ToList();
        var result = new List<string>();
        var inTargetFile = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("diff --git"))
            {
                inTargetFile = line.Contains(fileName);
            }

            if (inTargetFile)
            {
                result.Add(line);
            }
        }

        if (!result.Any())
        {
            foreach (var line in lines)
            {
                if (line.Contains(fileName))
                {
                    return fullDiff;
                }
            }
        }

        return string.Join("\n", result);
    }

    Task<object> ITool.RunToolAsync(params string[] parameters) => RunToolAsync(parameters).ContinueWith(t => (object)t.Result);
    Task<T> ITool.RunToolAsync<T>(params string[] parameters) => (Task<T>)(object)RunToolAsync(parameters);
}
