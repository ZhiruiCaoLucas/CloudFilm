using CloudFilm.Application.Analysis;

namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Reads and replaces the stored analysis of one study.
/// </summary>
/// <remarks>
/// <para>
/// Purpose-specific, like <see cref="IStudyReadStateStore"/> and for the same
/// reason. Every method names exactly one study and there is no overload that
/// omits it; nothing here accepts a filter, a page size or a list of studies, so
/// this cannot grow into a general query surface over a table that says which of
/// a hospital's studies have been sent to a processor.
/// </para>
/// <para>
/// The store holds no opinion about who may see a study. Callers authorise
/// first - a study a caller may not see must be answered before this is asked,
/// or the stored result becomes an oracle for which studies exist.
/// </para>
/// <para>
/// Implementations must not open their own transaction and must not assume they
/// are the only writer in the unit of work. The caller composes the write and
/// its audit record into one transaction; see <see cref="IUnitOfWork"/>.
/// </para>
/// </remarks>
public interface IStudyAnalysisResultStore
{
    /// <summary>
    /// Returns the stored analysis of one study.
    /// </summary>
    /// <param name="studyInstanceUid">The study, already trimmed and length checked.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The stored analysis, or <see langword="null"/> when this study has never
    /// been analysed.
    /// </returns>
    /// <remarks>
    /// A study nobody has analysed answers <see langword="null"/> rather than
    /// failing. Whether the study exists at all is the archive's question, and
    /// answering it here would let a stored result be used to discover which
    /// studies are held.
    /// </remarks>
    public Task<StudyAnalysis?> FindAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the analysis of one study, replacing any earlier one.
    /// </summary>
    /// <param name="analysis">What the analyser reported.</param>
    /// <param name="requestedByUserId">The account that asked for the analysis.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the result has been written.</returns>
    /// <remarks>
    /// The study is <see cref="StudyAnalysis.StudyInstanceUid"/> rather than a
    /// separate argument, so a caller cannot file one study's findings under
    /// another's identifier. Idempotent: storing the same analysis twice leaves
    /// one row.
    /// </remarks>
    public Task SaveAsync(
        StudyAnalysis analysis,
        Guid? requestedByUserId,
        CancellationToken cancellationToken = default);
}
