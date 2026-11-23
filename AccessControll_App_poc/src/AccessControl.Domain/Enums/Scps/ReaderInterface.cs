namespace AccessControl.Domain.Enums.Scps;

/// <summary>
/// How a reader talks to the controller (command 112). OSDP is the target for the VL70LF (spec §7.1);
/// Wiegand is the fallback until the reader's interface is confirmed (open item 1).
/// </summary>
public enum ReaderInterface
{
    Osdp = 1,
    Wiegand = 2,
}
