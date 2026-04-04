using CodeReviewAgent.Tools;
using CodeReviewAgent.Utils;

namespace CodeReviewAgent.Factories;

/// <summary>
/// Factory for creating tools
/// </summary>
public class ToolFactory : IToolFactory
{
    private readonly IgnorePatternMatcher _ignorePatternMatcher;

    /// <summary>
    /// Initializes a new instance of the ToolFactory class
    /// </summary>
    /// <param name="ignorePatternMatcher">Pattern matcher for ignoring files</param>
    public ToolFactory(IgnorePatternMatcher ignorePatternMatcher)
    {
        _ignorePatternMatcher = ignorePatternMatcher;
    }

    /// <summary>
    /// Creates a tool by its name
    /// </summary>
    /// <param name="toolName">Name of the tool to create</param>
    /// <returns>The created tool</returns>
    public ITool CreateTool(string toolName)
    {
        return toolName switch
        {
            "ReadFileTreeTool" => new ReadFileTreeTool(_ignorePatternMatcher),
            "ReadFileTool" => new ReadFileTool(),
            "WriteFileTool" => new WriteFileTool(),
            "SearchAndReplaceTool" => new SearchAndReplaceTool(),
            "FileGitDiffTool" => new FileGitDiffTool(),
            _ => throw new ArgumentException($"Unknown tool: {toolName}")
        };
    }
}
