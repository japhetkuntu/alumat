"use client";

import { useQuery } from "@tanstack/react-query";
import { ChevronDown, UsersRound } from "lucide-react";
import { getMyCommunities } from "@/lib/member-api";
import { cn } from "@alumni/ui";
import { DropdownMenu, DropdownMenuTrigger, DropdownMenuContent, DropdownMenuItem } from "@alumni/ui";

/**
 * "All" shows the merged feed (institution-wide + every community you belong
 * to) — the default. Picking a community narrows to just that community's
 * items. There's no "institution-only" option: the merge is the point.
 *
 * Two renderings of the same control: a row of pills on sm+ screens (dropped
 * straight into the caller's existing filter row, same visual language as
 * every other filter pill there), collapsing to a single compact dropdown
 * button below sm — a whole extra pill group wrapping onto its own broken
 * line is what made mobile look cluttered, not the filter itself.
 */
export function SourceFilterChips({ value, onChange, divider = true }: {
  value: string | null;
  onChange: (communityId: string | null) => void;
  /** Set false when these are the only pills in their row (no preceding filter group to separate from). */
  divider?: boolean;
}) {
  const { data } = useQuery({
    queryKey: ["my-communities-chips"],
    queryFn: getMyCommunities,
  });
  const communities = (data ?? []).filter((c) => c.myStatus === "Approved");

  if (communities.length === 0) return null;

  const chipClass = (active: boolean) => cn(
    "px-3.5 py-1.5 text-[12.5px] font-semibold transition-all duration-200 border",
    active
      ? "bg-primary text-primary-foreground border-transparent"
      : "bg-background text-muted-foreground border-border hover:border-primary/40",
  );

  const selectedLabel = value === null ? "All sources" : communities.find((c) => c.id === value)?.name ?? "All sources";

  return (
    <>
      {/* sm+ — inline pills, matching every other filter pill on the page */}
      {divider && <span className="hidden sm:block w-px h-5 shrink-0 self-center" style={{ background: "var(--border)" }} aria-hidden="true" />}
      <button onClick={() => onChange(null)} className={cn("hidden sm:inline-flex", chipClass(value === null))}>
        All sources
      </button>
      {communities.map((c) => (
        <button key={c.id} onClick={() => onChange(c.id)} className={cn("hidden sm:inline-flex", chipClass(value === c.id))}>
          {c.name}
        </button>
      ))}

      {/* below sm — one compact dropdown instead of a whole pill group wrapping onto its own line */}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <button
            className={cn(
              "sm:hidden inline-flex items-center gap-1.5 px-3 py-1.5 text-[12.5px] font-semibold border transition-colors",
              value !== null
                ? "bg-primary text-primary-foreground border-transparent"
                : "bg-background text-muted-foreground border-border",
            )}
          >
            <UsersRound size={12} />
            {selectedLabel}
            <ChevronDown size={12} />
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="start">
          <DropdownMenuItem onClick={() => onChange(null)} className={value === null ? "font-semibold" : undefined}>
            All sources
          </DropdownMenuItem>
          {communities.map((c) => (
            <DropdownMenuItem key={c.id} onClick={() => onChange(c.id)} className={value === c.id ? "font-semibold" : undefined}>
              {c.name}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
    </>
  );
}
