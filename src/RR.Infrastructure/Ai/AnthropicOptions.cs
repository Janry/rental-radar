namespace RR.Infrastructure.Ai;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>Claude model ID. Default — Haiku 4.5 (швидко і дешево для цього кейсу).</summary>
    public string Model { get; init; } = "claude-haiku-4-5-20251001";

    /// <summary>Max output tokens per request. 1024 з запасом для нашого схема.</summary>
    public int MaxTokens { get; init; } = 1024;

    /// <summary>
    /// API key — читається окремо з env-vars (`ANTHROPIC_API_KEY` за замовчуванням
    /// у Anthropic.SDK), тут не зберігається щоб не потрапив у serialized config.
    /// </summary>
    public string? ApiKey { get; init; }
}
