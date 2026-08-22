using CloudFilm.Application.Analysis;
using CloudFilm.Application.Imaging;

namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Analyses one study that has already been retrieved.
/// </summary>
/// <remarks>
/// <para>
/// The port takes the study's bytes, not its address. There is no URL to fetch,
/// no identifier to resolve and no catalogue to query, so an implementation
/// cannot reach a study nobody authorised - the only imaging it will ever see is
/// imaging a caller has already been permitted to hold.
/// </para>
/// <para>
/// That is the whole point of the shape. The analyser is expected to be a
/// separate process, and one that authenticates nobody; if it could name a study
/// it would need its own answer to "may this caller see it", and CloudFilm would
/// have two. Authorisation stays where <see cref="IStudyAccessPolicy"/> put it,
/// runs before <see cref="IStudyDownloadSource"/> is asked for anything, and this
/// port never revisits the question.
/// </para>
/// <para>
/// Implementations receive DICOM. That means they receive the patient
/// identifiers DICOM carries in its own tags, whatever this interface's
/// signature suggests - an analyser is inside the PHI boundary and must be
/// operated as such: nothing written to disk, no tag values in logs.
/// </para>
/// </remarks>
public interface IStudyAnalyser
{
    /// <summary>
    /// Analyses one study.
    /// </summary>
    /// <param name="studyInstanceUid">
    /// The study the bytes belong to. A correlation handle, so the result can say
    /// what it is about and the audit trail can say what was analysed - never an
    /// instruction to fetch anything.
    /// </param>
    /// <param name="study">
    /// The open study, as <see cref="IStudyDownloadSource"/> yielded it. The
    /// caller owns it and disposes it; an implementation reads the stream and
    /// must not dispose it, because the caller may still need to release what the
    /// stream is holding open.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the analysis.</param>
    /// <returns>
    /// What the model reported, or <see langword="null"/> when this analyser has
    /// nothing to say about this study - an unsupported modality, say. Null is an
    /// answer, not a failure: it means the study was seen and not analysed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Failures throw rather than resolving to null. An analyser that is
    /// unreachable, times out, or breaks on well-formed input is a fault the
    /// caller must be able to tell apart from "nothing to report", because one is
    /// worth retrying and the other is not.
    /// </para>
    /// <para>
    /// The stream arrives while the study is still on the wire. An implementation
    /// should stream it onward rather than read it into memory: a study is large
    /// enough that buffering it is the difference between a service that runs on
    /// a hospital's hardware and one that does not.
    /// </para>
    /// </remarks>
    public Task<StudyAnalysis?> AnalyseAsync(
        string studyInstanceUid,
        StudyDownload study,
        CancellationToken cancellationToken = default);
}
