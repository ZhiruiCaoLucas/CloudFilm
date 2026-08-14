using CloudFilm.Application.Abstractions;

using Microsoft.EntityFrameworkCore.Storage;

namespace CloudFilm.Infrastructure.Persistence;

/// <summary>
/// Runs an operation inside a PostgreSQL transaction on the request's context.
/// </summary>
/// <remarks>
/// <para>
/// Everything that shares the request's <see cref="CloudFilmDbContext"/> - the
/// read-state store and the audit writer alike - shares its connection, so a
/// transaction opened here encloses all of them. That is what makes a state
/// change and its audit record atomic without changing either of the components
/// that perform them.
/// </para>
/// <para>
/// The audit writer calls <c>SaveChangesAsync</c> itself, which normally
/// commits. With a transaction already open on the connection, EF Core does not
/// start its own: the save flushes into the ambient transaction and the commit
/// happens here, once, after the whole operation has succeeded. So the audit
/// writer keeps the exact behaviour it had before and needs no change.
/// </para>
/// <para>
/// No retry loop. The connection is not configured with
/// <c>EnableRetryOnFailure</c>, so there is no execution strategy that would
/// object to a manually opened transaction. If retries are ever enabled, this is
/// the class that has to wrap the operation in
/// <c>Database.CreateExecutionStrategy()</c>, and the transaction here is the
/// reason.
/// </para>
/// </remarks>
internal sealed class TransactionalUnitOfWork(CloudFilmDbContext context) : IUnitOfWork
{
    private readonly CloudFilmDbContext _context = context;

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Nested boundaries are not supported and must not be silently ignored:
        // committing an inner scope would commit the outer one's partial work.
        // Nothing nests today, and this makes a future attempt fail loudly here
        // rather than quietly commit half an operation.
        if (_context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "A transaction is already open on this context; nested units of work are not supported.");
        }

        await using IDbContextTransaction transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // No try/catch. Disposing an uncommitted transaction rolls it back, so an
        // exception - from the mutation, from the audit write, or from the commit
        // itself - leaves the database as it was and propagates to the caller.
        TResult result = await operation(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}
