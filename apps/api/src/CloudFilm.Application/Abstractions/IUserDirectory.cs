namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Answers whether an account exists.
/// </summary>
/// <remarks>
/// <para>
/// One question, and deliberately only one. Whether an account exists is enough
/// to refuse a grant to nobody; anything more - an address, a name, a list -
/// would make this a directory that could be read, and CloudFilm has no feature
/// that needs one.
/// </para>
/// <para>
/// The answer must never reach a client on its own. A caller that returns "no
/// such user" has built an account enumerator, which is exactly what the
/// sign-in path is written to avoid; callers refuse such requests the same way
/// they refuse any other malformed one.
/// </para>
/// </remarks>
public interface IUserDirectory
{
    /// <summary>
    /// Reports whether an account with this identifier exists.
    /// </summary>
    /// <param name="userId">The account identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the account exists.</returns>
    public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default);
}
