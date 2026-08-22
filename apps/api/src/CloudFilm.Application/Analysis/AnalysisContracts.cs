namespace CloudFilm.Application.Analysis;

/// <summary>
/// What an analyser says about one study.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately free of patient identity. The analyser is told which study it is
/// looking at and nothing about whose it is, so there is nothing here to echo
/// back: no name, no record number, no date of birth. A reader who needs those
/// already holds the study and can ask the catalogue.
/// </para>
/// <para>
/// The model is named and versioned because a result without them cannot be
/// reproduced, compared against a later run, or withdrawn when a model is found
/// to be wrong. "The system said so" is not a finding anybody can act on.
/// </para>
/// <para>
/// This is not a diagnosis and this record does not model one. It carries what a
/// model reported, with the model's own confidence, for a clinician to read.
/// </para>
/// </remarks>
/// <param name="StudyInstanceUid">The study the findings are about.</param>
/// <param name="ModelName">The model that produced them.</param>
/// <param name="ModelVersion">
/// The exact version of that model. Two results from the same
/// <paramref name="ModelName"/> and different versions are not comparable.
/// </param>
/// <param name="CompletedAtUtc">When the analysis finished.</param>
/// <param name="Findings">
/// What the model reported, in the order it reported them. Empty is a real and
/// meaningful answer - the model ran and found nothing it names - and is not the
/// same as no analysis having happened. Note that two analyses carrying equal
/// findings in separate lists are not equal to one another: a record compares a
/// list by reference, so compare findings directly rather than whole results.
/// </param>
public sealed record StudyAnalysis(
    string StudyInstanceUid,
    string ModelName,
    string ModelVersion,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<AnalysisFinding> Findings);

/// <summary>
/// One thing a model reported.
/// </summary>
/// <remarks>
/// A label and a number, and nothing else yet. Regions, outlines and
/// measurements are deliberately absent: a contract that promises geometry
/// nothing produces is worse than one that does not mention it, and adding a
/// field later is easier than removing one a client has started drawing.
/// </remarks>
/// <param name="Label">
/// What the model calls this finding. The model's own vocabulary, passed through
/// rather than mapped: CloudFilm does not own a clinical code system and
/// inventing a mapping here would put a translation nobody reviewed between a
/// model and a clinician.
/// </param>
/// <param name="Score">
/// The model's confidence, from 0 to 1 inclusive. Whether that number means
/// anything - and what threshold is worth acting on - is a property of the model,
/// not of this record.
/// </param>
public sealed record AnalysisFinding(string Label, double Score);

/// <summary>
/// Bounds the analysis contract enforces on what it will carry.
/// </summary>
/// <remarks>
/// Stated here so that the endpoint, the adapter and the analyser agree before
/// any of them exist. Nothing in this milestone validates against them; they are
/// the numbers whatever validates later must use, rather than three sets of
/// magic numbers discovered to disagree.
/// </remarks>
public static class AnalysisLimits
{
    /// <summary>
    /// The longest Study Instance UID an analysis will carry.
    /// </summary>
    /// <remarks>
    /// The same 128 every other feature that stores one uses - read state,
    /// grants, share links and the audit trail. DICOM caps a UID at 64
    /// characters; 128 leaves room without admitting an unbounded string.
    /// </remarks>
    public const int MaxStudyInstanceUidLength = 128;

    /// <summary>The longest model name an analysis will carry.</summary>
    public const int MaxModelNameLength = 128;

    /// <summary>The longest model version an analysis will carry.</summary>
    public const int MaxModelVersionLength = 64;

    /// <summary>The longest label a single finding will carry.</summary>
    public const int MaxLabelLength = 128;

    /// <summary>
    /// The most findings one analysis will carry.
    /// </summary>
    /// <remarks>
    /// A ceiling rather than an expectation. It exists so that a misbehaving
    /// model cannot turn one study into a response large enough to be a problem
    /// for whoever renders it.
    /// </remarks>
    public const int MaxFindings = 100;

    /// <summary>The lowest confidence a finding may report.</summary>
    public const double MinimumScore = 0d;

    /// <summary>The highest confidence a finding may report.</summary>
    public const double MaximumScore = 1d;

    /// <summary>
    /// The longest analysis status a stored result will carry.
    /// </summary>
    /// <remarks>
    /// The same 32 an access grant's permission uses. These are short words
    /// chosen by CloudFilm, not values another system supplies.
    /// </remarks>
    public const int MaxStatusLength = 32;
}

/// <summary>
/// How an analysis run ended.
/// </summary>
/// <remarks>
/// <para>
/// Constants rather than an enum, for the reason <c>AuditAction</c> and
/// <c>GrantPermissions</c> are: the value is written to the database verbatim
/// and read back by people running SQL during an incident, and an integer would
/// send them to the source to find out what happened.
/// </para>
/// <para>
/// Only <see cref="Succeeded"/> is ever stored today. A run that fails leaves no
/// row, so that a failure cannot replace a good result a clinician is relying
/// on; the failure itself is reported to the caller and recorded in the audit
/// trail. The column exists because a stored result must say what it is, and
/// because an installation that later queues analyses will need to record a run
/// that has not finished without changing the schema to do it.
/// </para>
/// </remarks>
public static class AnalysisStatuses
{
    /// <summary>The analyser ran and reported findings.</summary>
    /// <remarks>
    /// Empty findings are a success. The model ran and named nothing, which is a
    /// real answer and not the same as no analysis having happened.
    /// </remarks>
    public const string Succeeded = "SUCCEEDED";
}
