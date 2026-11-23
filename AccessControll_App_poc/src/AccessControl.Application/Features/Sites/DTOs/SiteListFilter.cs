namespace AccessControl.Application.Features.Sites.DTOs;

/// <summary>Optional list filters; untrusted query-string input is normalized here, once.</summary>
public sealed record SiteListFilter
{
    public const int MaxSearchLength = 100;

    public static readonly SiteListFilter None = new();

    public SiteListFilter(string? search = null)
    {
        var trimmed = search?.Trim();
        Search = string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.Length > MaxSearchLength ? trimmed[..MaxSearchLength] : trimmed;
    }

    public string? Search { get; }

    public bool IsEmpty => Search is null;
}
