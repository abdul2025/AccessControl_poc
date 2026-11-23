using AccessControl.Application.Common.Interfaces;
using AccessControl.Application.Features.Scps.Ports;
using AccessControl.Application.Features.Sites.Ports;
using AccessControl.Infrastructure.Persistence.Features.Scps;
using AccessControl.Infrastructure.Persistence.Features.Sites;
using AccessControl.Infrastructure.Auditing;
using AccessControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AccessControl.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "AccessControl";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Required configuration 'ConnectionStrings:{ConnectionStringName}' is missing.");
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AccessControlDbContext>((serviceProvider, options) =>
            options
                .UseSqlServer(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<ISiteRepository, SiteRepository>();
        services.AddScoped<IScpRepository, ScpRepository>();

        return services;
    }
}
