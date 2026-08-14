using System.Globalization;

namespace CloudFilm.Application.Imaging;

/// <summary>
/// What a clinician is looking for.
/// </summary>
/// <remarks>
/// <para>
/// States the clinical question, never the archive's query language. There is no
/// wildcard here, no DICOM date-range string, no <c>fuzzymatching</c> and no
/// <c>offset</c>: those are how one particular archive answers the question, and
/// putting them in the contract would tie every caller - and every future
/// hospital adapter - to DICOMweb.
/// </para>
/// <para>
/// The values are carried exactly as the user typed them. Escaping is the
/// translator's job and it must not be skipped: a search term reaches a query
/// language, so <c>*</c>, <c>?</c>, <c>^</c> and <c>\</c> in a name are
/// characters a user typed, not operators they asked for.
/// </para>
/// <para>
/// Fields supplied together are combined with AND, which is what QIDO-RS does
/// with multiple parameters and what a clinician expects from a search form.
/// </para>
/// </remarks>
/// <param name="PatientName">
/// Part of the patient's name, or <see langword="null"/> to not filter by name.
/// Translated later to a DICOM prefix match (<c>value*</c>).
/// </param>
/// <param name="AccessionNumber">
/// The accession number or its leading characters, or <see langword="null"/>.
/// Translated later to a DICOM prefix match (<c>value*</c>).
/// </param>
/// <param name="StudyDateFrom">
/// The earliest study date to include, inclusive, or <see langword="null"/> for
/// no lower bound.
/// </param>
/// <param name="StudyDateTo">
/// The latest study date to include, inclusive, or <see langword="null"/> for no
/// upper bound. Together the two become DICOM date-range syntax, which supports
/// open ends in both directions.
/// </param>
/// <param name="Page">The zero-based page of results wanted.</param>
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
/// One page of matching studies.
/// </summary>
/// <remarks>
/// <para>
/// Carries no total. The archive reports none - a QIDO-RS response has no
/// <c>X-Total-Count</c>, no <c>Content-Range</c> and no <c>Link</c> header, which
/// was measured against the deployment rather than assumed - so a total could
/// only be produced by fetching every match and counting, which is exactly what
/// paging exists to avoid.
/// </para>
/// <para>
/// <see cref="HasMore"/> answers the only question a caller can act on: is there
/// another page. The implementation asks the archive for one result more than
/// the page holds; if that extra result arrives, there is more, and it is
/// dropped rather than returned. So a truncated result is never silently
/// truncated - the caller is told, and can narrow the search or turn the page.
/// </para>
/// </remarks>
/// <param name="Items">
/// The hits on this page. Never longer than <see cref="PageSize"/>.
/// </param>
/// <param name="HasMore">
/// <see langword="true"/> when at least one further result exists beyond this
/// page.
/// </param>
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
public static class StudySearchLimits
{
    /// <summary>The smallest page a caller may ask for.</summary>
    public const int MinPageSize = 1;

    /// <summary>
    /// The page size used when a caller names none.
    /// </summary>
    /// <remarks>
    /// A screenful, not the ceiling. A client that forgets to say how much it
    /// wants should not be handed the largest page of patient data the system
    /// can produce.
    /// </remarks>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// The largest page a caller may ask for.
    /// </summary>
    /// <remarks>
    /// A ceiling on how much patient data one request can disclose, not a
    /// performance tuning knob. It is enforced here rather than trusted from the
    /// client.
    /// </remarks>
    public const int MaxPageSize = 100;
}

/// <summary>
/// Checks a search query before it becomes an archive query.
/// </summary>
/// <remarks>
/// <para>
/// Pure and free of I/O, so it is testable without a database, an archive or a
/// web host, and so the same rules apply wherever a query is built.
/// </para>
/// <para>
/// Returns problems rather than throwing. A malformed search is an ordinary
/// outcome of a person filling in a form, and the caller turns the list into one
/// 400 response.
/// </para>
/// </remarks>
public static class StudySearchValidation
{
    /// <summary>
    /// Reports everything wrong with a query.
    /// </summary>
    /// <param name="query">The query to check.</param>
    /// <returns>One message per problem; empty when the query is usable.</returns>
    /// <remarks>
    /// Every problem is reported, not just the first. A person who fixes one
    /// field and is then told about the next has been made to submit the form
    /// twice to learn what one response could have said.
    /// </remarks>
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
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"PageSize must be between {StudySearchLimits.MinPageSize} and {StudySearchLimits.MaxPageSize}."));
        }

        // An inverted range matches nothing, and DICOM range syntax cannot even
        // express it. Saying so is more useful than an empty result page that
        // looks like "this patient has no studies".
        if (query.StudyDateFrom is { } from && query.StudyDateTo is { } to && from > to)
        {
            problems.Add("StudyDateFrom must not be later than StudyDateTo.");
        }

        return problems;
    }

    /// <summary>
    /// Reads a study date supplied as text.
    /// </summary>
    /// <param name="value">
    /// The date as <c>YYYY-MM-DD</c> or <c>YYYYMMDD</c>, or nothing at all.
    /// </param>
    /// <param name="date">
    /// The parsed date, or <see langword="null"/> when none was supplied.
    /// </param>
    /// <returns>
    /// <see langword="false"/> only when text was supplied and could not be read
    /// as a date.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Absent is not invalid: a search with no lower bound is a legitimate
    /// search, so blank input parses successfully to <see langword="null"/>.
    /// </para>
    /// <para>
    /// Two formats, both exact and both culture-invariant. <c>YYYY-MM-DD</c> is
    /// what the portal already uses for every date it shows; <c>YYYYMMDD</c> is
    /// what DICOM writes. Nothing else is accepted - reading <c>03/04/2018</c>
    /// would mean guessing between March and April, and a study search that
    /// silently guesses the month is worse than one that refuses.
    /// </para>
    /// <para>
    /// Once parsed the format is gone: the query carries a
    /// <see cref="DateOnly"/>, so no malformed date can reach the archive
    /// whatever the caller sent.
    /// </para>
    /// </remarks>
    public static bool TryParseStudyDate(string? value, out DateOnly? date)
    {
        date = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        string trimmed = value.Trim();

        if (DateOnly.TryParseExact(
                trimmed,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly iso))
        {
            date = iso;

            return true;
        }

        if (DateOnly.TryParseExact(
                trimmed,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly dicom))
        {
            date = dicom;

            return true;
        }

        return false;
    }
}
