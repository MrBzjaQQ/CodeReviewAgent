using System.Collections.Generic;

namespace ReviewAgent.Services;

// Represents a single comment in code review
public class ReviewComment
{
    public int Line { get; set; }
    
    public string Severity { get; set; } = "Warning"; // Info, Warning, Error, Critical
    
    public string Message { get; set; } = "";
    
    public string Suggestion { get; set; } = "";
}

public class ReviewComments
{
    public ReviewComment[] Comments { get; set; } = [];
}

// Represents the result of reviewing a single file
public class FileReviewResult
{
    public string FilePath { get; set; } = string.Empty;
    
    public ReviewComment[] Comments { get; set; } = [];
    
    public int OverallScore { get; set; } // 0-100 scale
    
    public string DiffContent { get; set; }
    
    public string FullContent { get; set; }
    
    public override string ToString() => $"{FilePath} - Score: {OverallScore}/100 ({Comments.Length} comments)";
}
