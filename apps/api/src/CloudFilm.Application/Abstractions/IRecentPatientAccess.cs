using CloudFilm.Application.Access;

namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Answers one question: which patients has this user opened recently.
/// </summary>
/// <remarks>
/// Deliberately not a general audit query. There is no action filter, no
/// resource-type filter, no date range and no page size. The port has exactly
/// the shape of the feature it exists for, so a future endpoint cannot widen it
/// by accident.
///
/// The actor user ID is passed as a parameter rather than resolved by the
/// implementation. The caller has already validated the bearer token and
/// knows who is asking. This keeps identity validation in one place.
/// </remarks>
public interface IRecentPatientAccess
{
    /// <summary>
    /// Lists the patients an account opened, most recently first.
    /// </summary>
    /// <param name="actorUserId">
    /// The account asking. Comes from the validated <c>sub</c> claim and never
    /// from anything the client supplied.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the operation.
    /// </param>
    /// <returns>
    /// Up to <see cref="RecentPatientAccess.MaxResults"/> distinct patients,
    /// ordered by the most recent time this account opened each one.
    /// </returns>
    public Task<IReadOnlyList<RecentPatient>> GetRecentPatientsAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
