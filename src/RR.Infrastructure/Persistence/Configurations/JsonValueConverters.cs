using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RR.Infrastructure.Persistence.Configurations;

/// <summary>
/// SQLite не має нативних масивів, тому колекції зберігаємо як JSON-рядок.
/// ValueComparer потрібен, щоб change-tracker EF Core помічав мутації списку.
/// </summary>
internal static class JsonValueConverters
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public static ValueConverter<List<string>, string> StringList { get; } = new(
        v => JsonSerializer.Serialize(v, JsonOpts),
        v => JsonSerializer.Deserialize<List<string>>(v, JsonOpts) ?? new());

    public static ValueComparer<List<string>> StringListComparer { get; } = new(
        (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
        v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
        v => v.ToList());

    public static ValueConverter<List<T>, string> EnumList<T>() where T : struct, Enum => new(
        v => JsonSerializer.Serialize(v.Select(e => e.ToString()).ToList(), JsonOpts),
        v => (JsonSerializer.Deserialize<List<string>>(v, JsonOpts) ?? new())
            .Select(s => Enum.Parse<T>(s)).ToList());

    public static ValueComparer<List<T>> EnumListComparer<T>() where T : struct, Enum => new(
        (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
        v => v.Aggregate(0, (h, e) => HashCode.Combine(h, e.GetHashCode())),
        v => v.ToList());
}
