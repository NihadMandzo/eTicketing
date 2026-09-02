namespace eTicketing.Ticketing.Business.Analytics.Narrative;

/// <summary>Bound from the "Insights" section of appsettings.json.</summary>
public sealed class InsightsOptions
{
    public const string SectionName = "Insights";

    /// <summary>How long a computed AI Uvidi response is reused for.
    ///
    /// Short, not zero. Fitting SSA and K-Means is milliseconds, but the tab also costs three
    /// report queries and up to two cross-service lookups, and the range the user is looking at
    /// does not change between the moment they switch tabs and the moment they hit the horizon
    /// selector. Ten minutes is short enough that a sale made now shows up while the organizer is
    /// still at their desk.</summary>
    public int CacheMinutes { get; set; } = 10;

    public NarrativeOptions Narrative { get; set; } = new();
}

/// <summary>
/// The optional LLM summary on top of the deterministic insights.
///
/// One implementation covers every provider worth using here because they all speak the same
/// OpenAI-compatible <c>/chat/completions</c> shape — only the base URL, the model name and whether
/// a key is needed differ:
///
/// <list type="bullet">
///   <item><b>Ollama</b> — <c>http://ollama:11434/v1</c>, no key. Runs in the compose stack, costs
///   nothing and never sends an organization's figures off the machine. The default.</item>
///   <item><b>Groq</b> — <c>https://api.groq.com/openai/v1</c>, free key. Noticeably better
///   Bosnian than a 3B local model, at the cost of an external dependency.</item>
///   <item><b>OpenRouter</b> — <c>https://openrouter.ai/api/v1</c>, free-tier models.</item>
/// </list>
/// </summary>
public sealed class NarrativeOptions
{
    /// <summary>"None" (default) or "OpenAiCompatible". Off by default so the stack runs, is
    /// demoable and passes its tests with no model server anywhere near it — the narrative is a
    /// nicety on top of the insights, never a prerequisite for them.</summary>
    public string Provider { get; set; } = "None";

    public string BaseUrl { get; set; } = "http://localhost:11434/v1";

    public string Model { get; set; } = "llama3.2:3b";

    /// <summary>Empty for Ollama, which authenticates nothing; a free-tier token for Groq or
    /// OpenRouter. Sent as a Bearer header only when non-empty.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>A local 3B model on CPU takes a few seconds; 20 is generous for that and still
    /// short enough that a hung endpoint does not hold a report request open.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>Hard cap on what is accepted back. A model that ignores the "four sentences"
    /// instruction and writes an essay gets truncated rather than pushed onto the screen — the
    /// summary sits in a fixed-height card.</summary>
    public int MaxCharacters { get; set; } = 900;

    /// <summary>
    /// Token budget for the completion. Four sentences of Bosnian are 120-180 tokens, so 800 looks
    /// wildly generous — and it is not, because of reasoning models.
    ///
    /// A reasoning model (Groq's <c>openai/gpt-oss-*</c>, the qwen3 family) spends this budget on
    /// hidden reasoning tokens *before* emitting a single visible character: measured at ~296
    /// reasoning tokens for this prompt. A 220-token budget therefore returns an empty completion,
    /// which is a maddening failure to diagnose — HTTP 200, no error, no text.
    ///
    /// So the default suits the recommended hosted path, and this is configurable for the one case
    /// that cares about the cap: a local CPU model, where generation is linear in tokens produced
    /// (~3,8 tokens/s measured) and 800 would mean minutes. Set it to ~200 there — a small local
    /// model does no reasoning, so it does not need the headroom.
    /// </summary>
    public int MaxTokens { get; set; } = 800;

    public bool IsEnabled =>
        string.Equals(Provider, "OpenAiCompatible", StringComparison.OrdinalIgnoreCase);
}
