using AccessControl.Application.Common.Interfaces;
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

        // One line per feature:
        // services.AddScoped<I{Entity}Repository, {Entity}Repository>();

        return services;
    }
}
