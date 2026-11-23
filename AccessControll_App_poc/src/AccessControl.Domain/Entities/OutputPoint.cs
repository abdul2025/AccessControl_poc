using AccessControl.Domain.Common.Entities;
using AccessControl.Domain.Common.Results;

namespace AccessControl.Domain.Entities;

/// <summary>
/// A relay on an SIO (command 111): drives a door lock or, as a control point, auxiliary equipment (command 114).
/// Part of the <see cref="Scp"/> aggregate; changed only through it.
/// </summary>
public sealed class OutputPoint : BaseEntity
{
    public const int NameMaxLength = 100;
    public const short DefaultPulseSeconds = 1;
    public const short MaxPulseSeconds = 255;

    private OutputPoint()
    {
    }

    public short Number { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>How long a momentary activation keeps the relay on (command 114 dflt_pulse).</summary>
    public short PulseSeconds { get; private set; }

    internal static OutputPoint Create(short number, string name) => new()
    {
        Number = number,
        Name = name,
        PulseSeconds = DefaultPulseSeconds,
    };

    internal Result Update(string name, short pulseSeconds)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            return Result.Failure(Scp.ValidationErrorCode, $"Output name is required and must be at most {NameMaxLength} characters.");
        }

        if (pulseSeconds is < 1 or > MaxPulseSeconds)
        {
            return Result.Failure(Scp.ValidationErrorCode, $"Pulse time must be between 1 and {MaxPulseSeconds} seconds.");
        }

        Name = trimmedName;
        PulseSeconds = pulseSeconds;
        return Result.Success();
    }
}
