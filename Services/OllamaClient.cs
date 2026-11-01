using System.Net.Http.Json;

namespace RestaurantManagementSystem.Services;

public sealed class OllamaClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;

    public OllamaClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _configuration = configuration;
    }

    public bool Enabled =>
        _configuration.GetValue("Ollama:Enabled", false);

    public async Task<string?> ChatAsync(string systemPrompt, string userMessage)
    {
        var model = _configuration["Ollama:Model"] ?? "llama3.2";

        var request = new
        {
            model,
            stream = false,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            }
        };

        using var response = await _http.PostAsJsonAsync("/api/chat", request);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content
            .ReadFromJsonAsync<OllamaResponse>();

        return result?.Message?.Content?.Trim();
    }

    private sealed class OllamaResponse
    {
        public OllamaMessage? Message { get; set; }
    }

    private sealed class OllamaMessage
    {
        public string? Content { get; set; }
    }
}