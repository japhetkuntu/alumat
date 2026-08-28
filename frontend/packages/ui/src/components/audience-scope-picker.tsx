"use client";

import { YearGroupPicker } from "./year-group-picker";
import { FormSelect } from "./select";
import { Label } from "./label";
import { cn } from "../lib/utils";

export type AudienceMode = "everyone" | "community" | "yearGroups";

export interface AudienceScopeCommunity {
  id: string;
  name: string;
}

export interface AudienceScopePickerProps {
  mode: AudienceMode;
  onModeChange: (mode: AudienceMode) => void;
  communityId: string;
  onCommunityChange: (id: string) => void;
  communities: AudienceScopeCommunity[];
  yearGroups: number[];
  onYearGroupsChange: (years: number[]) => void;
  /** Whether this content type supports community scoping at all — false hides that option entirely (e.g. Jobs, News). */
  supportsCommunity: boolean;
  /** When set, the whole interactive control is replaced by one static message — e.g. for a ScopedAdmin who can't choose an audience. */
  restricted?: { reason: string };
  className?: string;
}

/**
 * Single choice of who sees this item — "Everyone" / "A specific community"
 * (only when supportsCommunity) / "Specific year groups" — never two
 * independently-combinable controls, which used to let an admin silently
 * intersect a community and a year group into a much narrower, unindicated
 * audience.
 */
export function AudienceScopePicker({
  mode,
  onModeChange,
  communityId,
  onCommunityChange,
  communities,
  yearGroups,
  onYearGroupsChange,
  supportsCommunity,
  restricted,
  className,
}: AudienceScopePickerProps) {
  if (restricted) {
    return (
      <div className={cn("space-y-1.5", className)}>
        <Label>Who can see this?</Label>
        <p className="text-xs text-muted-foreground">{restricted.reason}</p>
      </div>
    );
  }

  const options: { value: AudienceMode; label: string }[] = [
    { value: "everyone", label: "Everyone" },
    ...(supportsCommunity ? [{ value: "community" as const, label: "A specific community" }] : []),
    { value: "yearGroups", label: "Specific year groups" },
  ];

  return (
    <div className={cn("space-y-2.5", className)}>
      <Label>Who can see this?</Label>
      <div className="flex flex-wrap gap-2">
        {options.map((opt) => (
          <button
            key={opt.value}
            type="button"
            onClick={() => onModeChange(opt.value)}
            className={cn(
              "px-3 py-1.5 text-[11px] font-bold uppercase tracking-[.05em] border transition-colors",
              mode === opt.value
                ? "text-white border-transparent"
                : "border-border text-muted-foreground hover:border-primary/40"
            )}
            style={mode === opt.value ? { background: "var(--primary)" } : undefined}
          >
            {opt.label}
          </button>
        ))}
      </div>

      {mode === "everyone" && (
        <p className="text-xs text-muted-foreground">Visible to every member of the institution.</p>
      )}

      {mode === "community" && supportsCommunity && (
        <FormSelect
          value={communityId}
          onValueChange={onCommunityChange}
          options={communities.map((c) => ({ value: c.id, label: c.name }))}
          placeholder="Select a community"
        />
      )}

      {mode === "yearGroups" && (
        <YearGroupPicker value={yearGroups} onChange={onYearGroupsChange} />
      )}
    </div>
  );
}

/** Derives the picker's mode from stored community/year-group values, e.g. when opening an item to edit. */
export function inferAudienceMode(communityId: string | null | undefined, yearGroups: number[] | null | undefined): AudienceMode {
  if (communityId) return "community";
  if (yearGroups && yearGroups.length > 0) return "yearGroups";
  return "everyone";
}
