namespace RR.Core.Domain;

/// <summary>
/// Оголошення про оренду житла. Центральна сутність системи.
/// </summary>
public sealed class Listing
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Локація, до якої прив'язане оголошення.</summary>
    public required Guid LocationId { get; init; }

    /// <summary>Джерело, з якого витягнули — посилання на ScrapeSource.</summary>
    public required Guid SourceId { get; init; }

    /// <summary>Унікальний ID посту з джерела (для дедуплікації в межах джерела).</summary>
    public required string ExternalId { get; init; }

    public required string SourceUrl { get; init; }

    public required string RawText { get; init; }
    public string? AuthorName { get; init; }
    public string? AuthorProfileUrl { get; init; }

    // Структуровані поля — заповнюються AI-фільтром
    public decimal? PricePerMonth { get; set; }    // у валюті Location (Location.Currency)
    public string? Area { get; set; }              // район в межах Location.Areas
    public int? Bedrooms { get; set; }
    public PropertyType? PropertyType { get; set; }
    public bool? PetsAllowed { get; set; }
    public bool? HasPool { get; set; }
    public bool? HasHotWater { get; set; }
    public bool? HasWifi { get; set; }
    public DateOnly? AvailableFrom { get; set; }
    public DateOnly? AvailableUntil { get; set; }

    public List<string> ImageUrls { get; init; } = new();
    public List<string> ContactInfo { get; init; } = new(); // phone, line, whatsapp

    public DateTime PostedAt { get; init; }
    public DateTime ScrapedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    /// <summary>0.0 - 1.0, наскільки AI впевнений, що це справді оголошення про оренду.</summary>
    public double ConfidenceScore { get; set; }
}

public enum PropertyType
{
    Bungalow,
    House,
    Apartment,
    Studio,
    Villa,
    Room
}
