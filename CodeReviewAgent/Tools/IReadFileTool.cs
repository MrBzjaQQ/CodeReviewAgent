using System.Threading.Tasks;

namespace CodeReviewAgent.Tools;

public interface IReadFileTool : ITool
{
    Task<string> RunToolAsync(params string[] parameters);
}
