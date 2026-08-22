using System.Security.Cryptography;
using System.Text;
using CloudFilm.Application.Sharing;
using Microsoft.AspNetCore.WebUtilities;

namespace CloudFilm.Infrastructure.Sharing;

/// <summary>
/// Mints share tokens and reduces them to what may be stored.
/// </summary>
/// <remarks>
/// <para>
/// Two functions and no state, because a bearer credential needs exactly two
/// things done to it: it is created once from a cryptographic source, and it is
/// hashed every time it is written down. Wrapping that in an injected
/// abstraction would add a seam with one implementation and make the crypto
/// harder to read, not easier to change.
/// </para>
/// <para>
/// Public rather than internal only because the endpoint that hands the token to
/// its recipient lives in the API project. Nothing here holds a secret of its
/// own, so exposing it costs nothing.
/// </para>
/// <para>
/// The plaintext never comes back. <see cref="Hash"/> is one-way, so a token
/// lost by its recipient cannot be recovered from the database and has to be
/// re-issued - which is the correct behaviour for a credential, not a
/// shortcoming.
/// </para>
/// </remarks>
public static class ShareTokens
{
    /// <summary>
    /// The number of random bytes behind a token.
    /// </summary>
    /// <remarks>
    /// 32 bytes, so 256 bits. A share token is guessable only by exhausting that
    /// space, and it is the only thing standing between a URL and a patient's
    /// imaging - there is no account, no password and no second factor behind
    /// it. <see cref="Guid"/> would carry 122 bits and is not required to come
    /// from a cryptographic source at all.
    /// </remarks>
    public const int TokenByteLength = 32;

    /// <summary>
    /// Creates a token.
    /// </summary>
    /// <returns>The token, URL-safe, to be shown to its creator exactly once.</returns>
    /// <remarks>
    /// Base64Url rather than Base64 or hex: the value travels in a URL, and an
    /// encoding that needs escaping is one that will eventually be truncated,
    /// re-encoded or mangled by something in the path.
    /// </remarks>
    public static string Create() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenByteLength));

    /// <summary>
    /// Reduces a token to the digest stored against it.
    /// </summary>
    /// <param name="token">The token as it was issued.</param>
    /// <returns>
    /// The SHA-256 digest, <see cref="ShareLinkLimits.TokenHashLength"/> bytes.
    /// </returns>
    /// <remarks>
    /// Unsalted on purpose. A salt defends a low-entropy secret against a
    /// precomputed table; this input is 256 bits of randomness, so there is no
    /// table to build, and a per-row salt would only stop the digest from being
    /// looked up by index - which is exactly how a token is redeemed.
    /// </remarks>
    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
