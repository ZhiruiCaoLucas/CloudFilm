/**
 * Shown while the viewer's availability is still unknown.
 *
 * Distinct from the offline state on purpose: reporting "offline" before the
 * probe has answered would be a claim the application cannot yet support.
 */
export function ViewerConnecting() {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-2 px-6 text-center">
      <p className="text-sm font-medium text-slate-300">Connecting to viewer…</p>
      <p className="text-sm text-slate-500">Checking whether the imaging viewer is available.</p>
    </div>
  );
}
