"use client";

import { useQuery } from "@tanstack/react-query";
import { getMyCommunities } from "@/lib/member-api";
import { cn } from "@alumni/ui";

/**
 * "All" shows the merged feed (institution-wide + every community you belong
 * to) — the default. Picking a community narrows to just that community's
 * items. There's no "institution-only" option: the merge is the point.
 */
export function SourceFilterChips({ value, onChange }: {
  value: string | null;
  onChange: (communityId: string | null) => void;
}) {
  const { data } = useQuery({
    queryKey: ["my-communities-chips"],
    queryFn: getMyCommunities,
  });
  const communities = (data ?? []).filter((c) => c.myStatus === "Approved");

  if (communities.length === 0) return null;

  return (
    <div className="flex items-center gap-2 flex-wrap">
      <button
        onClick={() => onChange(null)}
        className={cn(
          "px-3 py-1.5 rounded-full border text-[12.5px] font-semibold transition-colors",
          value === null ? "bg-primary/10 text-primary border-primary/30" : "bg-background text-foreground border-border hover:bg-muted"
        )}
      >
        All
      </button>
      {communities.map((c) => (
        <button
          key={c.id}
          onClick={() => onChange(c.id)}
          className={cn(
            "px-3 py-1.5 rounded-full border text-[12.5px] font-semibold transition-colors",
            value === c.id ? "bg-primary/10 text-primary border-primary/30" : "bg-background text-foreground border-border hover:bg-muted"
          )}
        >
          {c.name}
        </button>
      ))}
    </div>
  );
}
