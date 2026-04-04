namespace CodeReviewAgent.Services;

public class PromptService
{
    private readonly string _templatesDirectory;

    public PromptService(string templatesDirectory)
    {
        _templatesDirectory = templatesDirectory;
    }

    public string GetSystemPrompt()
    {
        return LoadTemplate("system-prompt.txt");
    }

    public string GetToolsDescription()
    {
        return LoadTemplate("tools-description.txt");
    }

    public string GetAdditionalInstructions()
    {
        return LoadTemplate("additional-instructions.txt");
    }

    public string GetResponsePattern()
    {
        return LoadTemplate("response-pattern.txt");
    }

    private string LoadTemplate(string fileName)
    {
        var filePath = Path.Combine(_templatesDirectory, fileName);
        
        if (File.Exists(filePath))
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load template '{fileName}': {ex.Message}");
            }
        }

        return GetDefaultValue(fileName);
    }

    private string GetDefaultValue(string fileName)
    {
        return fileName switch
        {
            "system-prompt.txt" => "Perform a thorough code review of the changed file.",
            "tools-description.txt" => "- ReadFileTreeTool: Lists all files\n- ReadFileTool: Reads file content\n- WriteFileTool: Writes content to file\n- SearchAndReplaceTool: Searches and replaces content\n- FileGitDiffTool: Gets git diff",
            "additional-instructions.txt" => "",
            "response-pattern.txt" => "Write your code review findings to the result file using WriteFileTool.",
            _ => ""
        };
    }
}
