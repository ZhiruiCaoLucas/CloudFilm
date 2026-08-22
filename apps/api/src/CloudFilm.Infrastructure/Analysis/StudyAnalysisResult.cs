namespace CloudFilm.Infrastructure.Analysis;

/// <summary>
/// The current automated characterisation of one study.
/// </summary>
/// <remarks>
/// <para>
/// One row per study, replaced when the study is analysed again. That is the
/// opposite of <c>AccessGrant</c> and <c>ShareLink</c>, which accumulate, and it
/// is deliberate: those tables record who was given access to a patient's
/// imaging and are read afterwards as evidence, whereas this row is a derived
/// artefact that any later run supersedes. Nothing is lost by replacing it,
/// because the audit trail already writes one <c>ANALYSE_STUDY</c> entry per run
/// and that record is append-only.
/// </para>
/// <para>
/// Carries a surrogate key even though <see cref="StudyInstanceUid"/> is unique.
/// The uniqueness is expressed as a unique index instead, so the identity of the
/// row and the rule that there may only be one of them stay separable - a later
/// sprint that admits several models per study widens the index without
/// rewriting every reference to the row.
/// </para>
/// <para>
/// Holds no patient identity. The analyser is told which study it is looking at
/// and nothing about whose it is, so there is nothing here to store: no name, no
/// record number, no date of birth. A reader who needs those already holds the
/// study and can ask the catalogue.
/// </para>
/// <para>
/// Deliberately does not implement <c>IAuditableEntity</c>. That interface is
/// stamped by a <c>SaveChanges</c> interceptor, and this row is written by a
/// single <c>INSERT … ON CONFLICT</c> statement that never passes through the
/// change tracker, so the interceptor would not fire and the stamps would be
/// silently absent. They are set explicitly by the store instead, from the same
/// <c>IClock</c> the interceptor would have used.
/// </para>
/// </remarks>
public sealed class StudyAnalysisResult
{
    /// <summary>Gets or sets the record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the DICOM Study Instance UID that was analysed.</summary>
    /// <remarks>
    /// Stored exactly as the endpoint accepted it, after trimming, and unique
    /// across the table. No case folding: a DICOM UID is digits and dots, so it
    /// has no case to fold. Not a foreign key, because CloudFilm holds no study
    /// table - the archive is the system of record and study metadata is
    /// resolved live through the Retrieve Service.
    /// </remarks>
    public string StudyInstanceUid { get; set; } = string.Empty;

    /// <summary>Gets or sets how the run that produced this row ended.</summary>
    /// <remarks>
    /// One of the <c>AnalysisStatuses</c> values, stored verbatim as a string
    /// for the reason <c>AuditAction</c> is: the value is read back by people
    /// running SQL during an incident, and an integer would make them consult
    /// the source to find out what happened.
    /// </remarks>
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets the model that produced the findings.</summary>
    /// <remarks>
    /// A real column rather than a field inside <see cref="Findings"/>. A result
    /// whose model and version are unknown cannot be reproduced, compared with a
    /// later run, or withdrawn when a model turns out to be wrong - so the two
    /// facts an investigation always needs are readable without parsing JSON.
    /// </remarks>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Gets or sets the exact version of that model.</summary>
    public string ModelVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets when the analysis finished, in UTC.</summary>
    /// <remarks>
    /// The analyser's own instant, not the moment this row was written. It is
    /// what the reader is shown, so it must survive being stored;
    /// <see cref="CreatedAtUtc"/> answers the different question of when
    /// CloudFilm learned of it.
    /// </remarks>
    public DateTimeOffset CompletedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the findings, as the JSON array the transport contract uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>jsonb</c>, and the only such column in CloudFilm. Everything else here
    /// is a fixed, bounded fact about the run; the findings are a model's own
    /// vocabulary, they differ in shape from one model to the next, and nothing
    /// in the application ever queries inside them - the API returns them
    /// verbatim and the portal renders them. A child table would add a join and
    /// a migration to every future model for a list that is read whole or not at
    /// all, and <c>AnalysisLimits.MaxFindings</c> already bounds its size.
    /// </para>
    /// <para>
    /// Serialised from <c>IReadOnlyList&lt;AnalysisFinding&gt;</c> with the same
    /// camelCase naming the HTTP API uses, so a row read in <c>psql</c> looks
    /// like the response a client received.
    /// </para>
    /// </remarks>
    public string Findings { get; set; } = "[]";

    /// <summary>Gets or sets the account that asked for the analysis.</summary>
    /// <remarks>
    /// <para>
    /// Deliberately not a foreign key, for the reason the audit trail's actor
    /// and <c>ShareLink.CreatedByUserId</c> are not: who sent a patient's
    /// imaging to another process must outlive the account that did it. A
    /// foreign key would force a choice between blocking the deletion and
    /// erasing the record, and both are wrong.
    /// </para>
    /// <para>
    /// Nullable, like <c>AuditLog.ActorUserId</c> and for the same reason: a
    /// validated token that carries no usable subject is a state the endpoint
    /// must still be able to record rather than one it should invent an account
    /// for. The audit trail remains the authoritative answer to who did this;
    /// this column is here so the stored row can be read on its own.
    /// </para>
    /// </remarks>
    public Guid? RequestedByUserId { get; set; }

    /// <summary>Gets or sets when this row was first written, in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when this row was last replaced, in UTC.
    /// </summary>
    /// <remarks>
    /// Null until the study is analysed a second time, so the two timestamps
    /// distinguish a result that has stood since it was produced from one that
    /// has been superseded.
    /// </remarks>
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
