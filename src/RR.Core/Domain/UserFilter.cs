namespace RR.Core.Domain;

/// <summary>
/// Фільтр користувача — критерії за якими шукати оголошення.
/// Один користувач може мати кілька активних фільтрів.
/// </summary>
public sealed class UserFilter
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required long TelegramChatId { get; init; }
    public required Guid LocationId { get; init; }            // фільтр завжди в межах однієї локації
    public required string Name { get; set; }  // "Студія Шрітану до 12к"

    public decimal? MaxPrice { get; set; }     // у валюті Location
    public decimal? MinPrice { get; set; }
    public List<string> Areas { get; init; } = new();
    public List<PropertyType> PropertyTypes { get; init; } = new();
    public int? MinBedrooms { get; set; }
    public bool? RequirePetsAllowed { get; set; }
    public bool? RequirePool { get; set; }
    public bool? RequireHotWater { get; set; }

    /// <summary>
    /// Природна мова для семантичної фільтрації через Claude API.
    /// Наприклад: "тихе місце поруч з джунглями, не на головній дорозі"
    /// </summary>
    public string? SemanticQuery { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastMatchedAt { get; set; }
}
