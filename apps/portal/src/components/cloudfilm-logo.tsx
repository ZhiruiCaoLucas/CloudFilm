/**
 * The CloudFilm mark: a cloud enclosing an imaging aperture.
 *
 * Inline rather than a file asset so it inherits `currentColor` and needs no
 * network request on first paint. Decorative — the product name sits next to it
 * as real text, so this is hidden from assistive technology.
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
      <rect width="32" height="32" rx="8" className="fill-accent" />
      <path
        d="M11 21h10a4 4 0 0 0 .4-7.98A6 6 0 0 0 10.3 12.2 3.9 3.9 0 0 0 11 21Z"
        className="fill-accent-foreground"
        opacity="0.95"
      />
      <circle cx="16" cy="16.4" r="2.6" className="fill-accent" />
    </svg>
  );
}
