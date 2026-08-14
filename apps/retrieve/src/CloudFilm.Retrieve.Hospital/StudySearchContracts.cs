using System.Globalization;

namespace CloudFilm.Retrieve.Hospital;

/// <summary>
/// What to look for, as one hospital source is asked it.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the API's contract of the same name and is deliberately a separate
/// type, exactly as <see cref="PatientSummary"/> and <see cref="StudySummary"/>
/// already are on both sides of this boundary. The Retrieve Service is a
/// separate process reached over HTTP; sharing an assembly across that boundary
/// would couple two deployables that are meant to version independently.
/// </para>
/// <para>
/// Still expressed as a clinical question rather than a DICOMweb one. An adapter
/// for a hospital that speaks C-FIND, or a vendor's HTTP API, must be able to
/// answer this without a wildcard or a QIDO date range anywhere in sight.
/// Translating <see cref="PatientName"/> into <c>value*</c>, the dates into
/// DICOM range syntax and <see cref="Page"/>/<see cref="PageSize"/> into
/// <c>offset</c>/<c>limit</c> is each adapter's own business.
/// </para>
/// <para>
/// The values arrive as the user typed them and are already validated for page
/// bounds and date sanity by the API. An adapter must still escape them for
/// whatever query language it speaks: a name is text a person entered, never an
/// expression.
/// </para>
/// </remarks>
/// <param name="PatientName">Part of the patient's name, or <see langword="null"/>.</param>
/// <param name="AccessionNumber">The accession number or its leading characters, or <see langword="null"/>.</param>
/// <param name="StudyDateFrom">Earliest study date, inclusive, or <see langword="null"/>.</param>
/// <param name="StudyDateTo">Latest study date, inclusive, or <see langword="null"/>.</param>
/// <param name="Page">The zero-based page wanted.</param>
/// <param name="PageSize">How many results one page holds.</param>
public sealed record StudySearchQuery(
    string? PatientName,
    string? AccessionNumber,
    DateOnly? StudyDateFrom,
    DateOnly? StudyDateTo,
    int Page,
    int PageSize);

/// <summary>
/// One search hit: a study, and the patient it belongs to.
/// </summary>
/// <remarks>
/// <para>
/// A search crosses patients, which is what makes it different from every other
/// study list in CloudFilm. On a patient page the patient is the page, so a
/// study needs no owner; in a result set, two rows may belong to two people, and
/// a row that cannot say whose study it is cannot be acted on safely. It is also
/// what makes the result navigable to both the patient and the workspace.
/// </para>
/// <para>
/// Composed of the existing contracts rather than a flattened copy of their
/// fields. A search result is a pairing of two things the system already
/// describes, and re-describing either one would fork a contract the portal
/// already understands.
/// </para>
/// <para>
/// Both halves come from the same archive response. QIDO-RS returns the patient
/// attributes on every study result, so pairing them costs no extra request.
/// </para>
/// </remarks>
/// <param name="Patient">The patient the study belongs to.</param>
/// <param name="Study">The matching study.</param>
public sealed record StudySearchItem(PatientSummary Patient, StudySummary Study);

/// <summary>
/// One page of matching studies from a hospital source.
/// </summary>
/// <remarks>
/// No total, because DICOMweb reports none: the deployment's QIDO-RS responses
/// carry no <c>X-Total-Count</c>, no <c>Content-Range</c> and no <c>Link</c>
/// header. An adapter establishes <see cref="HasMore"/> by asking for one result
/// beyond the page and reporting whether it arrived, and must not return that
/// extra result in <see cref="Items"/>.
/// </remarks>
/// <param name="Items">The hits on this page; never longer than <paramref name="PageSize"/>.</param>
/// <param name="HasMore"><see langword="true"/> when at least one further result exists.</param>
/// <param name="Page">The zero-based page this result represents.</param>
/// <param name="PageSize">The page size the search was executed with.</param>
public sealed record StudySearchResult(
    IReadOnlyList<StudySearchItem> Items,
    bool HasMore,
    int Page,
    int PageSize);

/// <summary>
/// The bounds a study search is executed within.
/// </summary>
/// <remarks>
/// Mirrors the API's limits of the same name. Restated here rather than trusted
/// from the caller: the API validates, but this service is a separate process
/// with its own HTTP surface, and a page ceiling that only one side enforces is
/// a ceiling that can be walked around.
/// </remarks>
public static class StudySearchLimits
{
    /// <summary>The smallest page an adapter will execute.</summary>
    public const int MinPageSize = 1;

    /// <summary>
    /// The page size used when a caller names none.
    /// </summary>
    /// <remarks>
    /// A screenful, not the ceiling. A caller that forgets to say how much it
    /// wants should not be handed the largest page the system can produce.
    /// </remarks>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// The largest page an adapter will execute.
    /// </summary>
    /// <remarks>
    /// A ceiling on how much patient data one request can disclose, not a
    /// performance tuning knob.
    /// </remarks>
    public const int MaxPageSize = 100;
}

/// <summary>
/// Checks a search query before it becomes an archive query.
/// </summary>
/// <remarks>
/// Mirrors the API's validator of the same name. Restated rather than shared for
/// the same reason the contracts are: this is a separate process reached over
/// HTTP, and a rule enforced only by the caller is a rule this service does not
/// have. Pure and free of I/O, so the endpoint can be tested without a host.
/// </remarks>
public static class StudySearchValidation
{
    /// <summary>
    /// Reports everything wrong with a query.
    /// </summary>
    /// <param name="query">The query to check.</param>
    /// <returns>One message per problem; empty when the query is usable.</returns>
    public static IReadOnlyList<string> Validate(StudySearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<string> problems = [];

        if (query.Page < 0)
        {
            problems.Add("Page must be zero or greater.");
        }

        if (query.PageSize < StudySearchLimits.MinPageSize
            || query.PageSize > StudySearchLimits.MaxPageSize)
        {
            problems.Add("PageSize must be between 1 and 100.");
        }

        if (query.StudyDateFrom is { } from && query.StudyDateTo is { } to && from > to)
        {
            problems.Add("StudyDateFrom must not be later than StudyDateTo.");
        }

        return problems;
    }

    /// <summary>
    /// Reads a study date supplied as text.
    /// </summary>
    /// <param name="value">The date as <c>YYYY-MM-DD</c> or <c>YYYYMMDD</c>, or nothing.</param>
    /// <param name="date">The parsed date, or <see langword="null"/> when none was supplied.</param>
    /// <returns><see langword="false"/> only when text was supplied and could not be read.</returns>
    /// <remarks>
    /// Absent is not invalid: a search with no lower bound is a legitimate
    /// search. Only the two unambiguous formats are accepted, so no request can
    /// turn into a QIDO date the caller did not mean.
    /// </remarks>
    public static bool TryParseStudyDate(string? value, out DateOnly? date)
    {
        date = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        string trimmed = value.Trim();

        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly iso))
        {
            date = iso;

            return true;
        }

        if (DateOnly.TryParseExact(trimmed, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly dicom))
        {
            date = dicom;

            return true;
        }

        return false;
    }
}
