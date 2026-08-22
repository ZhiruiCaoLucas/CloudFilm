namespace CloudFilm.Application.Auth;

/// <summary>
/// The application roles CloudFilm recognises.
/// </summary>
/// <remarks>
/// <para>
/// Two, and adding a third is a product decision rather than a code change
/// somebody makes in passing. Both currently reach the same functionality: the
/// milestone that introduced them established who a caller is, not yet what one
/// kind of caller may do that another may not.
/// </para>
/// <para>
/// Constants rather than an enum, because these values are written to
/// <c>AspNetRoles</c> verbatim and read back by people running SQL during an
/// incident. An enum would store an integer whose meaning lives only in this
/// assembly - the same reasoning that made <c>AuditAction</c> a set of strings.
/// </para>
/// <para>
/// Not to be confused with <c>CloudFilm.Infrastructure.Identity.ApplicationRole</c>,
/// which is the ASP.NET Identity entity - the row. This type holds the names
/// those rows are created with, lives in the Application layer, and is the one
/// place any of them is spelled out. Nothing else in CloudFilm may write a role
/// name as a literal string.
/// </para>
/// <para>
/// There is deliberately no <c>Patient</c> role. A patient signing in to see
/// their own imaging needs an account bound to a medical record number and
/// per-record filtering on every read - a different feature with its own data
/// model, not a third entry in this list. Adding the name here without that
/// machinery would produce an account that authenticates and then sees
/// everybody.
/// </para>
/// </remarks>
public static class RoleNames
{
    /// <summary>
    /// Operates the deployment.
    /// </summary>
    /// <remarks>
    /// The role the seeded account is given, because a system whose only account
    /// holds no role is a system nobody can sign in to once roles are enforced.
    /// </remarks>
    public const string Administrator = "Administrator";

    /// <summary>
    /// Reads patients and imaging studies in the course of clinical work.
    /// </summary>
    public const string Clinician = "Clinician";

    /// <summary>
    /// Every role CloudFilm recognises.
    /// </summary>
    /// <remarks>
    /// The seeder creates exactly these, and authorisation admits exactly these.
    /// A role that exists in the database but not in this list is not a
    /// CloudFilm role: it grants nothing, and it is not removed either, because
    /// deleting rows an operator created by hand is not this application's
    /// business.
    /// </remarks>
    public static IReadOnlyList<string> All { get; } = [Administrator, Clinician];
}

/// <summary>
/// The authorisation policies CloudFilm enforces.
/// </summary>
/// <remarks>
/// <para>
/// One policy, because there is currently one distinction worth drawing: an
/// account that has been given a role, and an account that has not. Both roles
/// reach the same functionality today, so a policy per role would encode a
/// difference that does not exist and would have to be unpicked the moment one
/// does.
/// </para>
/// <para>
/// It sits beside the authentication fallback policy rather than replacing it.
/// The fallback establishes that a caller is signed in and answers 401 when they
/// are not; this establishes that a signed-in caller is one of ours and answers
/// 403 when they are not. Keeping the two separate is what keeps those status
/// codes meaning what they say.
/// </para>
/// </remarks>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Requires the caller to hold at least one role from <see cref="RoleNames.All"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An authenticated account with no application role satisfies
    /// authentication and fails this: it receives 403, not 401 and not a silent
    /// promotion. That is the explicit answer to the accounts that already exist
    /// - the seeded administrator is given a role by the seeder, and anything
    /// else stays locked out until somebody deliberately grants one.
    /// </para>
    /// <para>
    /// There is no "no role means administrator" fallback and there will not be
    /// one. A default that grants power to accounts nobody configured is how a
    /// forgotten account becomes an incident.
    /// </para>
    /// </remarks>
    public const string RequiresApplicationRole = "RequiresApplicationRole";

    /// <summary>
    /// Requires the caller to hold <see cref="RoleNames.Administrator"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first distinction CloudFilm draws between its two roles, and it is
    /// deliberately not about reading patient data - both roles still read
    /// exactly the same studies. It is about deciding who else may: opening one
    /// patient's imaging to another account is an administrative act, and a
    /// clinician doing clinical work has no reason to perform one.
    /// </para>
    /// <para>
    /// A Clinician fails this and receives 403. That is a different answer from
    /// the one <see cref="RequiresApplicationRole"/> gives an account with no
    /// role at all, and only in the message: both are refusals of an
    /// authenticated caller, which is what 403 means.
    /// </para>
    /// </remarks>
    public const string RequiresAdministrator = "RequiresAdministrator";
}
