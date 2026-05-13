using System.Text.Json.Nodes;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RR.Core.Abstractions;
using RR.Core.Domain;
using RR.Infrastructure.Ai;
using Xunit;

namespace RR.IntegrationTests;

public sealed class ClaudeAiListingExtractorTests
{
    private static readonly Location TestLocation = new()
    {
        Name = "Ko Phangan",
        Slug = "ko-phangan",
        Country = "TH",
        Currency = "THB",
        Areas = ["Sri Thanu", "Tong Sala"]
    };

    private static readonly RawListing TestRaw = new(
        SourceId: Guid.NewGuid(),
        ExternalId: "abc123",
        SourceUrl: "https://facebook.com/posts/abc123",
        Text: "Studio in Sri Thanu, 12000 THB/month, pet friendly, available now",
        AuthorName: "Test Author",
        AuthorProfileUrl: null,
        ImageUrls: ["https://scontent.com/img1.jpg"],
        PostedAt: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

    private static ClaudeAiListingExtractor BuildExtractor(IClaudeClient client) =>
        new(client, Options.Create(new AnthropicOptions()), NullLogger<ClaudeAiListingExtractor>.Instance);

    [Fact]
    public async Task Rental_post_maps_all_fields_to_listing()
    {
        var fake = new FakeClaudeClient(_ => ResponseWithFields("""
            {
              "is_rental_post": true,
              "confidence": 0.92,
              "price_per_month": 12000,
              "area": "Sri Thanu",
              "bedrooms": 0,
              "property_type": "Studio",
              "pets_allowed": true,
              "has_pool": null,
              "has_hot_water": true,
              "has_wifi": true,
              "available_from": "2026-05-10",
              "contact_info": ["+66 89 123 4567"]
            }
            """));

        var extractor = BuildExtractor(fake);
        var listing = await extractor.ExtractAsync(TestRaw, TestLocation);

        Assert.NotNull(listing);
        Assert.Equal(TestRaw.ExternalId, listing.ExternalId);
        Assert.Equal(TestRaw.Text, listing.RawText);
        Assert.Equal(12000m, listing.PricePerMonth);
        Assert.Equal("Sri Thanu", listing.Area);
        Assert.Equal(0, listing.Bedrooms);
        Assert.Equal(PropertyType.Studio, listing.PropertyType);
        Assert.True(listing.PetsAllowed);
        Assert.Null(listing.HasPool);
        Assert.True(listing.HasHotWater);
        Assert.True(listing.HasWifi);
        Assert.Equal(new DateOnly(2026, 5, 10), listing.AvailableFrom);
        Assert.Single(listing.ContactInfo);
        Assert.Equal(0.92, listing.ConfidenceScore);
    }

    [Fact]
    public async Task Non_rental_post_returns_null()
    {
        var fake = new FakeClaudeClient(_ => ResponseWithFields("""
            {"is_rental_post": false, "confidence": 0.85}
            """));

        var extractor = BuildExtractor(fake);
        var listing = await extractor.ExtractAsync(TestRaw, TestLocation);

        Assert.Null(listing);
    }

    [Fact]
    public async Task Api_exception_returns_null_without_throwing()
    {
        var fake = new FakeClaudeClient(_ => throw new HttpRequestException("simulated 429"));

        var extractor = BuildExtractor(fake);
        var listing = await extractor.ExtractAsync(TestRaw, TestLocation);

        Assert.Null(listing);
    }

    [Fact]
    public async Task Missing_tool_use_block_returns_null()
    {
        var fake = new FakeClaudeClient(_ => new MessageResponse
        {
            Content = new List<ContentBase>
            {
                new TextContent { Text = "I'm not calling the tool, sorry." }
            }
        });

        var extractor = BuildExtractor(fake);
        var listing = await extractor.ExtractAsync(TestRaw, TestLocation);

        Assert.Null(listing);
    }

    private static MessageResponse ResponseWithFields(string fieldsJson) => new()
    {
        Content = new List<ContentBase>
        {
            new ToolUseContent
            {
                Id = "tool_use_id",
                Name = "extract_rental_listing",
                Input = JsonNode.Parse(fieldsJson)!
            }
        }
    };
}

internal sealed class FakeClaudeClient(Func<MessageParameters, MessageResponse> behaviour) : IClaudeClient
{
    public Task<MessageResponse> GetMessageAsync(MessageParameters parameters, CancellationToken ct = default) =>
        Task.FromResult(behaviour(parameters));
}
