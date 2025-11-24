using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ReviewAgent.Clients;

public class LmStudioClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public LmStudioClient(string baseUrl = "http://localhost:1234", HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient()
        {
            BaseAddress = new Uri(baseUrl)
        };
        
        _endpoint = "v1/chat/completions";
    }

    public void Dispose() => _httpClient.Dispose();

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(messages, options);
        var response = await _httpClient.PostAsJsonAsync(_endpoint, request, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonDocument.Parse(body);

        var content = parsed.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return new ChatResponse([new ChatMessage(ChatRole.Assistant, content ?? string.Empty)]);
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
            Content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json")
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
                Console.WriteLine($"[LmStudioClient] Stream parse error: {ex.Message}");
            }

            if (update != null)
                yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    // Вспомогательная функция для сборки запроса
    private static Dictionary<string, object?> BuildRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        return new Dictionary<string, object?>
        {
            ["model"] = options?.ModelId ?? "gemma-3-4b-it",
            ["temperature"] = options?.Temperature ?? 0.7,
            ["messages"] = messages.Select(m => new
            {
                role = m.Role,
                content = m.Contents
            }).ToArray()
        };
    }
}
