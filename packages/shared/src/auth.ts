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

  /**
   * The application roles the account holds.
   *
   * Empty when it holds none, never absent and never null - the API states this
   * for every account, so there is no third case for a caller to handle. An
   * account with no role is a real state: it signs in successfully and is
   * refused everything else.
   *
   * Read from this field, never from the token. The access token is opaque to
   * the portal by design - it is written to an `HttpOnly` cookie the browser
   * cannot read, and decoding it would be a second, divergent source of truth
   * for something the API already answers.
   */
  readonly roles: readonly string[];
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

  /**
   * The application roles carried by the token, from its role claims.
   *
   * Empty when the account holds none. Resolved by the API from the token it
   * validated, so this costs no database query either.
   *
   * For display only. Hiding a control these do not cover is a courtesy to the
   * reader; it is not access control. Every protected endpoint enforces the same
   * rule again on every request, and that is the one that counts - a browser can
   * be told anything.
   */
  readonly roles: readonly string[];
}
