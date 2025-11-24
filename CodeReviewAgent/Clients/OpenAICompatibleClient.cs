using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ReviewAgent.Clients;

public class OpenAICompatibleClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public OpenAICompatibleClient(
        string baseUrl = "http://localhost:1234",
        string model = "gemma-3-4b-it",
        HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri(baseUrl) };
        _endpoint = "v1/chat/completions";
        _model = model;
    }

    public void Dispose() => _httpClient.Dispose();
    
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(messages, options);

        using var response = await _httpClient.PostAsJsonAsync(
            _endpoint,
            request,
            _jsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var root = json.RootElement;

        var content = root
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        return new ChatResponse(
            new[] { new ChatMessage(ChatRole.Assistant, content) }
        );
    }
    
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(messages, options);
        request["stream"] = true;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // LM Studio formates SSE (Server-Sent Events)
            if (line.StartsWith("data: "))
                line = line[6..].Trim();

            if (line == "[DONE]") yield break;

            ChatResponseUpdate? update = null;
            try
            {
                var json = JsonDocument.Parse(line);
                var delta = json.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("delta");

                if (delta.TryGetProperty("content", out var contentProp))
                {
                    var chunk = contentProp.GetString();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        update = new ChatResponseUpdate(ChatRole.Assistant, chunk);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OpenAICompatible] Stream parse error: {ex.Message}");
            }

            if (update != null)
                yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    
    private Dictionary<string, object?> BuildRequest(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options)
    {
        return new Dictionary<string, object?>
        {
            ["model"] = options?.ModelId ?? _model,
            ["temperature"] = options?.Temperature ?? 0.7,
            ["messages"] = messages.Select(m => new
            {
                role = RoleToString(m.Role),
                content = m.Text // Important: ChatMessage.Contents is not correct for OpenAI wire format
            }).ToArray()
        };
    }

    private static string RoleToString(ChatRole role)
    {
        if (role.Equals(ChatRole.User)) return "user";
        if (role.Equals(ChatRole.System)) return "system";
        if (role.Equals(ChatRole.Assistant)) return "assistant";
        return "user";
    }
}
