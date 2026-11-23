namespace AccessControl.Domain.Enums.Scps;

/// <summary>
/// How the Gateway and the controller connect (docs/PROJECT_SPEC.md §5). IP Client: the controller calls the
/// Gateway and is identified by serial number. IP Server: the Gateway connects to the controller's IP address.
/// </summary>
public enum ConnectionMode
{
    IpClient = 1,
    IpServer = 2,
}
