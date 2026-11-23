using AccessControl.Domain.Catalog;
using AccessControl.Domain.Common.Entities;
using AccessControl.Domain.Common.Results;
using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Domain.Entities;

/// <summary>
/// A serial I/O board on a controller (command 109): the X1100 built-in SIO, or an external X100/X200/X300 on an
/// RS-485 port. Created with all of its physical inputs and outputs from the hardware catalog. Part of the
/// <see cref="Scp"/> aggregate; created and changed only through it.
/// </summary>
public sealed class SioPanel : BaseEntity
{
    public const int NameMaxLength = 100;
    public const short MaxRs485Address = 15;

    private readonly List<InputPoint> _inputs = [];
    private readonly List<OutputPoint> _outputs = [];

    private SioPanel()
    {
    }

    public short Number { get; private set; }
    public SioModel Model { get; private set; }
    public SioPort Port { get; private set; }
    public short Address { get; private set; }
    public string Name { get; private set; } = null!;

    public IReadOnlyList<InputPoint> Inputs => _inputs;
    public IReadOnlyList<OutputPoint> Outputs => _outputs;

    public bool IsInternal => Port == SioPort.Internal;

    /// <summary>The controller built-in SIO: number 0, internal port, address 0 (spec §4).</summary>
    internal static SioPanel CreateInternal(SioModel model) => CreateWithPoints(0, model, SioPort.Internal, 0, "Internal I/O");

    /// <summary>An external board on RS-485 port 1 or 2; port, address and model are validated by the controller.</summary>
    internal static SioPanel CreateExternal(short number, SioModel model, SioPort port, short address, string name) =>
        CreateWithPoints(number, model, port, address, name);

    internal static string NormalizeName(string? name) => (name ?? string.Empty).Trim();

    internal Result Rename(string name)
    {
        var trimmed = NormalizeName(name);
        if (trimmed.Length is 0 or > NameMaxLength)
        {
            return Result.Failure(Scp.ValidationErrorCode, $"SIO name is required and must be at most {NameMaxLength} characters.");
        }

        Name = trimmed;
        return Result.Success();
    }

    private static SioPanel CreateWithPoints(short number, SioModel model, SioPort port, short address, string name)
    {
        var spec = HardwareCatalog.For(model);
        var panel = new SioPanel
        {
            Number = number,
            Model = model,
            Port = port,
            Address = address,
            Name = name,
        };

        // Every physical point exists from the start (commands 110 / 111 define all of them); users only name and tune them.
        for (short input = 0; input < spec.Inputs; input++)
        {
            panel._inputs.Add(InputPoint.Create(input, spec.InputLabels[input], spec.DedicatedFor(input)));
        }

        for (short output = 0; output < spec.Outputs; output++)
        {
            panel._outputs.Add(OutputPoint.Create(output, spec.OutputLabels[output]));
        }

        return panel;
    }
}
