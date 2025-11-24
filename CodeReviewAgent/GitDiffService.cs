using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ReviewAgent.Services;

public class GitDiffResult
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> Changes { get; set; } = new();
    public string DiffContent { get; set; } = string.Empty;
    public string FullContent { get; set; } = string.Empty;
}

public class GitDiffService
{
    private readonly string _repositoryPath;

    public GitDiffService(string repositoryPath)
    {
        if (!Directory.Exists(repositoryPath))
            throw new DirectoryNotFoundException($"Repository not found: {repositoryPath}");
            
        _repositoryPath = repositoryPath;
    }

    public async Task<List<GitDiffResult>> GetChangedFilesAsync(string? startCommit, string? endCommit, ReviewAgent.Utils.IgnorePatternMatcher ignorePatterns)
    {
        var result = new List<GitDiffResult>();
        
        try
        {
            // Determine git diff command based on provided commits
            string startCommitOrDefault = startCommit ?? "HEAD~1";
            string endCommitOrDefault = endCommit ?? "HEAD";
            string diffCommand = $"diff {startCommitOrDefault} {endCommitOrDefault}";

            // Run git diff command to get list of changed files
            var fileDiffResult = await ExecuteGitCommandAsync($"{diffCommand} --name-only");
            
            if (string.IsNullOrEmpty(fileDiffResult))
                return result;

            var lines = fileDiffResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string filePath = line.Trim();
                
                // Skip ignored files
                if (ignorePatterns.ShouldIgnore(filePath))
                    continue;
                    
                // Get full diff content for the file
                var fileContent = await ExecuteGitCommandAsync($"show {endCommitOrDefault}:{filePath}");
                var diffContent = await ExecuteGitCommandAsync($"--no-pager diff {startCommitOrDefault} {endCommitOrDefault} -- {filePath}");
                result.Add(new GitDiffResult
                {
                    FilePath = filePath,
                    Changes = ParseDiffChanges(fileContent),
                    DiffContent = diffContent,
                    FullContent = fileContent ?? string.Empty
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error getting git diff: {ex.Message}", ex);
        }

        return result;
    }

    private List<string> ParseDiffChanges(string diffOutput)
    {
        var changes = new List<string>();
        var lines = diffOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Only include actual code changes, not metadata
            if ((line.StartsWith("+") && !line.StartsWith("++")) ||
                (line.StartsWith("-") && !line.StartsWith("--")))
            {
                changes.Add(line);
            }
        }

        return changes;
    }

    private async Task<string> ExecuteGitCommandAsync(string command)
    {
        try
        {
            var processStartInfo = new System.Diagnostics.ProcessStartInfo("git", command)
            {
                WorkingDirectory = _repositoryPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
            {
                var output = new System.Text.StringBuilder();
                
                process.OutputDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        output.AppendLine(e.Data);
                };
                
                process.ErrorDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine($"[Git Error] {e.Data}");
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await Task.Run(() => process.WaitForExit());
                
                return output.ToString().Trim();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to execute git command '{command}': {ex.Message}", ex);
        }
    }

    public bool IsGitRepository(string path)
    {
        try
        {
            var gitDir = Path.Combine(path, ".git");
            return Directory.Exists(gitDir) || File.Exists(gitDir);
        }
        catch
        {
            return false;
        }
    }
}
