using CodeReviewAgent.Factories;
using CodeReviewAgent.Services;
using CodeReviewAgent.Tools;

namespace CodeReviewAgent.Context;

public class ContextService : IContextService
{
    private readonly IToolFactory _toolFactory;
    private readonly string _baseDirectory;
    private readonly PromptService _promptService;
    private List<string>? _fileTree;

    public ContextService(
        IToolFactory toolFactory,
        string baseDirectory,
        PromptService promptService)
    {
        _toolFactory = toolFactory;
        _baseDirectory = baseDirectory;
        _promptService = promptService;
    }

    public List<ITool> GetAvailableTools()
    {
        return new List<ITool>
        {
            _toolFactory.CreateTool("ReadFileTreeTool"),
            _toolFactory.CreateTool("ReadFileTool"),
            _toolFactory.CreateTool("WriteFileTool"),
            _toolFactory.CreateTool("SearchAndReplaceTool"),
            _toolFactory.CreateTool("FileGitDiffTool")
        };
    }

    public List<string> GetFileTree()
    {
        if (_fileTree == null)
        {
            var readFileTreeTool = (IReadFileTreeTool)_toolFactory.CreateTool("ReadFileTreeTool");
            _fileTree = readFileTreeTool.RunToolAsync(_baseDirectory).GetAwaiter().GetResult();
        }
        return _fileTree;
    }

    public string GetAdditionalInstructions() => _promptService.GetAdditionalInstructions();
    public string GetResponsePattern() => _promptService.GetResponsePattern();
    public string GetSystemPrompt() => _promptService.GetSystemPrompt();
    public string GetToolsDescription() => _promptService.GetToolsDescription();
}
