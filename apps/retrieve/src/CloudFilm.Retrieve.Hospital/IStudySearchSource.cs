namespace CloudFilm.Retrieve.Hospital;

/// <summary>
/// A hospital imaging source that can be searched.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="IHospitalAdapter"/> on purpose. Not every archive
/// can be searched by name - some hospitals expose a lookup by identifier and
/// nothing more - and adding a method to <see cref="IHospitalAdapter"/> would
/// force every adapter, present and future, to implement a query it cannot
/// answer, or to throw.
/// </para>
/// <para>
/// An adapter that can search implements both interfaces; the Retrieve endpoints
/// test for this one before offering search. That keeps "this source supports
/// search" a fact the type system carries rather than a configuration flag
/// someone has to remember to set.
/// </para>
/// </remarks>
public interface IStudySearchSource
{
    /// <summary>
    /// Finds studies matching a query.
    /// </summary>
    /// <param name="query">
    /// What to look for, already validated by the caller. Values are as the user
    /// typed them and must be escaped by the implementation before they reach a
    /// query language.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>
    /// One page of matches, and whether more exist beyond it. Matching nothing
    /// returns an empty page rather than <see langword="null"/>.
    /// </returns>
    public Task<StudySearchResult> SearchStudiesAsync(
        StudySearchQuery query,
        CancellationToken cancellationToken = default);
}
