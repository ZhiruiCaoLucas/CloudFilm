/**
 * Applies the colour scheme before the first paint, and keeps it in step with
 * the operating system afterwards.
 *
 * Three states, and the third is the default:
 *
 *   'dark'  - the reader chose dark. Overrides the system.
 *   'light' - the reader chose light. Overrides the system.
 *    absent - follow `prefers-color-scheme`, live.
 *
 * Runs as a blocking inline script on purpose: deferring it would show a light
 * flash before a dark reading room theme is applied. It also registers a
 * `matchMedia` listener so that a clinician who changes the desktop theme mid
 * shift sees the portal follow - but only while they have expressed no
 * preference of their own, because an explicit choice that the OS could
 * silently revoke is not a choice.
 *
 * `document.documentElement.classList` is the only mechanism. The stylesheet's
 * `html { color-scheme: light }` / `html.dark { color-scheme: dark }` pair hangs
 * off the same class, which is what keeps native scrollbars, autofill and form
 * widgets in step with the rest of the page rather than following the OS
 * independently.
 */
const SCRIPT = `(function () {
  var KEY = 'cloudfilm-theme';
  var root = document.documentElement;

  function readStored() {
    try {
      var value = window.localStorage.getItem(KEY);
      return value === 'dark' || value === 'light' ? value : null;
    } catch (error) {
      /* Storage is unavailable in private browsing. */
      return null;
    }
  }

  var media = window.matchMedia('(prefers-color-scheme: dark)');

  function apply() {
    var stored = readStored();
    var dark = stored === null ? media.matches : stored === 'dark';
    root.classList.toggle('dark', dark);
  }

  apply();

  // Follow the system while - and only while - nothing has been chosen here.
  var onChange = function () {
    if (readStored() === null) {
      apply();
    }
  };

  if (typeof media.addEventListener === 'function') {
    media.addEventListener('change', onChange);
  } else if (typeof media.addListener === 'function') {
    media.addListener(onChange);
  }

  // The control writes to storage and dispatches this; a second tab picks the
  // change up through the browser's own 'storage' event.
  window.addEventListener('cloudfilm:theme', apply);
  window.addEventListener('storage', function (event) {
    if (event.key === KEY || event.key === null) {
      apply();
    }
  });
})();`;

export function ThemeScript() {
  return <script dangerouslySetInnerHTML={{ __html: SCRIPT }} suppressHydrationWarning />;
}
