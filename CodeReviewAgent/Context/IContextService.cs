using CodeReviewAgent.Tools;

namespace CodeReviewAgent.Context;

public interface IContextService
{
    List<ITool> GetAvailableTools();
    List<string> GetFileTree();
    string GetAdditionalInstructions();
    string GetResponsePattern();
    string GetSystemPrompt();
    string GetToolsDescription();
}
