using AccessControl.Infrastructure.Aero.Driver;
using AccessControl.Infrastructure.Aero.Messages;
using AccessControl.Infrastructure.Aero.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AccessControl.Infrastructure.Aero;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the HID Aero driver integration. Call only from the Gateway host: the driver is a
    /// single-instance native Windows x64 component that owns the controller listener (spec §3.1).
    /// All services are singletons because they share the one driver instance in the process.
    /// </summary>
    public static IServiceCollection AddAeroDriver(this IServiceCollection services, IConfiguration configuration)
    {
        // Invalid driver settings stop the host at startup instead of failing on first connection.
        services.AddOptions<AeroOptions>()
            .Bind(configuration.GetSection(AeroOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AeroDriver>();
        services.AddSingleton<PendingCommandRegistry>();
        services.AddSingleton<AeroCommandWriter>();
        services.AddSingleton<AeroMessagePump>();
        services.AddSingleton<ControllerSessionTracker>();
        services.AddSingleton<ControllerAcceptance>();

        return services;
    }
}
