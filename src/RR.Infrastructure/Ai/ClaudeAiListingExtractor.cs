using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.Infrastructure.Ai;

/// <summary>
/// Claude-based AI-екстрактор: дзвонимо Haiku з force-tool_use, отримуємо
/// структуру з price/area/тощо. Якщо AI каже "не оголошення оренди" —
/// повертаємо null, scraper-у не зберігає Listing.
///
/// System prompt великий і ідентичний між викликами — кешуємо через
/// AutomaticToolsAndSystem (Anthropic prompt-cache, 5-хв TTL).
/// </summary>
public sealed class ClaudeAiListingExtractor(
    IClaudeClient client,
    IOptions<AnthropicOptions> options,
    ILogger<ClaudeAiListingExtractor> log)
    : IAiListingExtractor
{
    private const string ToolName = "extract_rental_listing";

    // Anthropic повертає поля у snake_case (як описано в нашій schema).
    // Наш record використовує PascalCase — мапимо через SnakeCaseLower policy.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly AnthropicOptions _opts = options.Value;
    private readonly Function _tool = BuildTool();

    public async Task<Listing?> ExtractAsync(RawListing raw, Location location, CancellationToken ct = default)
    {
        var systemMessages = BuildSystemMessages();
        var userMessage = BuildUserMessage(raw, location);

        var parameters = new MessageParameters
        {
            Model = _opts.Model,
            MaxTokens = _opts.MaxTokens,
            Temperature = 0m,   // детермінізм для extraction
            System = systemMessages,
            Messages = [new Message(RoleType.User, userMessage)],
            Tools = [_tool],
            ToolChoice = new ToolChoice { Type = ToolChoiceType.Tool, Name = ToolName },
            PromptCaching = PromptCacheType.AutomaticToolsAndSystem
        };

        MessageResponse response;
        try
        {
            response = await client.GetMessageAsync(parameters, ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Claude API error for post {ExternalId}; skipping", raw.ExternalId);
            return null;
        }

        var toolUse = response.Content.OfType<ToolUseContent>().FirstOrDefault();
        if (toolUse is null)
        {
            log.LogWarning("No tool_use block in Claude response for {ExternalId}", raw.ExternalId);
            return null;
        }

        ExtractedFields? fields;
        try
        {
            fields = toolUse.Input.Deserialize<ExtractedFields>(JsonOpts);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Failed to parse extracted fields for {ExternalId}", raw.ExternalId);
            return null;
        }

        if (fields is null || !fields.IsRentalPost)
        {
            log.LogDebug("Skipping non-rental post {ExternalId} (confidence={Confidence})",
                raw.ExternalId, fields?.Confidence ?? 0);
            return null;
        }

        return MapToListing(raw, location, fields);
    }

    private static Listing MapToListing(RawListing raw, Location location, ExtractedFields f) => new()
    {
        LocationId = location.Id,
        SourceId = raw.SourceId,
        ExternalId = raw.ExternalId,
        SourceUrl = raw.SourceUrl,
        RawText = raw.Text,
        AuthorName = raw.AuthorName,
        AuthorProfileUrl = raw.AuthorProfileUrl,
        ImageUrls = raw.ImageUrls.ToList(),
        ContactInfo = f.ContactInfo?.ToList() ?? new(),
        PostedAt = raw.PostedAt,
        ConfidenceScore = f.Confidence,
        PricePerMonth = f.PricePerMonth,
        Area = f.Area,
        Bedrooms = f.Bedrooms,
        PropertyType = ParsePropertyType(f.PropertyType),
        PetsAllowed = f.PetsAllowed,
        HasPool = f.HasPool,
        HasHotWater = f.HasHotWater,
        HasWifi = f.HasWifi,
        AvailableFrom = ParseDateOnly(f.AvailableFrom)
    };

    private static PropertyType? ParsePropertyType(string? raw) =>
        Enum.TryParse<PropertyType>(raw, ignoreCase: true, out var t) ? t : null;

    private static DateOnly? ParseDateOnly(string? raw) =>
        DateOnly.TryParse(raw, out var d) ? d : null;

    private List<SystemMessage> BuildSystemMessages() =>
    [
        new SystemMessage(SystemPromptText, new CacheControl { Type = CacheControlType.ephemeral })
    ];

    private static string BuildUserMessage(RawListing raw, Location location)
    {
        var areas = location.Areas.Count == 0 ? "(none specified)" : string.Join(", ", location.Areas);
        return $"""
                Location: {location.Name}, {location.Country}
                Currency: {location.Currency}
                Known areas: {areas}
                ---POST---
                {raw.Text}
                ---END---
                """;
    }

    private const string SystemPromptText = """
        You are an expert at parsing rental-housing posts from Facebook groups.
        Posts can be in English, Russian, Ukrainian, Thai, or mixed. The user is
        looking for LONG-TERM rentals (≥1 month). Treat short-term holiday rentals
        as NOT a match (is_rental_post = false).

        Always call the `extract_rental_listing` tool. Never reply with plain text.

        Rules:
        - `is_rental_post` = true ONLY if the post is offering a long-term rental
          listing (a place to live for ≥1 month). Anything else — for sale, wanted,
          short-stay, spam, questions — set false.
        - `confidence` 0.0-1.0: how sure you are about the classification AND
          extracted fields together. 0.9+ means clear. <0.5 means uncertain.
        - `price_per_month` is in the LOCATION CURRENCY (see user message).
          If the post quotes a different currency, convert at common-sense rates
          (e.g. USD → THB ≈ 35x). If unclear or "DM me" or "negotiable", set null.
          Strip commas/dots used as thousand separators.
        - `area` should match one of the "Known areas" if the post mentions it,
          otherwise null. Free-form area names that aren't in the list — still
          fill in if obvious (e.g. "Sri Thanu Beach" → "Sri Thanu").
        - `property_type` must be one of: Bungalow, House, Apartment, Studio,
          Villa, Room, or null if unclear.
        - `available_from` ISO date (YYYY-MM-DD). If post says "ASAP" or "now",
          use today's date. Null if not stated.
        - `contact_info` array: phone numbers, LINE IDs, WhatsApp, Telegram
          usernames mentioned in the post. Strip everything except the contact
          handle/number itself.

        Be conservative: if a field is ambiguous, null is better than wrong.
        """;

    private static Function BuildTool()
    {
        var schema = new InputSchema
        {
            Type = "object",
            Required = ["is_rental_post", "confidence"],
            Properties = new Dictionary<string, Property>
            {
                ["is_rental_post"] = new() { Type = "boolean", Description = "True if post offers long-term rental" },
                ["confidence"] = new() { Type = "number", Description = "0.0–1.0 confidence in classification + extraction" },
                ["price_per_month"] = new() { Type = "number", Description = "Monthly price in location currency, null if unclear" },
                ["area"] = new() { Type = "string", Description = "Neighbourhood / area name, null if unclear" },
                ["bedrooms"] = new() { Type = "integer", Description = "Number of bedrooms, null if unclear" },
                ["property_type"] = new()
                {
                    Type = "string",
                    Enum = ["Bungalow", "House", "Apartment", "Studio", "Villa", "Room"],
                    Description = "Property type, null if unclear"
                },
                ["pets_allowed"] = new() { Type = "boolean", Description = "Pets allowed flag, null if unstated" },
                ["has_pool"] = new() { Type = "boolean", Description = "Pool on premise, null if unstated" },
                ["has_hot_water"] = new() { Type = "boolean", Description = "Hot water available, null if unstated" },
                ["has_wifi"] = new() { Type = "boolean", Description = "WiFi included, null if unstated" },
                ["available_from"] = new() { Type = "string", Description = "ISO date YYYY-MM-DD, null if unstated" },
                ["contact_info"] = new() { Type = "array", Description = "Contact handles found in the post" }
            }
        };

        var jsonOpts = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var schemaJson = JsonSerializer.Serialize(schema, jsonOpts);
        return new Function(ToolName,
            "Extract structured fields from a Facebook rental-housing post.",
            JsonNode.Parse(schemaJson)!);
    }

    private sealed record ExtractedFields(
        bool IsRentalPost,
        double Confidence,
        decimal? PricePerMonth,
        string? Area,
        int? Bedrooms,
        string? PropertyType,
        bool? PetsAllowed,
        bool? HasPool,
        bool? HasHotWater,
        bool? HasWifi,
        string? AvailableFrom,
        List<string>? ContactInfo);
}
