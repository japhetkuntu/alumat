"use client";

import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { EmptyState, Figure, InsightSection, LoadError, cn } from "@alumni/ui";
import { getInstitutionHealth, type InstitutionHealthRow } from "@/lib/platform-api";
import { TaskDialog, type TaskPrefill } from "@/components/platform/work/task-dialog";

const FILTERS = [
  { value: "attention", label: "Needs attention" },
  { value: "all", label: "All institutions" },
  { value: "onboarding", label: "Onboarding" },
] as const;

const KLASS: Record<string, string> = { Healthy: "Healthy", NeedsAttention: "Needs attention", AtRisk: "At risk", Inactive: "Inactive", InsufficientData: "Too few members to score" };

export default function InstitutionHealthPage() {
  const [filter, setFilter] = useState<(typeof FILTERS)[number]["value"]>("attention");
  const [taskFor, setTaskFor] = useState<TaskPrefill | null>(null);
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["institution-health"], queryFn: getInstitutionHealth, staleTime: 60_000 });

  if (isError) return <div className="p-4 sm:p-7 max-w-[1240px] mx-auto"><LoadError title="Institution health couldn’t load" onRetry={() => void refetch()} /></div>;

  const s = data?.summary;
  const rows = (data?.institutions ?? []).filter((r) => filter === "all" || (filter === "attention" ? r.status === "NeedsAttention" : r.status === "Onboarding"));

  return (
    <div className="p-4 sm:p-7 max-w-[1240px] mx-auto space-y-4">
      <div>
        <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Institution health</h1>
        <p className="text-muted-foreground text-[13px] mt-1.5 max-w-[64ch]">
          Which institutions are thriving and which would welcome a call. Readings come from each institution&apos;s own daily community health score. You see counts and scores only, never members or payments.
        </p>
      </div>

      <InsightSection question="Across the platform" loading={isLoading}
        answer={s && (s.institutions === 0 ? <>No live institutions yet.</> : <>
          <Figure>{s.needsAttention}</Figure> of <Figure>{s.institutions}</Figure> live {s.institutions === 1 ? "institution needs" : "institutions need"} attention
          {s.averageScore !== undefined && s.averageScore !== null && <>. The average health score is <Figure>{s.averageScore}</Figure> out of 100</>}.
        </>)}>
        {s && s.institutions > 0 && (
          <div className="grid grid-cols-2 gap-x-6 gap-y-5 lg:grid-cols-5">
            <Tile label="Healthy" value={s.healthy} />
            <Tile label="At risk or inactive" value={s.atRiskOrInactive} />
            <Tile label="Onboarding" value={s.onboarding} note="Under two weeks old; not judged yet" />
            <Tile label="With ambassadors" value={s.withAmbassadors} note="Year-group ambassadors appointed" />
            <Tile label="No reading yet" value={s.noReading} />
          </div>
        )}
      </InsightSection>

      <div role="group" aria-label="Show" className="flex flex-wrap gap-2">
        {FILTERS.map((f) => (
          <button key={f.value} type="button" aria-pressed={filter === f.value} onClick={() => setFilter(f.value)}
            className={cn("text-[12.5px] font-medium px-3 py-1.5 border transition-colors", filter === f.value ? "bg-primary/10 text-primary border-primary/30" : "bg-background text-muted-foreground border-border hover:bg-muted")}>
            {f.label}
          </button>
        ))}
      </div>

      <section className="border border-border bg-card">
        {isLoading && <div className="p-6 space-y-3" aria-busy="true">{[0, 1, 2].map((i) => <div key={i} className="skeleton h-14 w-full rounded-none" />)}</div>}
        {!isLoading && rows.length === 0 && (
          <EmptyState title={filter === "attention" ? "No institution needs attention" : "Nothing to show"}
            description={filter === "attention" ? "Every institution past its first two weeks is on track. Check back after the next daily reading." : "Try another view."} />
        )}
        <ul className="divide-y divide-border">
          {rows.map((r) => <Row key={r.institutionId} r={r} onFollowUp={() => setTaskFor({ title: `Follow up with ${r.name}`, description: r.reasons.join("\n"), institutionId: r.institutionId })} />)}
        </ul>
      </section>

      {taskFor && <TaskDialog prefill={taskFor} onClose={() => setTaskFor(null)} onSaved={() => { setTaskFor(null); void refetch(); toast.success("Follow-up created. Find it under Activation, My tasks."); }} />}
    </div>
  );
}

function Tile({ label, value, note }: { label: string; value: number; note?: string }) {
  return (
    <div className="border-l-2 border-border pl-4">
      <p className="text-[26px] font-bold leading-none tabular-nums">{value.toLocaleString()}</p>
      <p className="mt-1.5 text-[14px]">{label}</p>
      {note && <p className="mt-0.5 text-[12.5px] text-muted-foreground">{note}</p>}
    </div>
  );
}

function Row({ r, onFollowUp }: { r: InstitutionHealthRow; onFollowUp: () => void }) {
  return (
    <li className="p-4 sm:p-5">
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <div className="min-w-0">
          <Link href={`/institutions/${r.institutionId}`} className="inline-block py-1.5 text-[15px] font-semibold hover:underline">{r.name}</Link>
          <span className="ml-2 text-[13px] text-muted-foreground">{r.classification ? KLASS[r.classification] ?? r.classification : "No reading yet"}</span>
        </div>
        <div className="text-[14px] tabular-nums">
          {r.score !== undefined && r.score !== null ? <><strong className="text-[18px]">{r.score}</strong><span className="text-muted-foreground">/100</span></> : <span className="text-muted-foreground">No score</span>}
          {r.scoreChange !== undefined && r.scoreChange !== null && r.scoreChange !== 0 && (
            <span className={cn("ml-2 text-[13px]", r.scoreChange > 0 ? "text-success" : "text-destructive")}>{r.scoreChange > 0 ? "+" : ""}{r.scoreChange} this week</span>
          )}
        </div>
      </div>
      <p className="mt-1 text-[12.5px] text-muted-foreground">
        {r.activeMembers.toLocaleString()} active {r.activeMembers === 1 ? "member" : "members"}
        {" · "}{r.daysSinceAdminActive === undefined || r.daysSinceAdminActive === null ? "no administrator sign-in yet" : r.daysSinceAdminActive === 0 ? "administrator active today" : `administrator last active ${r.daysSinceAdminActive} days ago`}
        {" · "}{r.openSuggestions} suggested {r.openSuggestions === 1 ? "action" : "actions"} waiting
        {" · "}{r.ambassadors} {r.ambassadors === 1 ? "ambassador" : "ambassadors"}
      </p>
      {r.reasons.length > 0 && (
        <ul className="mt-2 space-y-0.5 text-[13.5px]">{r.reasons.map((x) => <li key={x}>{x}</li>)}</ul>
      )}
      <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-[13px]">
        <button type="button" onClick={onFollowUp} className="py-2 text-[13px] font-semibold text-primary underline underline-offset-4">Create a follow-up task</button>
        {r.openFollowUps > 0 && <Link href="/activation?tab=tasks" className="text-muted-foreground underline underline-offset-4">{r.openFollowUps} open {r.openFollowUps === 1 ? "follow-up" : "follow-ups"}{r.overdueFollowUps > 0 ? `, ${r.overdueFollowUps} overdue` : ""}</Link>}
      </div>
    </li>
  );
}
