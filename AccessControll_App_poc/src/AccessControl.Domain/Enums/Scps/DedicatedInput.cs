namespace AccessControl.Domain.Enums.Scps;

/// <summary>Inputs wired to fixed equipment on every HID SIO board (TAMPER, AC FAIL, BATT FAIL terminals).</summary>
public enum DedicatedInput
{
    None = 1,
    Tamper = 2,
    AcFail = 3,
    BatteryFail = 4,
}
