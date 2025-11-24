using System;
using System.Collections.Generic;

namespace ReviewAgent;

public class CodeReviewOptions
{
    // Path to the code directory (required)
    public string? Directory { get; set; }
    
    // Starting commit hash for comparison (optional)
    public string? StartCommit { get; set; }
    
    // Ending commit hash for comparison (optional)
    public string? EndCommit { get; set; }
    
    // Path to .reviewignore file (optional, defaults to ./.reviewignore)
    public string? IgnoreFilePath { get; set; } = ".reviewignore";
    
    // Path to custom review rules template (optional)
    public string? ReviewRulesTemplatePath { get; set; }
    
    // Path to HTML output file (required, defaults to ./index.html)
    public string OutputHtmlFile { get; set; } = "index.html";
    
    // LLM Server base URL (defaults to http://localhost:1234)
    public string BaseUrl { get; set; } = "http://localhost:1234";
    
    // Model name for the review process
    public string ModelName { get; set; } = "gpt-oss-20b";
    
    // Temperature setting for LLM response generation (0.0 to 1.0)
    public double Temperature { get; set; } = 0.7;
    
    // Maximum file size in bytes that will be processed
    public int MaxFileSize { get; set; } = 1024 * 1024; // 1MB
    
    // Whether to include git diff in the output
    public bool IncludeDiff { get; set; } = true;
    
    // File extensions to process (null means all files)
    public List<string>? AllowedExtensions { get; set; }
    
    // Severity levels that should be included in review comments
    public ReviewCommentSeverity[] IncludedSeverities { get; set; } = 
        Enum.GetValues<ReviewCommentSeverity>();
    
    // LLM client type (defaults to LmStudio)
    public string ClientType { get; set; } = "LmStudio";
    
    // Validate options and throw exceptions if required fields are missing or invalid
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Directory))
            throw new ArgumentException("Directory path is required", nameof(Directory));
        
        if (!System.IO.Directory.Exists(Directory))
            throw new DirectoryNotFoundException($"Directory not found: {Directory}");
            
        if (Temperature < 0.0 || Temperature > 1.0)
            throw new ArgumentOutOfRangeException(nameof(Temperature), "Temperature must be between 0.0 and 1.0");
            
        if (MaxFileSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxFileSize), "Max file size must be positive");
    }
}

public enum ReviewCommentSeverity
{
    Info,
    Warning,
    Error,
    Critical
}
