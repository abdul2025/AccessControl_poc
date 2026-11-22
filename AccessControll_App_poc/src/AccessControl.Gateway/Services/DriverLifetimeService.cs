using AccessControl.Infrastructure.Aero.Driver;
using AccessControl.Infrastructure.Aero.Messages;

namespace AccessControl.Gateway.Services;

/// <summary>
/// Starts the HID Aero driver (11, 12) and its message loop with the host and shuts both down (203) when the
/// host stops. A startup failure throws, which stops the host with the reason in the log (fail fast).
/// Registered before <see cref="ReplyProcessingService"/> so it starts first and stops last.
/// </summary>
internal sealed class DriverLifetimeService(AeroDriver driver, AeroMessagePump pump) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        driver.Start();
        pump.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop reading first so no reply is processed against a closed driver, then close the channels.
        pump.Stop();
        driver.Stop();
        return Task.CompletedTask;
    }
}
