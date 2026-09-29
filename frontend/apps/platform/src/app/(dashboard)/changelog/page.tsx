"use client";

import { useMemo, useState } from "react";
import { Sparkles, Zap, ShieldCheck } from "@alumni/ui";
import { SegmentedControl } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { CHANGELOG_ENTRIES, type ChangelogEntry, type ChangelogScope, type ChangelogType } from "@/data/changelog";

const SCOPE_TABS: { value: ChangelogScope | "All"; label: string }[] = [
  { value: "All", label: "All" },
  { value: "Platform", label: "Platform" },
  { value: "Institution", label: "Institution" },
  { value: "Member", label: "Member" },
  { value: "Marketing", label: "Marketing" },
];

const TYPE_META: Record<ChangelogType, { icon: typeof Sparkles; color: string }> = {
  Feature: { icon: Sparkles, color: "var(--primary)" },
  Improvement: { icon: Zap, color: "var(--brand-accent-dark, var(--brand-accent, var(--primary)))" },
  Fix: { icon: ShieldCheck, color: "var(--warning, #b45309)" },
};

const SCOPE_COLOR: Record<ChangelogScope, string> = {
  Platform: "#7c3aed",
  Institution: "#0e7143",
  Member: "#2563eb",
  Marketing: "#b45309",
};

function EntryCard({ entry }: { entry: ChangelogEntry }) {
  const meta = TYPE_META[entry.type];
  const Icon = meta.icon;
  return (
    <div className="card p-5">
      <div className="flex items-start gap-3">
        <div className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0" style={{ background: `color-mix(in oklch, ${meta.color} 12%, transparent)` }}>
          <Icon size={15} style={{ color: meta.color }} />
        </div>
        <div className="min-w-0">
          <p className="text-[14.5px] font-semibold">{entry.title}</p>
          <p className="text-[12px] text-muted-foreground mt-0.5">{formatDate(entry.date)}</p>
        </div>
      </div>
      <p className="text-[13px] text-muted-foreground leading-relaxed mt-3">{entry.body}</p>
      <div className="flex flex-wrap gap-1.5 mt-3">
        {entry.scopes.map((s) => (
          <span
            key={s}
            className="text-[10.5px] font-bold uppercase tracking-wide px-2 py-0.5"
            style={{ background: `color-mix(in oklch, ${SCOPE_COLOR[s]} 12%, transparent)`, color: SCOPE_COLOR[s] }}
          >
            {s}
          </span>
        ))}
      </div>
    </div>
  );
}

export default function ChangelogPage() {
  const [scopeTab, setScopeTab] = useState<ChangelogScope | "All">("All");

  const entries = useMemo(
    () => (scopeTab === "All" ? CHANGELOG_ENTRIES : CHANGELOG_ENTRIES.filter((e) => e.scopes.includes(scopeTab))),
    [scopeTab],
  );

  return (
    <div className="p-4 sm:p-[26px] max-w-[820px] mx-auto space-y-5">
      <PageHeader
        eyebrow="Internal only — never shown to institutions or members"
        title="Changelog"
        description="What shipped, and which portals it touched — Platform, Institution, Member, or the marketing sites."
      />

      <SegmentedControl options={SCOPE_TABS} value={scopeTab} onChange={setScopeTab} label="Filter by portal" />

      {entries.length === 0 ? (
        <EmptyState title={`Nothing logged for ${scopeTab} yet`} description="Check back once something ships that touches this portal." />
      ) : (
        <div className="space-y-3">
          {entries.map((entry, i) => (
            <EntryCard key={`${entry.date}-${i}`} entry={entry} />
          ))}
        </div>
      )}
    </div>
  );
}
