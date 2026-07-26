namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Provides the current time. Abstracted so that time dependent behaviour,
/// notably cache expiry and audit stamping, is deterministic under test.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current instant in UTC.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
