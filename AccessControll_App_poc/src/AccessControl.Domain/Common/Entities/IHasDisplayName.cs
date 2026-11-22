namespace AccessControl.Domain.Common.Entities;

/// <summary>A human-readable name used wherever the entity is mentioned to a person (logs, notifications).</summary>
public interface IHasDisplayName
{
    string DisplayName { get; }
}
