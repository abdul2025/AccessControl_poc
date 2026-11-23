namespace AccessControl.Domain.Enums.Scps;

/// <summary>Where an SIO is connected on the controller (command 108 port: internal = 3, RS-485 = 1 / 2).</summary>
public enum SioPort
{
    Internal = 1,
    Rs485Port1 = 2,
    Rs485Port2 = 3,
}
