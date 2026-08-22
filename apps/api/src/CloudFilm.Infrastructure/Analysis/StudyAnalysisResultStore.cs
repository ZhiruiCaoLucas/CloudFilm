using System.Text.Json;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Analysis;
using CloudFilm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudFilm.Infrastructure.Analysis;

/// <summary>
/// Stores the current analysis of each study in PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// Shares the request's <see cref="CloudFilmDbContext"/>, like the audit writer,
/// so a stored result and the record of the access that produced it travel on
/// one connection and can be enclosed in one transaction by the caller. Nothing
/// here opens a transaction of its own.
/// </para>
/// <para>
/// The write is a single upsert rather than "look, then insert or update". Two
/// clinicians analysing the same study at the same moment would both find
/// nothing and both insert, and the loser would get a unique violation that has
/// to be caught and interpreted. <c>ON CONFLICT DO UPDATE</c> lets PostgreSQL
/// settle it: one statement, one row afterwards, and no window between a read
/// and a write for the other request to slip into.
/// </para>
/// </remarks>
internal sealed class StudyAnalysisResultStore(CloudFilmDbContext context, IClock clock)
    : IStudyAnalysisResultStore
{
    /// <summary>
    /// How findings are written to and read from the <c>jsonb</c> column.
    /// </summary>
    /// <remarks>
    /// Web defaults, the same options <c>StudyAnalyser</c> uses to read the
    /// analysis service's response. That gives camelCase property names, so a
    /// row read in <c>psql</c> during an incident looks like the JSON a client
    /// received rather than a second spelling of it.
    /// </remarks>
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly CloudFilmDbContext _context = context;
    private readonly IClock _clock = clock;

    /// <inheritdoc />
    public async Task<StudyAnalysis?> FindAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        // AsNoTracking because nothing here is going to be written back; the
        // upsert below does not go through the change tracker.
        StudyAnalysisResult? stored = await _context.StudyAnalysisResults
            .AsNoTracking()
            .SingleOrDefaultAsync(
                result => result.StudyInstanceUid == studyInstanceUid,
                cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return null;
        }

        // A row whose findings will not parse is a bug in whatever wrote it, not
        // a state to paper over: an empty list would be returned to a clinician
        // as "the model named nothing", which is a different and untrue answer.
        IReadOnlyList<AnalysisFinding> findings =
            JsonSerializer.Deserialize<List<AnalysisFinding>>(stored.Findings, _json)
            ?? throw new JsonException(
                "The stored findings for a study analysis could not be read.");

        return new StudyAnalysis(
            stored.StudyInstanceUid,
            stored.ModelName,
            stored.ModelVersion,
            stored.CompletedAtUtc,
            findings);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Raw SQL because EF Core has no upsert. The values are parameters, not
    /// interpolated text: the interpolated-string overload sends every hole to
    /// the server as a bound parameter. The schema, table and column names are
    /// the only literal parts, and they must be kept in step with
    /// <see cref="CloudFilmDbContext.Schema"/> and
    /// <c>StudyAnalysisResultConfiguration</c>.
    /// </para>
    /// <para>
    /// <c>ON CONFLICT ("StudyInstanceUid")</c> relies on the unique index that
    /// configuration declares. Without it PostgreSQL has no conflict target and
    /// this statement fails outright, which is the failure mode to want: the
    /// rule that a study has one current result cannot be dropped from the
    /// schema without the writes noticing.
    /// </para>
    /// <para>
    /// <c>CreatedAtUtc</c> is never overwritten. It records when CloudFilm first
    /// learned of an analysis for this study, and a rerun sets
    /// <c>UpdatedAtUtc</c> instead - so the two columns distinguish a result that
    /// has stood since it was produced from one that has superseded another.
    /// </para>
    /// </remarks>
    public async Task SaveAsync(
        StudyAnalysis analysis,
        Guid? requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        string findings = JsonSerializer.Serialize(analysis.Findings, _json);
        DateTimeOffset now = _clock.UtcNow;
        Guid id = Guid.NewGuid();

        await _context.Database
            .ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO cloudfilm."StudyAnalysisResults" (
                    "Id", "StudyInstanceUid", "Status", "ModelName", "ModelVersion",
                    "CompletedAtUtc", "Findings", "RequestedByUserId",
                    "CreatedAtUtc", "UpdatedAtUtc")
                VALUES (
                    {id}, {analysis.StudyInstanceUid}, {AnalysisStatuses.Succeeded},
                    {analysis.ModelName}, {analysis.ModelVersion},
                    {analysis.CompletedAtUtc}, CAST({findings} AS jsonb),
                    {requestedByUserId}, {now}, NULL)
                ON CONFLICT ("StudyInstanceUid") DO UPDATE SET
                    "Status" = EXCLUDED."Status",
                    "ModelName" = EXCLUDED."ModelName",
                    "ModelVersion" = EXCLUDED."ModelVersion",
                    "CompletedAtUtc" = EXCLUDED."CompletedAtUtc",
                    "Findings" = EXCLUDED."Findings",
                    "RequestedByUserId" = EXCLUDED."RequestedByUserId",
                    "UpdatedAtUtc" = {now}
                """,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
