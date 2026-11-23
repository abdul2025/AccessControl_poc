using AccessControl.Domain.Common.Entities;
using AccessControl.Domain.Common.Results;

namespace AccessControl.Domain.Entities;

/// <summary>A location (branch, airport, building) that groups controllers. The code is unique and upper-case.</summary>
public sealed class Site : AuditableEntity, IHasDisplayName
{
    public const string ValidationErrorCode = "Site.Validation";
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 2000;

    private Site()
    {
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    public string DisplayName => Code;

    public static Result<Site> Create(string code, string name, string? description)
    {
        var site = new Site();
        var result = site.Apply(code, name, description);

        return result.IsSuccess
            ? Result<Site>.Success(site)
            : Result<Site>.Failure(result.ErrorCode!, result.ErrorMessage!);
    }

    public Result Update(string code, string name, string? description) => Apply(code, name, description);

    /// <summary>Codes compare case-insensitively, so they are stored upper-case and trimmed.</summary>
    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static string NormalizeName(string? name) => (name ?? string.Empty).Trim();

    private Result Apply(string code, string name, string? description)
    {
        var normalizedCode = NormalizeCode(code);
        var trimmedName = NormalizeName(name);
        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (normalizedCode.Length is 0 or > CodeMaxLength)
        {
            return Invalid($"Code is required and must be at most {CodeMaxLength} characters.");
        }

        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            return Invalid($"Name is required and must be at most {NameMaxLength} characters.");
        }

        if (trimmedDescription is { Length: > DescriptionMaxLength })
        {
            return Invalid($"Description must be at most {DescriptionMaxLength} characters.");
        }

        Code = normalizedCode;
        Name = trimmedName;
        Description = trimmedDescription;

        return Result.Success();
    }

    private static Result Invalid(string message) => Result.Failure(ValidationErrorCode, message);
}
