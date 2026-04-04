using System.Threading.Tasks;

namespace CodeReviewAgent.Tools;

public interface IFileGitDiffTool : ITool
{
    Task<string> RunToolAsync(params string[] parameters);
}
