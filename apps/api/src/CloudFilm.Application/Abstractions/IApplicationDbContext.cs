namespace CloudFilm.Application.Abstractions;

/// <summary>
/// The application's unit of work.
/// </summary>
/// <remarks>
/// Use cases depend on this port rather than on the concrete EF Core context so
/// that the persistence technology remains replaceable. Entity sets are added to
/// this interface as feature modules are introduced.
/// </remarks>
public interface IApplicationDbContext
{
    /// <summary>
    /// Persists all pending changes as a single transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
