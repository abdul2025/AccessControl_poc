using AccessControl.Domain.Catalog;
using AccessControl.Domain.Common.Entities;
using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Domain.Entities;

/// <summary>
/// A serial I/O board on a controller (command 109): the X1100 built-in SIO, or an external X100/X200/X300 on an
/// RS-485 port. Part of the <see cref="Scp"/> aggregate; created and changed only through it.
/// </summary>
public sealed class SioPanel : BaseEntity
{
    public const int NameMaxLength = 100;
    public const short MaxRs485Address = 15;

    private SioPanel()
    {
    }

    public short Number { get; private set; }
    public SioModel Model { get; private set; }
    public SioPort Port { get; private set; }
    public short Address { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>The controller built-in SIO: number 0, internal port, address 0 (spec §4).</summary>
    internal static SioPanel CreateInternal(SioModel model) => new()
    {
        Number = 0,
        Model = model,
        Port = SioPort.Internal,
        Address = 0,
        Name = "Internal I/O",
    };
}
