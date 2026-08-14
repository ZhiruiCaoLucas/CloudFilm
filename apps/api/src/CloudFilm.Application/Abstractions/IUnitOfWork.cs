namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Runs several writes as one all-or-nothing operation.
/// </summary>
/// <remarks>
/// <para>
/// CloudFilm's first explicit transaction boundary, introduced for one specific
/// problem: a state change and its audit record must both happen or neither
/// must. A study that a clinician marked read with no audit row is an unrecorded
/// change to clinical workflow state; an audit row saying they marked it read
/// when the mark was never stored is a false entry in the trail. Both are worse
/// than a failed request.
/// </para>
/// <para>
/// This is not a repository and holds no entities. It is a boundary, and it
/// exists as a port so that neither the Application layer nor the endpoints have
/// to reference EF Core to open one. The implementation owns the connection and
/// the transaction; callers own only what happens inside.
/// </para>
/// <para>
/// The existing audit writer calls <c>SaveChangesAsync</c> itself. Inside this
/// boundary that call flushes into the open transaction instead of committing,
/// so the audit writer keeps its current behaviour unchanged and atomicity comes
/// from the boundary rather than from the order in which callers happen to write
/// things.
/// </para>
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Runs the operation inside a transaction and commits it.
    /// </summary>
    /// <typeparam name="TResult">What the operation produces.</typeparam>
    /// <param name="operation">The work to perform.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Whatever the operation returned, once the transaction has committed.</returns>
    /// <remarks>
    /// Any exception rolls the transaction back and propagates. Nothing is
    /// swallowed: a caller that cannot record what it did must not report
    /// success.
    /// </remarks>
    public Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
