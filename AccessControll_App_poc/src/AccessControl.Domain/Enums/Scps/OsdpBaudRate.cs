namespace AccessControl.Domain.Enums.Scps;

/// <summary>OSDP line speed. Values are the SDK codes for command 112 <c>osdp_flags</c> bits 0-2 (SDK §6.7).</summary>
public enum OsdpBaudRate
{
    Baud9600 = 1,
    Baud19200 = 2,
    Baud38400 = 3,
    Baud115200 = 4,
    Baud57600 = 5,
    Baud230400 = 6,
}
