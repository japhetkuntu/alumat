import { Building2, UsersRound, GraduationCap } from "@alumni/ui";

/** Deterministic hue from a string so each community reads as a consistent color across the app, without needing a color field on the Community entity. */
function hueFor(seed: string): number {
  let hash = 0;
  for (let i = 0; i < seed.length; i++) hash = (hash * 31 + seed.charCodeAt(i)) >>> 0;
  return hash % 360;
}

/** "2020", "2020 & 2021", "2019, 2020 & 3 more" — never lets a long batch list blow out a one-line badge. */
function yearGroupsLabel(years: number[]): string {
  const sorted = [...years].sort((a, b) => a - b);
  if (sorted.length === 1) return `Class of ${sorted[0]}`;
  if (sorted.length === 2) return `Classes of ${sorted[0]} & ${sorted[1]}`;
  const shown = sorted.slice(0, 2).join(", ");
  return `Classes of ${shown} & ${sorted.length - 2} more`;
}

/**
 * Shows who a piece of content is actually for — the whole institution, one
 * specific Community, or one or more graduation-year batches — so a merged
 * feed stays legible about its real audience instead of every scoped item
 * reading as "for everyone" just because it has no community.
 */
export function SourceBadge({ communityId, communityName, yearGroups, className }: {
  communityId?: string | null;
  communityName?: string | null;
  yearGroups?: number[] | null;
  className?: string;
}) {
  if (communityId && communityName) {
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

  if (yearGroups && yearGroups.length > 0) {
    return (
      <span
        className={`inline-flex items-center gap-1 px-2 py-0.5 text-[10.5px] font-semibold bg-accent/10 text-accent ${className ?? ""}`}
      >
        <GraduationCap size={10} />
        {yearGroupsLabel(yearGroups)}
      </span>
    );
  }

  return (
    <span
      className={`inline-flex items-center gap-1 px-2 py-0.5 text-[10.5px] font-semibold ${className ?? ""}`}
      style={{ background: "var(--muted)", color: "var(--muted-foreground)" }}
    >
      <Building2 size={10} />
      Everyone
    </span>
  );
}
