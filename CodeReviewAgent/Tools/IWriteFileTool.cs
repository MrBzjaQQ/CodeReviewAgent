using System.Threading.Tasks;

namespace CodeReviewAgent.Tools;

public interface IWriteFileTool : ITool
{
    Task<bool> RunToolAsync(params string[] parameters);
}
