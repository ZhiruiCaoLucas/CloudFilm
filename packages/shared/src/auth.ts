/**
 * Sign-in contracts.
 *
 * Mirror `CloudFilm.Application.Auth`. Nothing here describes the account
 * record: the API deliberately never projects `ApplicationUser`, so a password
 * hash, a security stamp or a lockout state has no type to travel in.
 */

/** A sign-in attempt. */
export interface LoginRequest {
  /** The e-mail address the account was registered with. */
  readonly email: string;

  /**
   * The password.
   *
   * Sent once, over TLS, and never stored by the portal. The token that comes
   * back is what subsequent requests carry.
   */
  readonly password: string;
}

/** A successful sign-in. */
export interface LoginResponse {
  /** The signed JWT, to be sent as `Authorization: Bearer <token>`. */
  readonly accessToken: string;

  /** When the token stops being accepted, as an ISO 8601 instant. */
  readonly expiresAt: string;

  /** The name to show in the portal. */
  readonly displayName: string;
}

/**
 * The signed-in user.
 *
 * Resolved by the API from token claims alone, so reading it costs no database
 * query.
 */
export interface CurrentUser {
  /** The account identifier, from the `sub` claim. */
  readonly userId: string;

  /** The e-mail address, from the `email` claim. */
  readonly email: string;

  /** The display name, from the `name` claim. */
  readonly displayName: string;
}
