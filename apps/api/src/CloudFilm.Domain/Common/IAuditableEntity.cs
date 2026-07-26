namespace CloudFilm.Domain.Common;

/// <summary>
/// Marks an entity whose creation and modification timestamps are maintained
/// automatically by the persistence layer.
/// </summary>
/// <remarks>
/// Audit stamping is applied centrally by the persistence interceptor so that no
/// application or domain code has to remember to set these values.
/// </remarks>
public interface IAuditableEntity
{
    /// <summary>
    /// Gets or sets the UTC instant at which the entity was created.
    /// </summary>
    DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC instant at which the entity was last modified.
    /// </summary>
    DateTimeOffset? ModifiedAtUtc { get; set; }
}
