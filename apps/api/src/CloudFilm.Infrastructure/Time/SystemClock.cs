using CloudFilm.Application.Abstractions;

namespace CloudFilm.Infrastructure.Time;

/// <summary>
/// The production <see cref="IClock"/>, backed by the host system clock.
/// </summary>
internal sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
