using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Domain.Catalog;

/// <summary>
/// Physical capabilities of one SIO model. Input and output numbers are zero-based (SDK commands 110 / 111);
/// labels are what is printed on the board.
/// </summary>
public sealed record SioModelSpec(
    SioModel Model,
    string DisplayName,
    IReadOnlyList<string> InputLabels,
    IReadOnlyList<string> OutputLabels,
    int ReaderPorts,
    int ReaderSlots,
    IReadOnlyDictionary<int, DedicatedInput> DedicatedInputs)
{
    public int Inputs => InputLabels.Count;
    public int Outputs => OutputLabels.Count;

    public DedicatedInput DedicatedFor(int inputNumber) =>
        DedicatedInputs.TryGetValue(inputNumber, out var dedicated) ? dedicated : DedicatedInput.None;
}

/// <summary>Physical capabilities of one controller model.</summary>
public sealed record ScpModelSpec(ScpModel Model, string DisplayName, SioModel InternalSio, int MaxSiosPerRs485Port);

/// <summary>
/// The hardware model catalog (docs/PROJECT_SPEC.md §3.7 rule 6). Sources: X1100 install guide PLT-04233
/// ("up to 4 readers, 7 inputs, 4 outputs"; TAMPER / AC FAIL / BATT FAIL inputs; RELAYS 1-4) and the Getting Started
/// guide ch. 04 (input numbering, nReaders = 4). Domain rules use it to create every SIO's points and to keep numbers
/// within what the hardware has.
/// </summary>
public static class HardwareCatalog
{
    /// <summary>SIOs per controller, matching the 1107 allocation nSio = 32 (SIO numbers 0-31).</summary>
    public const int MaxSiosPerScp = 32;

    /// <summary>Doors (ACRs) per controller, matching the 1107 allocation nAcr = 64.</summary>
    public const int MaxDoorsPerScp = 64;

    private static readonly Dictionary<ScpModel, ScpModelSpec> Scps = new()
    {
        [ScpModel.X1100] = new(ScpModel.X1100, "HID Aero X1100", SioModel.X1100Internal, MaxSiosPerRs485Port: 16),
    };

    private static readonly Dictionary<SioModel, SioModelSpec> Sios = new()
    {
        [SioModel.X1100Internal] = Board(SioModel.X1100Internal, "X1100 internal", generalInputs: 4, relays: 4, readerPorts: 2, readerSlots: 4),
        [SioModel.X100] = Board(SioModel.X100, "X100", generalInputs: 4, relays: 4, readerPorts: 2, readerSlots: 4),
        [SioModel.X200] = Board(SioModel.X200, "X200", generalInputs: 16, relays: 2, readerPorts: 0, readerSlots: 0),
        [SioModel.X300] = Board(SioModel.X300, "X300", generalInputs: 2, relays: 12, readerPorts: 0, readerSlots: 0),
    };

    public static ScpModelSpec For(ScpModel model) => Scps[model];

    public static SioModelSpec For(SioModel model) => Sios[model];

    public static bool IsKnown(ScpModel model) => Scps.ContainsKey(model);

    public static bool IsKnown(SioModel model) => Sios.ContainsKey(model);

    /// <summary>
    /// Every HID SIO board has general inputs IN 1..n followed by TAMPER, AC FAIL and BATT FAIL (Getting Started ch. 04).
    /// </summary>
    private static SioModelSpec Board(SioModel model, string displayName, int generalInputs, int relays, int readerPorts, int readerSlots)
    {
        var inputLabels = Enumerable.Range(1, generalInputs).Select(number => $"IN {number}")
            .Concat(["Tamper", "AC fail", "Battery fail"])
            .ToArray();
        var outputLabels = Enumerable.Range(1, relays).Select(number => $"Relay {number}").ToArray();
        var dedicated = new Dictionary<int, DedicatedInput>
        {
            [generalInputs] = DedicatedInput.Tamper,
            [generalInputs + 1] = DedicatedInput.AcFail,
            [generalInputs + 2] = DedicatedInput.BatteryFail,
        };

        return new SioModelSpec(model, displayName, inputLabels, outputLabels, readerPorts, readerSlots, dedicated);
    }
}
