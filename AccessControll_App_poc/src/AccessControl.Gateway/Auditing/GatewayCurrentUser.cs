using AccessControl.Application.Common.Interfaces;

namespace AccessControl.Gateway.Auditing;

/// <summary>Identity stamped on rows the Gateway writes (events, controller status, command results).</summary>
internal sealed class GatewayCurrentUser : ICurrentUser
{
    public const string SystemUserId = "system:gateway";

    public string? UserId => SystemUserId;
}
