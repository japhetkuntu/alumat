"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChangeStat, Figure, InsightSection, LoadError, formatCurrency } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { WrongWorkspace } from "@/components/institution/wrong-workspace";
import { getMonthlyReport, type MonthlyReport } from "@/lib/institution-api";

/** The last twelve months, newest first, as the "2026-09" the server expects and a label people read. */
function monthOptions() {
  const now = new Date();
  return Array.from({ length: 12 }, (_, i) => {
    const d = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - i, 1));
    return { value: `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, "0")}`, label: d.toLocaleDateString("en-GB", { month: "long", year: "numeric", timeZone: "UTC" }) };
  });
}

function csv(r: MonthlyReport) {
  const c = r.current, p = r.previous;
  const rows: [string, string, number | string, number | string][] = [
    ["Outcomes", "New members", c.newMembers, p.newMembers], ["Outcomes", "Members who took part", c.participants, p.participants],
    ["Outcomes", "Meaningful actions", c.meaningfulActions, p.meaningfulActions], ["Outcomes", "Event sign-ups", c.eventSignUps, p.eventSignUps],
    ["Money", "Collected", c.amountCollected, p.amountCollected], ["Money", "Members who gave", c.contributors, p.contributors],
    ["Activity", "Events held", c.eventsHeld, p.eventsHeld], ["Activity", "News published", c.newsPublished, p.newsPublished],
    ["Activity", "Opportunities shared", c.opportunitiesShared, p.opportunitiesShared], ["Activity", "Suggestions completed", c.suggestionsCompleted, p.suggestionsCompleted],
    ["Health", "Score at start of month", c.healthStart ?? "", p.healthStart ?? ""], ["Health", "Score at end of month", c.healthEnd ?? "", p.healthEnd ?? ""],
  ];
  const esc = (v: string | number) => `"${String(v).replace(/"/g, '""')}"`;
  return [["Section", "Measure", r.month, "Month before"], ...rows].map((row) => row.map(esc).join(",")).join("\n");
}

export default function MonthlyReportPage() {
  const { user } = useAuth();
  const isAdmin = user?.role === "SuperAdmin";
  const options = useMemo(() => monthOptions(), []);
  const [month, setMonth] = useState(options[0].value);
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["engagement-monthly", month], queryFn: () => getMonthlyReport(month), staleTime: 60_000, enabled: isAdmin });

  function download() {
    if (!data) return;
    const url = URL.createObjectURL(new Blob([csv(data)], { type: "text/csv;charset=utf-8" }));
    const a = document.createElement("a");
    a.href = url; a.download = `engagement-${data.month}.csv`; a.click();
    URL.revokeObjectURL(url);
  }

  if (user && !isAdmin) return <WrongWorkspace title="This report is for institution administrators" description="Your own workspace shows the year groups you look after." href="/ambassador" label="Open My year groups" />;
  if (isError) return <div className="p-4 sm:p-[26px] max-w-[1000px] mx-auto"><LoadError title="The report couldn’t load" onRetry={() => void refetch()} /></div>;
  const c = data?.current, p = data?.previous;
  const label = options.find((o) => o.value === month)?.label ?? month;

  return (
    <div className="p-4 sm:p-[26px] max-w-[1000px] mx-auto space-y-4 print:p-0">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <Link href="/engagement" className="inline-block py-2 text-[13px] text-muted-foreground underline underline-offset-4 print:hidden">Community health</Link>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0 mt-1">Monthly report: {label}</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5 max-w-[62ch]">
            Each figure is set against the month before. What members did is kept apart from what administrators did: doing more is not the same as it helping.
          </p>
        </div>
        <div className="flex items-center gap-2 print:hidden">
          <label className="sr-only" htmlFor="month">Month</label>
          <select id="month" value={month} onChange={(e) => setMonth(e.target.value)} className="border border-border bg-card px-3 py-2 text-[14px] min-h-[40px]">
            {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
          <button type="button" onClick={download} disabled={!data} className="border border-border bg-card px-3 py-2 text-[13px] font-semibold min-h-[40px] disabled:opacity-50">Download CSV</button>
          <button type="button" onClick={() => window.print()} className="border border-border bg-card px-3 py-2 text-[13px] font-semibold min-h-[40px]">Print</button>
        </div>
      </div>

      {data?.isCurrentMonth && (
        <p className="border border-border bg-card px-4 py-3 text-[13px] text-muted-foreground">This month is not over yet, so comparisons with last month will look lower than they finally are. Pick the previous month for a complete picture.</p>
      )}

      <InsightSection question="Outcomes: what members did" loading={isLoading}
        answer={c && <><Figure>{c.participants.toLocaleString()}</Figure> {c.participants === 1 ? "member" : "members"} took part and <Figure>{c.newMembers.toLocaleString()}</Figure> joined{data?.isCurrentMonth ? " so far this month" : ""}.</>}>
        {c && p && (
          <div className="grid grid-cols-2 gap-x-6 gap-y-6 lg:grid-cols-4">
            <ChangeStat label="New members" current={c.newMembers} previous={p.newMembers} period="month" />
            <ChangeStat label="Members who took part" current={c.participants} previous={p.participants} period="month" />
            <ChangeStat label="Meaningful actions" current={c.meaningfulActions} previous={p.meaningfulActions} period="month" />
            <ChangeStat label="Event sign-ups" current={c.eventSignUps} previous={p.eventSignUps} period="month" />
          </div>
        )}
      </InsightSection>

      <InsightSection question="Money" loading={isLoading}
        answer={c && (c.amountCollected === 0 ? <>No payments were collected this month.</> : <>
          <Figure>{formatCurrency(c.amountCollected)}</Figure> was collected from <Figure>{c.contributors.toLocaleString()}</Figure> {c.contributors === 1 ? "member" : "members"}.
        </>)}>
        {c && p && (
          <>
            <div className="grid grid-cols-2 gap-x-6 gap-y-6">
              <Money label="Collected" now={c.amountCollected} before={p.amountCollected} />
              <ChangeStat label="Members who gave" current={c.contributors} previous={p.contributors} period="month" />
            </div>
            <p className="mt-4 text-[12.5px] text-muted-foreground">Successful payments only.</p>
          </>
        )}
      </InsightSection>

      <InsightSection question="Activity: what was done" loading={isLoading}
        answer={c && <>Published <Figure>{c.newsPublished}</Figure> news, held <Figure>{c.eventsHeld}</Figure> {c.eventsHeld === 1 ? "event" : "events"}, shared <Figure>{c.opportunitiesShared}</Figure> {c.opportunitiesShared === 1 ? "opportunity" : "opportunities"}, and finished <Figure>{c.suggestionsCompleted}</Figure> suggested {c.suggestionsCompleted === 1 ? "action" : "actions"}.</>}>
        {c && p && (
          <div className="grid grid-cols-2 gap-x-6 gap-y-6 lg:grid-cols-4">
            <ChangeStat label="News published" current={c.newsPublished} previous={p.newsPublished} period="month" />
            <ChangeStat label="Events held" current={c.eventsHeld} previous={p.eventsHeld} period="month" />
            <ChangeStat label="Opportunities shared" current={c.opportunitiesShared} previous={p.opportunitiesShared} period="month" />
            <ChangeStat label="Suggestions finished" current={c.suggestionsCompleted} previous={p.suggestionsCompleted} period="month" />
          </div>
        )}
      </InsightSection>

      <InsightSection question="Community health" loading={isLoading}
        answer={c && (c.healthReadings === 0
          ? <>No health reading was recorded this month.</>
          : <>The score went from <Figure>{c.healthStart}</Figure> to <Figure>{c.healthEnd}</Figure> across {c.healthReadings} {c.healthReadings === 1 ? "reading" : "readings"}.</>)}>
        {data && (
          <>
            <p className="text-[13px] text-muted-foreground">Now: {data.healthNow.score ?? "no score yet"}{data.healthNow.score !== undefined && data.healthNow.score !== null ? " out of 100" : ""}. The sections below describe the position today, not the end of {label}.</p>
            {data.needsAttention.length > 0 && (
              <ul className="mt-4 space-y-3">
                {data.needsAttention.map((f) => (
                  <li key={f.key}><p className="text-[14px] font-semibold">{f.label} <span className="font-normal text-muted-foreground">({f.score}/100)</span></p><p className="text-[12.5px] text-muted-foreground leading-relaxed">{f.detail}</p></li>
                ))}
              </ul>
            )}
          </>
        )}
      </InsightSection>

      {data && data.cohorts.length > 0 && (
        <InsightSection question="Year groups and ambassadors, today" loading={false}
          answer={<><Figure>{data.cohorts.filter((x) => x.ambassadors.length > 0).length}</Figure> of <Figure>{data.cohorts.length}</Figure> year groups have an ambassador.</>}>
          <ul className="divide-y divide-border text-[13.5px]">
            {data.cohorts.map((x) => (
              <li key={x.year} className="flex flex-wrap justify-between gap-x-4 py-2">
                <span><strong className="tabular-nums">{x.year}</strong> <span className="text-muted-foreground">{x.activeMembers} members, {x.newMembers} joined this period</span></span>
                <span className="text-muted-foreground">{x.ambassadors.length ? x.ambassadors.map((a) => a.name).join(", ") : "No ambassador"}</span>
              </li>
            ))}
          </ul>
        </InsightSection>
      )}

      {data && data.nextActions.length > 0 && (
        <InsightSection question="Recommended next actions" loading={false} answer={<>The most important things still open.</>}>
          <ul className="divide-y divide-border">
            {data.nextActions.map((a) => (
              <li key={a.id} className="py-2.5 text-[14px]"><Link href={a.actionUrl} className="font-semibold text-primary underline underline-offset-4">{a.title}</Link><span className="block text-[12.5px] text-muted-foreground">{a.explanation}</span></li>
            ))}
          </ul>
        </InsightSection>
      )}
    </div>
  );
}

function Money({ label, now, before }: { label: string; now: number; before: number }) {
  const diff = now - before;
  return (
    <div className="border-l-2 border-border pl-4">
      <p className="text-[22px] font-bold leading-none tabular-nums">{formatCurrency(now)}</p>
      <p className="mt-1.5 text-[14px]">{label}</p>
      <p className="mt-0.5 text-[12.5px] text-muted-foreground">{diff === 0 ? "Same as the month before" : `${formatCurrency(Math.abs(diff))} ${diff > 0 ? "more" : "less"} than the month before`}</p>
    </div>
  );
}
