using CloudFilm.Application.Imaging;

namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Searches the imaging catalogue for studies.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="IImagingCatalog"/> rather than a method on it.
/// Searching is a different question from "fetch this patient" or "fetch this
/// study", it arrives later than either, and every existing implementer of
/// <see cref="IImagingCatalog"/> would otherwise have to grow a method it does
/// not yet answer. Narrow ports have been the pattern since
/// <see cref="IRecentPatientAccess"/>.
/// </para>
/// <para>
/// Read-only, like the rest of the imaging surface. CloudFilm never writes to an
/// archive.
/// </para>
/// </remarks>
public interface IStudySearchCatalog
{
    /// <summary>
    /// Finds studies matching a query.
    /// </summary>
    /// <param name="query">
    /// What to look for. Callers validate it with
    /// <see cref="StudySearchValidation.Validate"/> first; an implementation may
    /// assume the page bounds are already within
    /// <see cref="StudySearchLimits"/>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>
    /// One page of matches. A query that matches nothing returns an empty page
    /// rather than <see langword="null"/> - finding nothing is an answer, not a
    /// failure.
    /// </returns>
    public Task<StudySearchResult> SearchStudiesAsync(
        StudySearchQuery query,
        CancellationToken cancellationToken = default);
}
