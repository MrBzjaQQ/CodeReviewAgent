using CodeReviewAgent.Tools;

namespace CodeReviewAgent.Factories;

public interface IToolFactory
{
    ITool CreateTool(string toolName);
}
