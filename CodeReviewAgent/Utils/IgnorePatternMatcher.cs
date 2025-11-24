using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ReviewAgent.Utils;

public class IgnorePatternMatcher
{
    private readonly List<string> _ignorePatterns = new();
    private static readonly char[] PatternSeparators = { ' ', ',', ';', '\t' };

    public void LoadIgnoreFile(string ignoreFilePath)
    {
        if (!File.Exists(ignoreFilePath))
            return;

        try
        {
            var lines = File.ReadAllLines(ignoreFilePath);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip empty lines and comments
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;
                    
                _ignorePatterns.Add(trimmedLine);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not read ignore file {ignoreFilePath}: {ex.Message}");
        }
    }

    public bool ShouldIgnore(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        foreach (var pattern in _ignorePatterns)
        {
            if (IsMatch(path, pattern.ToLowerInvariant()))
                return true;
        }

        return false;
    }

    private bool IsMatch(string path, string pattern)
    {
        // Handle different patterns
        if (pattern.StartsWith("**"))
        {
            // Recursive directory: **/bin -> matches any file in bin or subdirectories
            var suffix = pattern.Substring(2);
            return path.Contains(suffix.Replace('/', '\\'));
        }
        else if (pattern.EndsWith("/**") || pattern.EndsWith("\\**"))
        {
            // Directory with all contents: node_modules/**
            var prefix = pattern.Substring(0, pattern.Length - 3).Replace('/', '\\');
            return path.StartsWith(prefix + "\\");
        }
        else if (pattern.Contains('*'))
        {
            // Wildcard pattern: *.dll
            var regexPattern = Regex.Escape(pattern)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".")
                .Replace(@"\/", @"[\\/]");

            return System.Text.RegularExpressions.Regex.IsMatch(path, $"^{regexPattern}$");
        }
        else if (pattern.EndsWith("/") || pattern.EndsWith("\\"))
        {
            // Directory: bin/ or obj\
            var dirPath = pattern.TrimEnd('/', '\\');
            return path.Contains(dirPath + "\\");
        }
        else
        {
            // Exact match or simple directory name
            return path.Contains(pattern.Replace('/', '\\'));
        }
    }

    public static string GetDefaultIgnoreFile(string baseDirectory)
    {
        return Path.Combine(baseDirectory, ".reviewignore");
    }

    private static readonly string[] DefaultPatterns = new[]
    {
        "bin/",
        "obj/",
        "node_modules/",
        ".git/",
        "__pycache__/",
        "*.pyc",
        "*.pyo",
        "*.dll",
        "*.exe",
        "*.pdb",
        "*.so",
        "*.dylib",
        "vendor/",
        "packages/",
        ".vs/",
        "Thumbs.db",
        ".DS_Store"
    };

    public void LoadDefaultPatterns()
    {
        _ignorePatterns.Clear();
        
        foreach (var pattern in DefaultPatterns)
        {
            if (!_ignorePatterns.Contains(pattern))
                _ignorePatterns.Add(pattern);
        }
    }
}
