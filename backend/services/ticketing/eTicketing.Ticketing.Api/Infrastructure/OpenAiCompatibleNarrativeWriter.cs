using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using eTicketing.Ticketing.Business.Analytics.Narrative;
using Microsoft.Extensions.Options;

namespace eTicketing.Ticketing.Api.Infrastructure;

/// <summary>
/// Writes the AI Uvidi executive summary through any OpenAI-compatible <c>/chat/completions</c>
/// endpoint.
///
/// <para><b>One implementation, three providers.</b> Ollama, Groq and OpenRouter all speak this
/// wire format, so which one is in use is entirely a matter of base URL, model name and whether a
/// key is sent — see <see cref="NarrativeOptions"/>. That keeps the free local option (Ollama in
/// the compose stack, nothing leaves the machine) and the free hosted options a config change apart
/// rather than a code change.</para>
///
/// <para><b>It never throws and never blocks a report.</b> Every failure path — no endpoint, a
/// 500, a timeout, a shape it does not recognise, an empty completion — returns null, and the tab
/// renders its deterministic insights without a summary. This class sits at the end of a request
/// whose real work is already done; taking that work down over an optional flourish would be the
/// worst possible trade.</para>
///
/// <para>Lives in .Api rather than .Business because HTTP wiring is a hosting concern, the same
/// split HttpCatalogClient and HttpIdentityClient follow. What may be sent is decided in .Business
/// by NarrativePromptBuilder.</para>
/// </summary>
public sealed class OpenAiCompatibleNarrativeWriter : INarrativeWriter
{
    /// <summary>Low but not zero. The task is to restate supplied figures in Bosnian, where
    /// inventiveness is the failure mode — but a 3B local model at temperature 0 tends to echo the
    /// input list back verbatim.</summary>
    private const double Temperature = 0.2;

    /// <summary>Four sentences of Bosnian fit comfortably; anything past this is a model that
    /// ignored the instruction, and the response is truncated at MaxCharacters anyway.</summary>
    private const int MaxTokens = 400;

    private readonly HttpClient _http;
    private readonly NarrativeOptions _options;
    private readonly ILogger<OpenAiCompatibleNarrativeWriter> _logger;

    public OpenAiCompatibleNarrativeWriter(
        HttpClient http,
        IOptions<InsightsOptions> options,
        ILogger<OpenAiCompatibleNarrativeWriter> logger)
    {
        _http = http;
        _options = options.Value.Narrative;
        _logger = logger;
    }

    public async Task<string?> WriteAsync(NarrativeContext context, CancellationToken ct = default)
    {
        try
        {
            var request = new ChatRequest(
                _options.Model,
                [
                    new ChatMessage("system", NarrativePromptBuilder.SystemPrompt),
                    new ChatMessage("user", NarrativePromptBuilder.BuildUserPrompt(context))
                ],
                Temperature,
                MaxTokens,
                // Ollama streams by default; the others do not. Stated explicitly so one shape of
                // response has to be parsed rather than two.
                Stream: false);

            using var response = await _http.PostAsJsonAsync("chat/completions", request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI sažetak nije generisan: model je odgovorio sa {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
            var text = completion?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("AI sažetak nije generisan: model je vratio prazan odgovor.");
                return null;
            }

            // Truncated on a word boundary rather than mid-syllable — the summary sits in a
            // fixed-height card, and a model that ignored "najviše 4 rečenice" should still read as
            // a sentence that trails off rather than as corrupted text.
            return Truncate(text, _options.MaxCharacters);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller gave up (the client disconnected), not a model failure. Propagating would
            // be correct but pointless: nobody is waiting for the report either.
            return null;
        }
        catch (Exception ex)
        {
            // Deliberately broad. The set of things that can go wrong between here and an
            // unreachable container is open-ended (DNS, TLS, a proxy returning HTML, a provider
            // changing its error shape), and every one of them has the same right answer.
            _logger.LogWarning(ex, "AI sažetak nije generisan — izvještaj se vraća bez njega.");
            return null;
        }
    }

    private static string Truncate(string text, int maxCharacters)
    {
        if (maxCharacters <= 0 || text.Length <= maxCharacters)
            return text;

        var cut = text[..maxCharacters];
        var lastSpace = cut.LastIndexOf(' ');

        return (lastSpace > maxCharacters / 2 ? cut[..lastSpace] : cut).TrimEnd() + "…";
    }

    // ── Wire shapes ──────────────────────────────────────────────────────────────────────────
    // Only the fields actually read. Everything else these providers send (usage, ids, fingerprints,
    // provider-specific extras) is ignored by the deserializer, which is what lets one set of
    // records serve three APIs that each add their own.

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatResponseMessage? Message);

    private sealed record ChatResponseMessage(
        [property: JsonPropertyName("content")] string? Content);
}
