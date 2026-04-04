using System.Collections.Generic;
using System.Threading.Tasks;

namespace CodeReviewAgent.Tools;

public interface IReadFileTreeTool : ITool
{
    Task<List<string>> RunToolAsync(params string[] parameters);
}
