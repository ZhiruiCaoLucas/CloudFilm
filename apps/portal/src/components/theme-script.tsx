/**
 * Applies the persisted colour scheme before the first paint.
 *
 * Runs as a blocking inline script on purpose: deferring it would show a light
 * flash before a dark reading room theme is applied.
 */
const SCRIPT = `(function () {
  try {
    var stored = window.localStorage.getItem('cloudfilm-theme');
    var prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    if (stored === 'dark' || (stored === null && prefersDark)) {
      document.documentElement.classList.add('dark');
    }
  } catch (error) {
    /* Storage is unavailable in private browsing; fall back to the light theme. */
  }
})();`;

export function ThemeScript() {
  return <script dangerouslySetInnerHTML={{ __html: SCRIPT }} suppressHydrationWarning />;
}
