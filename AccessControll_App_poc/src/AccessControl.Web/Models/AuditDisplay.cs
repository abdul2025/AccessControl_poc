using System.Globalization;

namespace AccessControl.Web.Models;

/// <summary>Formats audit values the same way on every page (invariant culture, UTC).</summary>
public static class AuditDisplay
{
    public static string Format(DateTimeOffset? value) =>
        value is { } instant ? instant.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) : "—";

    /// <summary>Machine-readable value for <c>&lt;time datetime&gt;</c>.</summary>
    public static string Iso(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    public static string User(string? userId) => string.IsNullOrWhiteSpace(userId) ? "—" : userId;

    public static string Optional(object? value) => value?.ToString() is { Length: > 0 } text ? text : "—";
}
