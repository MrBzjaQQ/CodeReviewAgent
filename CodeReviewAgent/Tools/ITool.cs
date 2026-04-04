namespace CodeReviewAgent.Tools;

public interface ITool
{
    string ToolName { get; }

    Task<object> RunToolAsync(params string[] parameters);

    Task<T> RunToolAsync<T>(params string[] parameters);
}
