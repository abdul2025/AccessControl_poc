using SiteEntity = AccessControl.Domain.Entities.Site;

namespace AccessControl.Application.Features.Sites;

public static class SiteErrors
{
    public const string NotFound = "Site.NotFound";
    public const string Validation = SiteEntity.ValidationErrorCode;
    public const string DuplicateCode = "Site.DuplicateCode";
    public const string DuplicateName = "Site.DuplicateName";
    public const string HasScps = "Site.HasScps";
    public const string ConcurrencyConflict = "Site.ConcurrencyConflict";

    public const string NotFoundMessage = "The site was not found.";
    public const string DuplicateCodeMessage = "Another site already uses this code.";
    public const string DuplicateNameMessage = "Another site already uses this name.";
    public const string HasScpsMessage = "This site still has controllers. Move or delete them before deleting the site.";
    public const string ConcurrencyConflictMessage =
        "This site was changed by someone else after you opened it. Reload and try again.";
}
