using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Core.Abstractions;
using RR.Core.Domain;
using RR.Infrastructure.Ai;

namespace RR.Infrastructure.Matching;

/// <summary>
/// Двоетапний match:
///   1) Структурний — швидкий LINQ по полях фільтра (price, area, property, ...)
///   2) Semantic — лише для тих фільтрів що мають SemanticQuery, запит до Claude
///      з force-tool_use {matches: bool}. Економить ~80% AI-викликів проти "всім підряд".
/// </summary>
public sealed class MatchingEngine(
    IUserFilterRepository filters,
    IClaudeClient claude,
    IOptions<AnthropicOptions> anthropicOptions,
    ILogger<MatchingEngine> log)
    : IMatchingEngine
{
    private const string ToolName = "evaluate_semantic_match";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly AnthropicOptions _opts = anthropicOptions.Value;
    private readonly Function _semanticTool = BuildSemanticTool();

    public async Task<IReadOnlyList<UserFilter>> FindMatchingFiltersAsync(
        Listing listing,
        CancellationToken ct = default)
    {
        var candidates = await filters.GetActiveByLocationAsync(listing.LocationId, ct);
        if (candidates.Count == 0) return Array.Empty<UserFilter>();

        var structural = candidates.Where(f => MatchesStructurally(listing, f)).ToList();
        if (structural.Count == 0) return Array.Empty<UserFilter>();

        var matched = new List<UserFilter>();
        foreach (var filter in structural)
        {
            if (string.IsNullOrWhiteSpace(filter.SemanticQuery))
            {
                matched.Add(filter);
                continue;
            }

            if (await SemanticMatchesAsync(listing, filter, ct))
                matched.Add(filter);
        }

        return matched;
    }

    private static bool MatchesStructurally(Listing l, UserFilter f)
    {
        // Прайс: якщо фільтр обмежує — listing мусить мати price і вкластись.
        // Listings без price (price=null) проходять структурно лише якщо фільтр НЕ обмежує price.
        if (f.MaxPrice.HasValue && (l.PricePerMonth is null || l.PricePerMonth > f.MaxPrice)) return false;
        if (f.MinPrice.HasValue && (l.PricePerMonth is null || l.PricePerMonth < f.MinPrice)) return false;

        if (f.Areas.Count > 0)
        {
            if (l.Area is null) return false;
            if (!f.Areas.Contains(l.Area, StringComparer.OrdinalIgnoreCase)) return false;
        }

        if (f.PropertyTypes.Count > 0)
        {
            if (l.PropertyType is null) return false;
            if (!f.PropertyTypes.Contains(l.PropertyType.Value)) return false;
        }

        if (f.MinBedrooms.HasValue && (l.Bedrooms is null || l.Bedrooms < f.MinBedrooms)) return false;

        // Boolean requires: якщо фільтр вимагає true — listing мусить мати true (не null, не false).
        if (f.RequirePetsAllowed == true && l.PetsAllowed != true) return false;
        if (f.RequirePool == true && l.HasPool != true) return false;
        if (f.RequireHotWater == true && l.HasHotWater != true) return false;

        return true;
    }

    private async Task<bool> SemanticMatchesAsync(Listing listing, UserFilter filter, CancellationToken ct)
    {
        var parameters = new MessageParameters
        {
            Model = _opts.Model,
            MaxTokens = 256,
            Temperature = 0m,
            System = [new SystemMessage(SemanticSystemPrompt)],
            Messages =
            [
                new Message(RoleType.User,
                    $"Listing description:\n{listing.RawText}\n\nUser's natural-language requirement:\n{filter.SemanticQuery}")
            ],
            Tools = [_semanticTool],
            ToolChoice = new ToolChoice { Type = ToolChoiceType.Tool, Name = ToolName }
        };

        try
        {
            var response = await claude.GetMessageAsync(parameters, ct);
            var toolUse = response.Content.OfType<ToolUseContent>().FirstOrDefault();
            if (toolUse is null)
            {
                log.LogDebug("Semantic match: no tool_use response for filter {FilterId}", filter.Id);
                return false;
            }

            var verdict = toolUse.Input.Deserialize<SemanticVerdict>(JsonOpts);
            log.LogDebug("Semantic match for filter {FilterId}: matches={Matches}, reason={Reason}",
                filter.Id, verdict?.Matches, verdict?.Reason);
            return verdict?.Matches ?? false;
        }
        catch (Exception ex)
        {
            // Якщо AI впав — пропускаємо лише structural-match, не блокуємо нотифу.
            // Альтернатива (false-safe) — пропускати такі взагалі. Поточний вибір: notify надмірно ніж недостатньо.
            log.LogWarning(ex, "Semantic match failed for filter {FilterId}; falling back to structural-only", filter.Id);
            return true;
        }
    }

    private const string SemanticSystemPrompt = """
        You evaluate whether a rental listing matches a user's natural-language requirement.
        Always call the `evaluate_semantic_match` tool with your verdict.

        Be moderately strict but not overly literal:
        - "quiet place" — listing in middle of busy main road = no match
        - "near nature" — listing in jungle / near rice fields = match; in town center = no
        - "modern" — listing of old wooden bungalow = no; new apartment = yes
        - If listing text is too short to judge, default to matches=true (better notify than miss).
        """;

    private static Function BuildSemanticTool()
    {
        var schema = new InputSchema
        {
            Type = "object",
            Required = ["matches", "reason"],
            Properties = new Dictionary<string, Property>
            {
                ["matches"] = new() { Type = "boolean", Description = "true if listing matches the user's requirement" },
                ["reason"] = new() { Type = "string", Description = "Short explanation (max 100 chars)" }
            }
        };

        var jsonOpts = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        return new Function(ToolName,
            "Decide whether the rental listing matches the user's free-form requirement.",
            JsonNode.Parse(JsonSerializer.Serialize(schema, jsonOpts))!);
    }

    private sealed record SemanticVerdict(bool Matches, string Reason);
}
