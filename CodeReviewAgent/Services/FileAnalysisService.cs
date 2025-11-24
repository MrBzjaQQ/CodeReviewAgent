using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ReviewAgent.Services;

public class FileAnalysisService
{
    private readonly string _repositoryPath;

    public FileAnalysisService(string repositoryPath)
    {
        if (!Directory.Exists(repositoryPath))
            throw new DirectoryNotFoundException($"Repository not found: {repositoryPath}");
            
        _repositoryPath = repositoryPath;
    }

public async Task<Dictionary<string, string>> GetRelatedDependenciesAsync(string filePath, string fileContent)
    {
        var dependencies = new Dictionary<string, string>();
        
        try
        {
            // Extract dependencies based on file type
            if (filePath.EndsWith(".cs"))
            {
                ExtractCSharpDependencies(filePath, fileContent, dependencies);
            }
            else if (filePath.EndsWith(".js") || filePath.EndsWith(".ts"))
            {
                ExtractJsTsDependencies(filePath, fileContent, dependencies);
            }
            else if (filePath.EndsWith(".py"))
            {
                ExtractPythonDependencies(filePath, fileContent, dependencies);
            }
            else if (filePath.EndsWith(".java"))
            {
                ExtractJavaDependencies(filePath, fileContent, dependencies);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error analyzing dependencies for {filePath}: {ex.Message}");
        }

        return dependencies;
    }

private void ExtractCSharpDependencies(string filePath, string content, Dictionary<string, string> dependencies)
    {
        // Extract using/Imports statements
        var usingPattern = @"(?:using|import)\s+([\w\.]+)(?:\s*;|\s*\{)";
        var matches = Regex.Matches(content, usingPattern);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                var importStatement = match.Groups[1].Value;
                
                // Convert to file path for common namespaces
                if (importStatement.StartsWith("System.") || 
                    importStatement.StartsWith("Microsoft."))
                {
                    continue; // Skip standard libraries
                }
                
                // Try to find the actual file in repository
                var potentialFile = FindCSharpDependency(importStatement);
                if (!string.IsNullOrEmpty(potentialFile))
                {
                    dependencies.Add(potentialFile, "C# dependency");
                }
            }
        }

        // Extract class/interface/struct references
        var typePattern = @"(?:class|interface|struct)\s+(\w+)";
        matches = Regex.Matches(content, typePattern);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1 && !dependencies.ContainsKey(match.Groups[1].Value + ".cs"))
            {
                dependencies.Add(match.Groups[1].Value + ".cs", "dependency");
            }
        }

        // Extract method calls that might indicate related files
        var methodCallPattern = @"(\w+)\.\w+\(";
        matches = Regex.Matches(content, methodCallPattern);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1 && !dependencies.ContainsKey(match.Groups[1].Value + ".cs"))
            {
                dependencies.Add(match.Groups[1].Value + ".cs", "dependency");
            }
        }
    }

private void ExtractJsTsDependencies(string filePath, string content, Dictionary<string, string> dependencies)
    {
        // Extract require() and ES6 imports
        var importPattern = @"(?:require|import)\s*\(?['""]([^'""]+)['""]\)|import\s+.*from\s*['""]([^'""]+)['""]";
        var matches = Regex.Matches(content, importPattern);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[1].Value))
                dependencies.Add(ResolveJsImportPath(filePath, match.Groups[1].Value), "JS/TS dependency");
            
            if (!string.IsNullOrEmpty(match.Groups[2].Value))
                dependencies.Add(ResolveJsImportPath(filePath, match.Groups[2].Value), "JS/TS dependency");
        }

        // Extract common framework patterns
        var reactPattern = @"React\.|react\.";
        if (Regex.IsMatch(content, reactPattern))
            dependencies.Add("react.js", "JS/TS dependency");
    }

private void ExtractPythonDependencies(string filePath, string content, Dictionary<string, string> dependencies)
    {
        // Extract import statements
        var importPattern = @"(?:import\s+([\w\.]+)|from\s+(\w+)\s+import)";
        var matches = Regex.Matches(content, importPattern);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                dependencies.Add(ResolvePythonImportPath(filePath, match.Groups[1].Value), "Python dependency");
            
            if (!string.IsNullOrEmpty(match.Groups[2].Value))
                dependencies.Add(ResolvePythonImportPath(filePath, match.Groups[2].Value), "Python dependency");
        }
    }

private void ExtractJavaDependencies(string filePath, string content, Dictionary<string, string> dependencies)
    {
        // Extract import statements
        var importPattern = @"import\s+([\w\.]+)";
        var matches = Regex.Matches(content, importPattern);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                dependencies.Add(ResolveJavaImportPath(filePath, match.Groups[1].Value), "Java import");
        }

        // Extract class references
        var classRefPattern = @"(?:class|interface)\s+(\w+)";
        matches = Regex.Matches(content, classRefPattern);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1 && !dependencies.ContainsKey(match.Groups[1].Value + ".java"))
                dependencies.Add(match.Groups[1].Value + ".java", "Java dependency");
        }
    }

    private string ResolveJsImportPath(string currentFile, string importPath)
    {
        // Handle relative imports
        if (importPath.StartsWith("./") || importPath.StartsWith("../"))
            return Path.GetRelativePath(Path.GetDirectoryName(currentFile) ?? "", importPath.Replace("./", "").Replace("../", "../"));

        // Handle node_modules packages
        if (!importPath.StartsWith("."))
            return $"node_modules/{importPath}/index.js";

        return importPath;
    }

    private string ResolvePythonImportPath(string currentFile, string importPath)
    {
        // Handle relative imports
        if (importPath.StartsWith("."))
            return Path.GetRelativePath(Path.GetDirectoryName(currentFile) ?? "", importPath.Replace("./", "").Replace("../", "../"));

        // Convert dotted notation to file path
        var parts = importPath.Split('.');
        var fileName = parts.LastOrDefault() + ".py";
        
        if (parts.Length > 1)
            return $"{string.Join("/", parts.Take(parts.Length - 1))}/{fileName}";

        return fileName;
    }

    private string ResolveJavaImportPath(string currentFile, string importPath)
    {
        // Convert package notation to file path
        var packageName = importPath.Replace('.', Path.DirectorySeparatorChar);
        return $"{packageName}.java";
    }

    private string? FindCSharpDependency(string namespaceOrClass)
    {
        try
        {
            var searchPattern = "*" + namespaceOrClass.Split('.').Last() + ".cs";
            
            var files = Directory.EnumerateFiles(
                _repositoryPath, 
                searchPattern, 
                SearchOption.AllDirectories
            );

            if (files.Any())
                return Path.GetRelativePath(_repositoryPath, files.First());
        }
        catch { /* Ignore search errors */ }

        return null;
    }

    public bool IsFileTooLarge(string filePath, int maxBytes)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length > maxBytes;
        }
        catch
        {
            return true; // If we can't determine size, assume it's too large
        }
    }

    public string? GetFileExtension(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        // Remove the dot from extension
        return extension.StartsWith('.') ? extension.Substring(1) : extension;
    }
}
