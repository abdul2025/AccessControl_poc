namespace AccessControl.Application.Features.Scps.DTOs;

/// <summary>Optional list filters; untrusted query-string input is normalized here, once.</summary>
public sealed record ScpListFilter
{
    public const int MaxSearchLength = 100;

    public static readonly ScpListFilter None = new();

    public ScpListFilter(string? search = null, Guid? siteId = null)
    {
        var trimmed = search?.Trim();
        Search = string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.Length > MaxSearchLength ? trimmed[..MaxSearchLength] : trimmed;
        SiteId = siteId is { } value && value != Guid.Empty ? value : null;
    }

    public string? Search { get; }
    public Guid? SiteId { get; }

    public bool IsEmpty => Search is null && SiteId is null;
}
