using System.Threading.Tasks;

namespace CodeReviewAgent.Tools;

public interface ISearchAndReplaceTool : ITool
{
    Task<bool> RunToolAsync(params string[] parameters);
}
