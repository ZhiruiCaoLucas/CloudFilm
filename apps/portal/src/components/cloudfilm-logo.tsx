/**
 * The CloudFilm mark: a cross inside a cloud.
 *
 * Atrium states the identity as a two-tone blue tile — the deep ink ground with
 * the cloud and the medical cross cut out of it in white. Inline rather than a
 * file asset so it needs no network request on first paint, and decorative:
 * the product name sits next to it as real text, so this is hidden from
 * assistive technology.
 *
 * The export contract is unchanged — one optional `className`, sized by the
 * caller.
 */
export function CloudFilmLogo({ className }: { readonly className?: string }) {
  return (
    <svg
      viewBox="0 0 32 32"
      className={className}
      role="presentation"
      aria-hidden="true"
      focusable="false"
    >
      <rect width="32" height="32" rx="9" fill="var(--atrium-ink)" />
      <rect width="32" height="32" rx="9" fill="url(#cf-mark-sheen)" />
      <path
        d="M10.6 22.4h11.2a4.3 4.3 0 0 0 .45-8.58 6.4 6.4 0 0 0-11.9-1.9 4.2 4.2 0 0 0 .25 10.48Z"
        fill="#ffffff"
        opacity="0.96"
      />
      <path
        d="M14.75 13.9h2.5v2.6h2.6v2.5h-2.6v2.6h-2.5v-2.6h-2.6v-2.5h2.6Z"
        fill="var(--atrium-primary)"
      />
      <defs>
        <linearGradient id="cf-mark-sheen" x1="0" y1="0" x2="0" y2="32">
          <stop offset="0" stopColor="#ffffff" stopOpacity="0.18" />
          <stop offset="1" stopColor="#ffffff" stopOpacity="0" />
        </linearGradient>
      </defs>
    </svg>
  );
}
