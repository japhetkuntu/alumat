/**
 * What a portal shows in the gap between "app mounted" and "auth check +
 * theme resolved" — previously that gap rendered nothing at all (a blank,
 * un-themed white flash), which is what actually reads as "bad loading" and
 * "default colors" to a visitor, since the real branded background/sidebar
 * hadn't painted yet. This mirrors the eventual sidebar + topbar + content
 * shape using the tenant's already-resolved CSS custom properties (they're
 * set on <html> server-side, before this ever renders), so nothing shifts
 * or re-colors when the real content swaps in a moment later.
 */
export function PortalShellSkeleton({ sidebarWidth = 240 }: { sidebarWidth?: number }) {
  const barStyle = { background: "color-mix(in oklch, var(--sidebar-foreground) 12%, transparent)" };
  return (
    <div className="flex h-dvh overflow-hidden" style={{ background: "var(--background)" }}>
      {/* Sidebar */}
      <div className="hidden lg:flex shrink-0 flex-col h-full border-r"
        style={{ width: sidebarWidth, background: "var(--sidebar)", borderColor: "var(--sidebar-border)" }}>
        <div className="p-4 flex items-center gap-2.5">
          <div className="w-8 h-8 rounded-lg shrink-0 animate-pulse" style={barStyle} />
          <div className="h-3.5 w-24 rounded animate-pulse" style={barStyle} />
        </div>
        <div className="flex-1 px-3 py-3 space-y-1.5">
          {Array.from({ length: 9 }).map((_, i) => (
            <div key={i} className="h-8 w-full rounded-md animate-pulse" style={{ ...barStyle, animationDelay: `${i * 60}ms` }} />
          ))}
        </div>
      </div>

      {/* Main column */}
      <div className="flex-1 flex flex-col min-w-0">
        <div className="h-14 shrink-0 border-b flex items-center justify-between px-6" style={{ borderColor: "var(--border)" }}>
          <div className="h-3.5 w-32 rounded animate-pulse" style={{ background: "var(--muted)" }} />
          <div className="w-8 h-8 rounded-full animate-pulse" style={{ background: "var(--muted)" }} />
        </div>
        <div className="flex-1 p-6 space-y-6 overflow-hidden">
          <div className="space-y-2">
            <div className="h-6 w-56 rounded animate-pulse" style={{ background: "var(--muted)" }} />
            <div className="h-3.5 w-80 max-w-full rounded animate-pulse" style={{ background: "var(--muted)" }} />
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="rounded-lg border p-5 space-y-3" style={{ borderColor: "var(--border)" }}>
                <div className="h-3 w-20 rounded animate-pulse" style={{ background: "var(--muted)" }} />
                <div className="h-6 w-16 rounded animate-pulse" style={{ background: "var(--muted)" }} />
                <div className="h-3 w-28 rounded animate-pulse" style={{ background: "var(--muted)" }} />
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
