using CloudFilm.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace CloudFilm.Infrastructure.Identity;

/// <summary>
/// Answers whether an account exists, from ASP.NET Identity.
/// </summary>
/// <remarks>
/// Reads one row and returns one boolean. The account itself never leaves this
/// class: <see cref="ApplicationUser"/> carries a password hash, a security
/// stamp and lockout state, and nothing above this layer has any business
/// holding them.
/// </remarks>
internal sealed class IdentityUserDirectory(UserManager<ApplicationUser> users) : IUserDirectory
{
    private readonly UserManager<ApplicationUser> _users = users;

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ApplicationUser? user = await _users
            .FindByIdAsync(userId.ToString())
            .ConfigureAwait(false);

        return user is not null;
    }
}
