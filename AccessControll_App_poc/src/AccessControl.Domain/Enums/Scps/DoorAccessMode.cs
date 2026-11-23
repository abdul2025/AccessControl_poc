namespace AccessControl.Domain.Enums.Scps;

/// <summary>
/// How a door grants access. Values are the SDK ACR modes for command 115 <c>default_mode</c> (SDK §8.2 / §8.8).
/// Only the modes offered in the UI are listed; others (facility code only, PIN only) can be added when needed.
/// </summary>
public enum DoorAccessMode
{
    Unlocked = 2,
    Locked = 3,
    CardOnly = 5,
    CardAndPin = 7,
    CardOrPin = 8,
}
