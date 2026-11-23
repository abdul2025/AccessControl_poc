using AccessControl.Application.Features.Scps;
using AccessControl.Application.Features.Scps.Interfaces;
using AccessControl.Application.Features.Sites;
using AccessControl.Application.Features.Sites.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AccessControl.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<ISiteService, SiteService>();
        services.AddScoped<IScpService, ScpService>();

        return services;
    }
}
