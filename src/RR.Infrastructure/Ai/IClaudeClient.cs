using Anthropic.SDK.Messaging;

namespace RR.Infrastructure.Ai;

/// <summary>
/// Тонка обгортка над AnthropicClient.Messages — щоб у тестах підставити фейк
/// без HTTP-моків. Усе інше з SDK (MessageParameters, MessageResponse, ToolUseContent)
/// використовуємо як є — це data shapes, не сервісні класи.
/// </summary>
public interface IClaudeClient
{
    Task<MessageResponse> GetMessageAsync(MessageParameters parameters, CancellationToken ct = default);
}

internal sealed class AnthropicSdkClient : IClaudeClient, IDisposable
{
    private readonly Anthropic.SDK.AnthropicClient _client;

    public AnthropicSdkClient(AnthropicOptions options)
    {
        // API ключ підтягується з env ANTHROPIC_API_KEY якщо options.ApiKey порожній —
        // це default behaviour AnthropicClient.
        _client = string.IsNullOrEmpty(options.ApiKey)
            ? new Anthropic.SDK.AnthropicClient()
            : new Anthropic.SDK.AnthropicClient(options.ApiKey);
    }

    public Task<MessageResponse> GetMessageAsync(MessageParameters parameters, CancellationToken ct = default) =>
        _client.Messages.GetClaudeMessageAsync(parameters, ctx: ct);

    public void Dispose() => _client.Dispose();
}
