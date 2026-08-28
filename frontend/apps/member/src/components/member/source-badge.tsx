import { Building2, UsersRound } from "lucide-react";

/** Deterministic hue from a string so each community reads as a consistent color across the app, without needing a color field on the Community entity. */
function hueFor(seed: string): number {
  let hash = 0;
  for (let i = 0; i < seed.length; i++) hash = (hash * 31 + seed.charCodeAt(i)) >>> 0;
  return hash % 360;
}

/**
 * Shows where a piece of content came from — the whole institution, or one
 * specific Community — so a merged feed stays legible about its sources
 * without needing to visit each Community separately to know what's there.
 */
export function SourceBadge({ communityId, communityName, className }: {
  communityId?: string | null;
  communityName?: string | null;
  className?: string;
}) {
  if (!communityId || !communityName) {
    return (
      <span
        className={`inline-flex items-center gap-1 px-2 py-0.5 text-[10.5px] font-semibold ${className ?? ""}`}
        style={{ background: "var(--muted)", color: "var(--muted-foreground)" }}
      >
        <Building2 size={10} />
        Institution
      </span>
    );
  }

  const hue = hueFor(communityId);
  return (
    <span
      className={`inline-flex items-center gap-1 px-2 py-0.5 text-[10.5px] font-semibold ${className ?? ""}`}
      style={{
        background: `hsl(${hue} 70% 95%)`,
        color: `hsl(${hue} 55% 32%)`,
      }}
    >
      <UsersRound size={10} />
      {communityName}
    </span>
  );
}
