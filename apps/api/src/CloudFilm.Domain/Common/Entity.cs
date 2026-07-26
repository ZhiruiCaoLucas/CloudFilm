namespace CloudFilm.Domain.Common;

/// <summary>
/// Base class for domain entities identified by a strongly typed identifier.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>
    /// Initialises a new instance of the <see cref="Entity{TId}"/> class.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    protected Entity(TId id) => Id = id;

    /// <summary>
    /// Gets the entity identifier.
    /// </summary>
    public TId Id { get; }

    /// <inheritdoc />
    public bool Equals(Entity<TId>? other) =>
        other is not null && GetType() == other.GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Entity<TId> entity && Equals(entity);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
