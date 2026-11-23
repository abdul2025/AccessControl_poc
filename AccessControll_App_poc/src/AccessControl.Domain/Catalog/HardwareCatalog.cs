using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Domain.Catalog;

/// <summary>Physical capabilities of one SIO model (inputs, outputs, reader ports).</summary>
public sealed record SioModelSpec(SioModel Model, string DisplayName, int Inputs, int Outputs, int ReaderPorts);

/// <summary>Physical capabilities of one controller model.</summary>
public sealed record ScpModelSpec(ScpModel Model, string DisplayName, SioModel InternalSio, int MaxSiosPerRs485Port);

/// <summary>
/// The hardware model catalog (docs/PROJECT_SPEC.md §3.7 rule 6, Getting Started guide ch. 04). Domain rules use it
/// to create built-in SIOs and to keep point and reader numbers within what the hardware has.
/// </summary>
public static class HardwareCatalog
{
    private static readonly Dictionary<ScpModel, ScpModelSpec> Scps = new()
    {
        [ScpModel.X1100] = new(ScpModel.X1100, "HID Aero X1100", SioModel.X1100Internal, MaxSiosPerRs485Port: 16),
    };

    private static readonly Dictionary<SioModel, SioModelSpec> Sios = new()
    {
        [SioModel.X1100Internal] = new(SioModel.X1100Internal, "X1100 internal", Inputs: 7, Outputs: 4, ReaderPorts: 2),
        [SioModel.X100] = new(SioModel.X100, "X100", Inputs: 7, Outputs: 4, ReaderPorts: 2),
        [SioModel.X200] = new(SioModel.X200, "X200", Inputs: 19, Outputs: 2, ReaderPorts: 0),
        [SioModel.X300] = new(SioModel.X300, "X300", Inputs: 5, Outputs: 12, ReaderPorts: 0),
    };

    public static ScpModelSpec For(ScpModel model) => Scps[model];

    public static SioModelSpec For(SioModel model) => Sios[model];

    public static bool IsKnown(ScpModel model) => Scps.ContainsKey(model);
}
