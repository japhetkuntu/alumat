"use client";

import Link from "next/link";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ChangeStat, EmptyState, Figure, InsightSection, LoadError, TrendChart, cn, formatCurrency } from "@alumni/ui";
import {
  assignRecommendation, getCohorts, getEngagementDashboard, getHealthHistory, getInstitutionStaff, resolveRecommendation, setChecklistItem,
  type CohortsData, type EngagementDashboard, type EngagementRecommendation, type HealthFactor,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { WrongWorkspace } from "@/components/institution/wrong-workspace";
import { useAuth } from "@/hooks/use-auth";

const PERIODS = [7, 30, 90] as const;
const PRIMARY = "var(--brand-primary-500, var(--primary))";
const ACCENT = "var(--brand-accent-500, var(--brand-accent))";

const CLASSIFICATION: Record<string, { label: string; tone: string }> = {
  Healthy: { label: "Healthy", tone: "text-success" },
  NeedsAttention: { label: "Needs attention", tone: "text-foreground" },
  AtRisk: { label: "At risk", tone: "text-destructive" },
  Inactive: { label: "Inactive", tone: "text-destructive" },
  InsufficientData: { label: "Not enough data yet", tone: "text-muted-foreground" },
};

const weekLabel = (iso: string) => new Date(iso).toLocaleDateString("en-GB", { day: "numeric", month: "short" });

export default function EngagementPage() {
  const { user } = useAuth();
  const isAdmin = user?.role === "SuperAdmin";
  const qc = useQueryClient();
  const [days, setDays] = useState<number>(30);
  const dashboard = useQuery({ queryKey: ["engagement", days], queryFn: () => getEngagementDashboard(days), staleTime: 60_000, enabled: isAdmin });
  const history = useQuery({ queryKey: ["engagement-history", days], queryFn: () => getHealthHistory(days), staleTime: 60_000, enabled: isAdmin });
  const cohorts = useQuery({ queryKey: ["engagement-cohorts", days], queryFn: () => getCohorts(days), staleTime: 60_000, enabled: isAdmin });
  const staff = useQuery({ queryKey: ["engagement-staff"], queryFn: () => getInstitutionStaff(1, 50), staleTime: 5 * 60_000, enabled: isAdmin });

  const refresh = () => qc.invalidateQueries({ queryKey: ["engagement"] });
  const resolve = useMutation({
    mutationFn: (v: { id: string; action: "complete" | "dismiss" | "snooze" }) => resolveRecommendation(v.id, v.action, v.action === "snooze" ? 3 : undefined),
    onSuccess: (_, v) => { refresh(); toast.success(v.action === "complete" ? "Marked as done" : v.action === "snooze" ? "Hidden for 3 days" : "Dismissed"); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const assign = useMutation({
    mutationFn: (v: { id: string; staffId: string | null }) => assignRecommendation(v.id, v.staffId),
    onSuccess: () => { refresh(); toast.success("Updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const tick = useMutation({
    mutationFn: (v: { key: string; done: boolean }) => setChecklistItem(v.key, v.done),
    onSuccess: refresh,
    onError: (e) => toast.error(handleApiError(e)),
  });

  const d = dashboard.data;
  if (user && !isAdmin) return <WrongWorkspace title="This page is for institution administrators" description="Your own workspace shows the year groups you look after and the tasks passed to you." href="/ambassador" label="Open My year groups" />;
  if (dashboard.isError) return <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto"><LoadError title="The engagement workspace couldn’t load" onRetry={() => void dashboard.refetch()} /></div>;

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Community health</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5 max-w-[60ch]">
            How alive your community is, and the few things most worth doing about it. Figures refresh every few minutes.{" "}
            <Link href="/engagement/report" className="inline-block py-2 font-semibold text-primary underline underline-offset-4">Monthly report</Link>
          </p>
        </div>
        <div role="group" aria-label="Reporting period" className="flex border border-border">
          {PERIODS.map((p) => (
            <button key={p} type="button" aria-pressed={days === p} onClick={() => setDays(p)}
              className={cn("px-3.5 py-2 text-[13px] font-semibold min-h-[40px]", days === p ? "bg-primary text-primary-foreground" : "bg-card text-muted-foreground hover:text-foreground")}>
              {p} days
            </button>
          ))}
        </div>
      </div>

      <div className="grid grid-cols-1 items-start gap-4 lg:grid-cols-[minmax(0,5fr)_minmax(0,7fr)]">
        <HealthCard d={d} loading={dashboard.isLoading} />
        <RecommendationsCard
          items={d?.recommendations} loading={dashboard.isLoading}
          staff={(staff.data?.results ?? []).map((s) => ({ id: s.id, name: `${s.firstName} ${s.lastName}`.trim() }))}
          busy={resolve.isPending || assign.isPending}
          onResolve={(id, action) => resolve.mutate({ id, action })}
          onAssign={(id, staffId) => assign.mutate({ id, staffId })}
        />
      </div>

      <InsightSection
        question={`What happened in the last ${days} days?`}
        loading={dashboard.isLoading}
        answer={d && (
          <>
            <Figure>{d.participants.current.toLocaleString()}</Figure> of <Figure>{d.activeMembers.toLocaleString()}</Figure> members did something meaningful: started or answered a discussion, signed up for an event, gave, or posted a class note. Signing in alone does not count.
          </>
        )}
      >
        {d && (
          <div className="grid grid-cols-2 gap-x-6 gap-y-6 lg:grid-cols-4">
            <ChangeStat label="New members" current={d.newMembers.current} previous={d.newMembers.previous} period={`${days} days`} />
            <ChangeStat label="Members taking part" current={d.participants.current} previous={d.participants.previous} period={`${days} days`} />
            <Stat label="Signed in, last 7 days" value={d.signedInLast7Days} note={`${d.signedInLast30Days.toLocaleString()} in the last 30 days`} />
            <Stat label="Quiet for 30+ days" value={d.dormant} note={d.dormant === 0 ? "Nobody has gone quiet" : "Joined over a month ago, no sign-in or activity"} />
            <Stat label="Activated members" value={d.activated} note="Signed in and filled in part of their profile" />
            <Stat label="Came back to take part" value={d.retentionPercent === undefined ? "n/a" : `${d.retentionPercent}%`} note={d.retentionPercent === undefined ? "Too few members in the previous period to compare" : "Of those who took part before, took part again"} />
            <Stat label="Upcoming events" value={d.upcomingEvents} note="In the next 30 days" />
            <Stat label="Contributions" value={formatCurrency(d.contributionVolume)} note={`Gross, from ${d.contributors.toLocaleString()} ${d.contributors === 1 ? "member" : "members"}. Before fees.`} />
          </div>
        )}
      </InsightSection>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <InsightSection question="Is activity growing week by week?" loading={dashboard.isLoading}
          answer={d && <>The last eight weeks, from people joining and members taking part.</>}>
          <TrendChart
            data={(d?.weekly ?? []).map((w) => ({ week: weekLabel(w.weekStart), Joined: w.newMembers, "Meaningful actions": w.meaningfulActions }))}
            xKey="week" variant="bar" height={190} loading={dashboard.isLoading}
            series={[{ key: "Joined", label: "Joined", color: ACCENT }, { key: "Meaningful actions", label: "Meaningful actions", color: PRIMARY }]}
            emptyMessage="Nothing to show yet" valueFormatter={(v) => v.toLocaleString()}
          />
        </InsightSection>
        <InsightSection question="How has the health score moved?" loading={history.isLoading}
          answer={<>One reading a day, kept so changes can be explained.</>}>
          <TrendChart
            data={(history.data ?? []).filter((h) => h.score !== undefined && h.score !== null).map((h) => ({ day: weekLabel(h.date), Score: h.score as number }))}
            xKey="day" variant="line" height={190} loading={history.isLoading}
            series={[{ key: "Score", label: "Health score", color: PRIMARY }]}
            emptyMessage="The first readings will appear here as days pass" valueFormatter={(v) => `${v}`}
          />
        </InsightSection>
      </div>

      <CohortsSection data={cohorts.data} loading={cohorts.isLoading} days={days} />

      <Checklist d={d} loading={dashboard.isLoading} onTick={(key, done) => tick.mutate({ key, done })} />
    </div>
  );
}

function Stat({ label, value, note }: { label: string; value: number | string; note: string }) {
  return (
    <div className="border-l-2 border-border pl-4">
      <p className="text-[26px] font-bold leading-none tabular-nums text-foreground">{typeof value === "number" ? value.toLocaleString() : value}</p>
      <p className="mt-1.5 text-[14px] text-foreground">{label}</p>
      <p className="mt-0.5 text-[12.5px] text-muted-foreground">{note}</p>
    </div>
  );
}

function HealthCard({ d, loading }: { d?: EngagementDashboard; loading: boolean }) {
  const h = d?.health;
  const klass = h ? CLASSIFICATION[h.classification] ?? CLASSIFICATION.NeedsAttention : undefined;
  const change = h?.score !== undefined && h?.previousScore !== undefined && h.previousScore !== null ? h.score - h.previousScore : undefined;
  return (
    <InsightSection question="Community health" loading={loading}
      answer={h && (
        <span className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
          {h.score !== undefined && h.score !== null
            ? <><span className="text-[44px] font-bold leading-none tabular-nums">{h.score}</span><span className="text-muted-foreground">out of 100</span></>
            : <span className="text-[20px] font-bold">No score yet</span>}
          <span className={cn("font-semibold", klass?.tone)}>{klass?.label}</span>
          {change !== undefined && change !== 0 && <span className={cn("text-[13px]", change > 0 ? "text-success" : "text-muted-foreground")}>{change > 0 ? "up" : "down"} {Math.abs(change)} since the last reading</span>}
        </span>
      )}>
      {h && (
        <>
          <p className="text-[14px] text-muted-foreground">{h.summary}</p>
          <ul className="mt-5 space-y-4">{h.factors.map((f) => <Factor key={f.key} f={f} />)}</ul>
        </>
      )}
    </InsightSection>
  );
}

function Factor({ f }: { f: HealthFactor }) {
  const counted = f.score !== undefined && f.score !== null;
  return (
    <li>
      <div className="flex items-baseline justify-between gap-3">
        <span className="text-[14px] font-semibold">{f.label}</span>
        <span className="shrink-0 whitespace-nowrap text-[13px] tabular-nums text-muted-foreground">{counted ? `${f.score}/100 · ${f.effectiveWeight}% of the score` : "Left out"}</span>
      </div>
      <div className="mt-1.5 h-1.5 bg-muted" aria-hidden>
        {counted && <div className="h-full bg-primary" style={{ width: `${f.score}%` }} />}
      </div>
      <p className="mt-1.5 text-[12.5px] leading-relaxed text-muted-foreground">{f.detail}</p>
    </li>
  );
}

function RecommendationsCard({ items, loading, staff, busy, onResolve, onAssign }: {
  items?: EngagementRecommendation[]; loading: boolean; staff: { id: string; name: string }[]; busy: boolean;
  onResolve: (id: string, action: "complete" | "dismiss" | "snooze") => void; onAssign: (id: string, staffId: string | null) => void;
}) {
  return (
    <InsightSection question="What to do next" loading={loading}
      answer={items && (items.length === 0
        ? <>Nothing needs your attention right now.</>
        : <><Figure>{items.length}</Figure> {items.length === 1 ? "suggestion" : "suggestions"}, most important first.</>)}>
      {items && items.length === 0 && (
        <EmptyState title="You are up to date" description="New suggestions appear here when something worth acting on comes up, such as new members to welcome or an event that needs promoting." />
      )}
      <ul className="divide-y divide-border">
        {items?.map((r) => (
          <li key={r.id} className="py-4 first:pt-0">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p className="text-[15px] font-semibold leading-snug">
                  {r.priority === "High" && <span className="mr-2 text-[11.5px] font-bold uppercase tracking-wide text-destructive">Important</span>}
                  {r.title}
                </p>
                <p className="mt-1 text-[13.5px] text-muted-foreground leading-relaxed">{r.explanation}</p>
                {r.assignedToName && <p className="mt-1 text-[12.5px] text-muted-foreground">Delegated to {r.assignedToName}</p>}
              </div>
            </div>
            <div className="mt-1 flex flex-wrap items-center gap-x-4 text-[13px]">
              <Link href={r.actionUrl} className="inline-block py-2 font-semibold text-primary underline underline-offset-4">{r.actionLabel}</Link>
              <button type="button" disabled={busy} onClick={() => onResolve(r.id, "complete")} className="py-2 text-[13px] font-semibold hover:underline underline-offset-4">Mark done</button>
              <button type="button" disabled={busy} onClick={() => onResolve(r.id, "snooze")} className="py-2 text-[13px] text-muted-foreground hover:underline underline-offset-4">Remind me in 3 days</button>
              <button type="button" disabled={busy} onClick={() => onResolve(r.id, "dismiss")} className="py-2 text-[13px] text-muted-foreground hover:underline underline-offset-4">Dismiss</button>
              {staff.length > 1 && (
                <label className="ml-auto flex items-center gap-2 text-muted-foreground">
                  <span className="sr-only">Delegate to</span>
                  <select value={r.assignedToId ?? ""} disabled={busy} onChange={(e) => onAssign(r.id, e.target.value || null)}
                    className="border border-border bg-card px-2 py-1 text-[13px] min-h-[40px]">
                    <option value="">Delegate to…</option>
                    {staff.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                  </select>
                </label>
              )}
            </div>
          </li>
        ))}
      </ul>
    </InsightSection>
  );
}

function Checklist({ d, loading, onTick }: { d?: EngagementDashboard; loading: boolean; onTick: (key: string, done: boolean) => void }) {
  const done = d?.checklist.filter((i) => i.done).length ?? 0;
  const total = d?.checklist.length ?? 0;
  return (
    <InsightSection question="This week's checklist" loading={loading}
      answer={d && <><Figure>{done}</Figure> of <Figure>{total}</Figure> done for the week starting {weekLabel(d.weekStart)}. Items appear only where they apply to your community.</>}>
      <ul className="divide-y divide-border">
        {d?.checklist.map((i) => (
          <li key={i.key} className="flex items-start gap-3 py-3 first:pt-0">
            <input type="checkbox" checked={i.done} disabled={i.automaticallyDone} id={`chk-${i.key}`}
              onChange={(e) => onTick(i.key, e.target.checked)} className="shrink-0 size-5 mt-0.5 accent-[var(--primary)]" />
            <label htmlFor={`chk-${i.key}`} className="min-w-0 flex-1">
              <span className={cn("block text-[14.5px] font-semibold", i.done && "text-muted-foreground line-through")}>{i.title}</span>
              <span className="block text-[13px] text-muted-foreground">{i.automaticallyDone ? `${i.detail} Done automatically.` : i.detail}</span>
            </label>
            <Link href={i.actionUrl} className="inline-flex min-h-11 min-w-11 shrink-0 items-center justify-center px-1 text-[13px] font-semibold text-primary underline underline-offset-4">Open</Link>
          </li>
        ))}
      </ul>
    </InsightSection>
  );
}

export function CohortsSection({ data, loading, days }: { data?: CohortsData; loading: boolean; days: number }) {
  if (!loading && (!data || data.cohorts.length === 0)) return null; // institutions without year groups never see this
  const uncovered = data ? data.cohortsWithMembers - data.cohortsCovered : 0;
  return (
    <InsightSection
      question="Year groups and ambassadors"
      loading={loading}
      action={<Link href="/staff" className="inline-block py-2 font-semibold text-primary underline underline-offset-4">Manage ambassadors</Link>}
      answer={data && (data.cohortsWithMembers === 0
        ? <>No year group has enough members yet to need an ambassador.</>
        : <><Figure>{data.cohortsCovered}</Figure> of <Figure>{data.cohortsWithMembers}</Figure> year groups have an ambassador{uncovered > 0 ? <>. <Figure>{uncovered}</Figure> {uncovered === 1 ? "has" : "have"} nobody looking after {uncovered === 1 ? "it" : "them"}.</> : <>.</>}</>)}
    >
      {data && (
        <>
          {/* Phone: one compact card per year group instead of a seven-column table. */}
          <ul className="space-y-3 sm:hidden">
            {data.cohorts.map((c) => (
              <li key={c.year} className="border border-border p-3">
                <div className="flex items-baseline justify-between gap-3">
                  <p className="text-[15px] font-bold tabular-nums">Class of {c.year}</p>
                  <p className="text-[12.5px] text-muted-foreground">{c.ambassadors.length === 0 ? (c.activeMembers >= 3 ? "No ambassador" : "Too few members") : c.ambassadors.map((x) => x.name + (x.active ? "" : " (quiet)")).join(", ")}</p>
                </div>
                <dl className="mt-2 grid grid-cols-2 gap-x-4 gap-y-1.5 text-[13px]">
                  <div><dt className="text-muted-foreground">Members</dt><dd className="font-semibold tabular-nums">{c.activeMembers}</dd></div>
                  <div><dt className="text-muted-foreground">Joined, last {days} days</dt><dd className="font-semibold tabular-nums">{c.newMembers}</dd></div>
                  <div><dt className="text-muted-foreground">Active</dt><dd className="font-semibold tabular-nums">{c.activeMembers === 0 ? "n/a" : `${Math.round(100 * c.activated / c.activeMembers)}%`}</dd></div>
                  <div><dt className="text-muted-foreground">Took part</dt><dd className="font-semibold tabular-nums">{c.activeMembers === 0 ? "n/a" : `${Math.round(100 * c.participants / c.activeMembers)}%`}</dd></div>
                  <div><dt className="text-muted-foreground">Joined by invitation</dt><dd className="font-semibold tabular-nums">{c.invitationsRegistered}</dd></div>
                </dl>
              </li>
            ))}
          </ul>
          <div className="hidden overflow-x-auto sm:block">
            <table className="w-full min-w-[640px] text-[13.5px]">
              <thead>
                <tr className="text-left text-[12px] text-muted-foreground">
                  <th className="py-2 pr-3 font-semibold">Year group</th><th className="py-2 pr-3 text-right font-semibold">Members</th>
                  <th className="py-2 pr-3 text-right font-semibold">Joined, last {days} days</th><th className="py-2 pr-3 text-right font-semibold">Active</th>
                  <th className="py-2 pr-3 text-right font-semibold">Took part</th><th className="py-2 pr-3 text-right font-semibold">Joined by invitation</th><th className="py-2 font-semibold">Ambassador</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data.cohorts.map((c) => (
                  <tr key={c.year}>
                    <td className="py-2.5 pr-3 font-semibold tabular-nums">{c.year}</td>
                    <td className="py-2.5 pr-3 text-right tabular-nums">{c.activeMembers}</td>
                    <td className="py-2.5 pr-3 text-right tabular-nums">{c.newMembers}</td>
                    <td className="py-2.5 pr-3 text-right tabular-nums">{c.activeMembers === 0 ? "n/a" : `${Math.round(100 * c.activated / c.activeMembers)}%`}</td>
                    <td className="py-2.5 pr-3 text-right tabular-nums">{c.activeMembers === 0 ? "n/a" : `${Math.round(100 * c.participants / c.activeMembers)}%`}</td>
                    <td className="py-2.5 pr-3 text-right tabular-nums">{c.invitationsRegistered}</td>
                    <td className="py-2.5">
                      {c.ambassadors.length === 0
                        ? <span className="text-muted-foreground">{c.activeMembers >= 3 ? "None yet" : "Too few members"}</span>
                        : c.ambassadors.map((a) => <span key={a.staffId} className="mr-2 whitespace-nowrap">{a.name}{!a.active && <span className="text-muted-foreground"> (quiet)</span>}</span>)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {data.ambassadors.length > 0 && (
            <div className="mt-6 border-t border-border pt-4">
              <h3 className="text-[13px] font-semibold text-muted-foreground">Ambassadors</h3>
              <ul className="mt-2 divide-y divide-border">
                {data.ambassadors.map((a) => (
                  <li key={a.staffId} className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 py-2.5 text-[13.5px]">
                    <span><span className="font-semibold">{a.name}</span> <span className="text-muted-foreground">looks after {a.yearGroups.join(", ")}</span></span>
                    <span className={a.active ? "text-muted-foreground" : "text-destructive"}>
                      {a.daysSinceActive < 0 ? "Has not signed in yet" : a.daysSinceActive === 0 ? "Active today" : `Last active ${a.daysSinceActive} days ago`}
                      {" · "}{a.tasksCompleted} {a.tasksCompleted === 1 ? "task" : "tasks"} done, {a.tasksOpen} open
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </>
      )}
    </InsightSection>
  );
}
