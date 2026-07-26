using Microsoft.AspNetCore.Identity;

namespace CloudFilm.Infrastructure.Identity;

/// <summary>
/// A CloudFilm authorisation role, for example <c>Patient</c>, <c>Physician</c> or <c>Administrator</c>.
/// </summary>
public sealed class ApplicationRole : IdentityRole<Guid>
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ApplicationRole"/> class.
    /// </summary>
    public ApplicationRole()
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="ApplicationRole"/> class with the given name.
    /// </summary>
    /// <param name="roleName">The role name.</param>
    public ApplicationRole(string roleName)
        : base(roleName)
    {
    }
}
