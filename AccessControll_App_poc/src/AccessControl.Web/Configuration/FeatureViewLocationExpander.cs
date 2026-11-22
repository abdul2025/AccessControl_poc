using Microsoft.AspNetCore.Mvc.Razor;

namespace AccessControl.Web.Configuration;

/// <summary>
/// Resolves views from <c>Features/{Controller}/Views/</c> and shared views from <c>Shared/Views/</c>
/// (FEATURE-PATTERN "Presentation (MVC host)") before the framework's default locations.
/// </summary>
public sealed class FeatureViewLocationExpander : IViewLocationExpander
{
    private static readonly string[] FeatureLocations =
    [
        "/Features/{1}/Views/{0}.cshtml",
        "/Shared/Views/{0}.cshtml",
    ];

    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations) =>
        FeatureLocations.Concat(viewLocations);
}
