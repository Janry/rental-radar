using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.McpServer.Tools;

[McpServerToolType]
public sealed class FilterManagementTools(
    IUserFilterRepository filters,
    ILocationRepository locations)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    [McpServerTool(Name = "create_notification_filter")]
    [Description("Створити фільтр для отримання Telegram нотифікацій про нові оголошення " +
                 "в межах певної локації. Користувач отримуватиме повідомлення в реальному часі.")]
    public async Task<string> CreateFilterAsync(
        [Description("Telegram chat ID")] long telegramChatId,
        [Description("ID або slug локації ('ko-phangan')")] string location,
        [Description("Зрозуміла назва фільтру: 'Студія Шрітану до 12к'")] string name,
        [Description("Максимальна ціна за міс у валюті локації")] decimal? maxPrice = null,
        [Description("Райони в межах локації")] string[]? areas = null,
        [Description("Типи житла")] string[]? propertyTypes = null,
        [Description("Семантичний запит природньою мовою для AI-фільтрації " +
                     "(напр.: 'тихе місце поруч з джунглями')")] string? semanticQuery = null,
        CancellationToken ct = default)
    {
        var loc = Guid.TryParse(location, out var lid)
            ? await locations.GetByIdAsync(lid, ct)
            : await locations.GetBySlugAsync(location.ToLowerInvariant(), ct);

        if (loc is null)
            return JsonSerializer.Serialize(new { error = $"Location '{location}' not found" });

        var filter = new UserFilter
        {
            TelegramChatId = telegramChatId,
            LocationId = loc.Id,
            Name = name,
            MaxPrice = maxPrice,
            Areas = areas?.ToList() ?? new(),
            PropertyTypes = propertyTypes?
                .Where(p => Enum.TryParse<PropertyType>(p, true, out _))
                .Select(p => Enum.Parse<PropertyType>(p, true))
                .ToList() ?? new(),
            SemanticQuery = semanticQuery
        };

        await filters.AddAsync(filter, ct);

        return JsonSerializer.Serialize(new
        {
            success = true,
            filter_id = filter.Id,
            location = loc.Name,
            currency = loc.Currency,
            message = $"Активовано фільтр '{name}' для {loc.Name}. Нотифікації — в Telegram."
        }, JsonOpts);
    }

    [McpServerTool(Name = "list_my_filters")]
    [Description("Показати всі активні фільтри користувача.")]
    public async Task<string> ListFiltersAsync(
        [Description("Telegram chat ID користувача")] long telegramChatId,
        CancellationToken ct = default)
    {
        var list = await filters.GetByChatIdAsync(telegramChatId, ct);
        return JsonSerializer.Serialize(new { count = list.Count, filters = list }, JsonOpts);
    }

    [McpServerTool(Name = "delete_filter")]
    [Description("Видалити фільтр за його ID.")]
    public async Task<string> DeleteFilterAsync(
        [Description("ID фільтру (GUID)")] string filterId,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(filterId, out var id))
            return JsonSerializer.Serialize(new { error = "Invalid filter ID" });

        await filters.DeleteAsync(id, ct);
        return JsonSerializer.Serialize(new { success = true });
    }
}
