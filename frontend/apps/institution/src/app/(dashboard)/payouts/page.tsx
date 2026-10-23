"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown, ChevronRight, Landmark } from "@alumni/ui";
import { Badge, Card, CardContent, CardSkeleton, EmptyState, FormSelect, LoadError } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { getPayoutDay, getPayoutForecast, getPayoutHistory, type PayoutDay } from "@/lib/institution-api";

const RANGES = [
  { value: "30", label: "Last 30 days" },
  { value: "90", label: "Last 3 months" },
  { value: "180", label: "Last 6 months" },
  { value: "365", label: "Last year" },
];

const weekday = (iso: string) => new Date(`${iso}T12:00:00`).toLocaleDateString("en-GB", { weekday: "long" });
const time = (iso: string) => new Date(iso).toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" });
const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? "" : "s"}`;

/** The payments inside one payout, loaded only when the day is opened. */
function DayDetail({ date }: { date: string }) {
  const { data, isLoading, isError } = useQuery({ queryKey: ["payout-day", date], queryFn: () => getPayoutDay(date) });
  if (isLoading) return <p className="px-4 py-3 text-[13px] text-muted-foreground">Loading the payments…</p>;
  if (isError || !data) return <p className="px-4 py-3 text-[13px] text-destructive">Could not load this payout. Try again.</p>;
  if (data.items.length === 0) return <p className="px-4 py-3 text-[13px] text-muted-foreground">No payments found for this day.</p>;
  return (
    <ul className="divide-y divide-border/60">
      {data.items.map((item, i) => (
        <li key={i} className="flex items-center gap-3 px-4 py-2.5 text-[13.5px]">
          <Badge variant="secondary" size="sm" className="shrink-0">{item.source}</Badge>
          <div className="min-w-0 flex-1">
            <p className="truncate font-medium">{item.title}</p>
            <p className="truncate text-[12.5px] text-muted-foreground">{item.payerName || "Member"} · {formatDate(item.confirmedAt)}, {time(item.confirmedAt)}</p>
          </div>
          <span className="shrink-0 font-semibold tabular-nums">{formatCurrency(item.amount)}</span>
        </li>
      ))}
    </ul>
  );
}

function DayRow({ day }: { day: PayoutDay }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="border-t border-border first:border-t-0">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        className="flex w-full items-center gap-3 px-4 py-3.5 text-left transition-colors hover:bg-muted/40"
      >
        {open ? <ChevronDown size={14} className="shrink-0 text-muted-foreground" /> : <ChevronRight size={14} className="shrink-0 text-muted-foreground" />}
        <div className="min-w-0 flex-1">
          <p className="text-[14.5px] font-semibold">{weekday(day.date)}, {formatDate(day.date)}</p>
          <p className="mt-0.5 flex flex-wrap gap-x-3 gap-y-0.5 text-[12.5px] text-muted-foreground">
            <span>{plural(day.transactionCount, "payment")}</span>
            {day.sources.map((s) => <span key={s.source}>{s.source} {formatCurrency(s.amount)}</span>)}
          </p>
        </div>
        <span className="shrink-0 text-[16px] font-bold tabular-nums">{formatCurrency(day.amount)}</span>
      </button>
      {open && <div className="border-t border-border/60 bg-muted/20"><DayDetail date={day.date} /></div>}
    </div>
  );
}

export default function PayoutsPage() {
  const [range, setRange] = useState("30");
  const forecast = useQuery({ queryKey: ["payout-forecast"], queryFn: getPayoutForecast, staleTime: 5 * 60 * 1000 });
  const history = useQuery({ queryKey: ["payout-history", range], queryFn: () => getPayoutHistory(Number(range)), placeholderData: (prev) => prev });
  const data = history.data;

  return (
    <div className="mx-auto max-w-[980px] space-y-6 p-4 sm:p-[26px]">
      <header className="space-y-1">
        <h1 className="m-0 text-[20px] font-bold sm:text-[25px]">Payouts</h1>
        <p className="max-w-[62ch] text-[13px] leading-relaxed text-muted-foreground">
          Paystack pays your online collections into your bank account every working morning. Each day below is one of those payouts,
          with what it was made of. Open a day to see the payments.
        </p>
      </header>

      {forecast.data && !forecast.data.payoutsConfigured && (
        <div className="border-l-2 border-primary bg-muted/40 py-3 pl-4 pr-3 text-[13px]">
          Your payout account isn&apos;t set up yet, so nothing is being paid out.{" "}
          <Link href="/settings" className="font-semibold text-accent hover:underline">Set up payouts</Link>
        </div>
      )}

      {forecast.data && forecast.data.payoutsConfigured && (
        <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2">
          <Card className="border-border/60"><CardContent className="p-[18px]">
            <p className="text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">Latest payout</p>
            <p className="mt-1.5 text-[26px] font-bold tabular-nums">{formatCurrency(forecast.data.lastPayout.amount)}</p>
            <p className="mt-1 text-[12.5px] text-muted-foreground">{formatDate(forecast.data.lastPayout.date)} · {plural(forecast.data.lastPayout.transactionCount, "payment")}</p>
          </CardContent></Card>
          <Card className="border-border/60"><CardContent className="p-[18px]">
            <p className="text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">Next payout</p>
            <p className="mt-1.5 text-[26px] font-bold tabular-nums">{formatCurrency(forecast.data.nextPayout.amount)}</p>
            <p className="mt-1 text-[12.5px] text-muted-foreground">Expected {formatDate(forecast.data.nextPayout.date)} morning · {plural(forecast.data.nextPayout.transactionCount, "payment")} so far</p>
          </CardContent></Card>
        </div>
      )}

      <section aria-labelledby="history-heading" className="space-y-3">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 id="history-heading" className="m-0 text-[16px] font-bold">Payout history</h2>
            {data && (
              <p className="mt-0.5 text-[13px] text-muted-foreground">
                {data.days.length === 0 ? "No payouts in this period." : <><span className="font-semibold text-foreground tabular-nums">{formatCurrency(data.totalAmount)}</span> paid out across {plural(data.days.length, "day")}</>}
              </p>
            )}
          </div>
          <FormSelect className="w-[180px]" value={range} onValueChange={setRange} options={RANGES} />
        </div>

        {history.isLoading ? (
          <CardSkeleton />
        ) : history.isError || !data ? (
          <LoadError title="Could not load your payouts" onRetry={() => history.refetch()} />
        ) : data.days.length === 0 ? (
          <EmptyState
            icon={<Landmark size={28} />}
            title="No payouts in this period"
            description={data.hasOlder ? "There are older payouts. Choose a longer period to see them." : "Once members pay online, each day's payout will be listed here."}
          />
        ) : (
          <>
            <Card className={cn("overflow-hidden border-border/60", history.isFetching && "opacity-70 transition-opacity")}>
              <CardContent className="p-0">{data.days.map((day) => <DayRow key={day.date} day={day} />)}</CardContent>
            </Card>
            {data.hasOlder && range !== "365" && <p className="text-[12.5px] text-muted-foreground">There are older payouts too. Choose a longer period to see them.</p>}
          </>
        )}
        <p className="text-[12px] text-muted-foreground">
          Worked out from your confirmed online payments, so it is an estimate. Your bank statement is the final record.
        </p>
      </section>
    </div>
  );
}
