"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import {
  Figure, InsightSection, LoadError, RankingBarChart, ShareBars, TrendChart,
  formatCurrency, formatDate, monthLabel, percent,
} from "@alumni/ui";
import { getPlatformAnalytics } from "@/lib/platform-api";
import { PageHeading } from "@/components/platform/page-heading";

const PRIMARY = "var(--brand-primary-500, var(--primary))";

/** "GH₵184K" — short enough for a chart axis, where a full amount with pesewas crowds out the chart. */
const compactCurrency = (value: number) =>
  new Intl.NumberFormat("en-GH", { style: "currency", currency: "GHS", notation: "compact", maximumFractionDigits: 1 }).format(value);

export default function PlatformAnalyticsPage() {
  const { data, isLoading: loading, isError, refetch } = useQuery({
    queryKey: ["platform-analytics"],
    queryFn: getPlatformAnalytics,
    staleTime: 5 * 60_000,
  });

  if (isError && !data) {
    return (
      <div className="p-4 sm:p-7 max-w-[1240px]">
        <LoadError title="Analytics couldn’t load" onRetry={() => void refetch()} />
      </div>
    );
  }

  const institutions = data?.institutions;
  const members = data?.members;
  const institutionGrowth = data?.institutionGrowth ?? [];
  const memberGrowth = data?.memberGrowth ?? [];
  const onboardedInYear = institutionGrowth.reduce((sum, m) => sum + m.count, 0);
  const joinedThisMonth = memberGrowth.at(-1)?.count ?? 0;
  const joinedLastMonth = memberGrowth.at(-2)?.count ?? 0;

  const money = data?.money ?? null;
  const collectedDifference = money ? money.thisYearCollected - money.lastYearToDateCollected : 0;
  const leaders = data?.leaders ?? [];
  const leadersTotal = leaders.reduce((sum, l) => sum + l.collected, 0);
  const quiet = data?.quiet ?? [];

  return (
    <div className="p-4 sm:p-7 max-w-[1240px]">
      <PageHeading
        title="Analytics"
        description="The questions worth asking about the platform, each answered from live data across every institution."
      />

      <div className="space-y-4">
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <InsightSection
            question="Are the institutions we signed alive?"
            loading={loading}
            action={<Link href="/activation" className="font-semibold text-primary underline underline-offset-4">Activation scorecard</Link>}
            answer={institutions && (
              institutions.total === 0
                ? <>No institutions have been onboarded yet.</>
                : <>
                    Of <Figure>{institutions.total.toLocaleString()}</Figure> institutions, <Figure>{institutions.withRecentSignIns.toLocaleString()}</Figure> had
                    a member sign in during the last 30 days and <Figure>{institutions.collecting.toLocaleString()}</Figure> took a payment in the last 90.
                  </>
            )}
          >
            {institutions && institutions.total > 0 && (
              <ShareBars items={[
                { label: "Onboarded", value: institutions.total },
                { label: "Activated", value: institutions.activated },
                { label: "A member signed in", value: institutions.withRecentSignIns, note: "last 30 days" },
                { label: "Took a payment", value: institutions.collecting, note: "last 90 days" },
              ]} />
            )}
          </InsightSection>

          <InsightSection
            question="Are we signing new institutions?"
            loading={loading}
            answer={institutions && (
              <>
                <Figure>{onboardedInYear.toLocaleString()}</Figure> {onboardedInYear === 1 ? "institution" : "institutions"} onboarded in the last 12 months,{" "}
                <Figure>{(institutionGrowth.at(-1)?.count ?? 0).toLocaleString()}</Figure> of them this month.
              </>
            )}
          >
            <TrendChart
              data={institutionGrowth.map((m) => ({ month: monthLabel(m.year, m.month), Onboarded: m.count }))}
              xKey="month"
              series={[{ key: "Onboarded", label: "Institutions onboarded", color: PRIMARY }]}
              variant="bar"
              height={190}
              loading={loading}
              emptyMessage="No institution has been onboarded in the last 12 months"
              valueFormatter={(v) => v.toLocaleString()}
            />
          </InsightSection>
        </div>

        <InsightSection
          question="Are their members joining and coming back?"
          loading={loading}
          answer={members && (
            <>
              <Figure>{members.total.toLocaleString()}</Figure> members across the platform. <Figure>{joinedThisMonth.toLocaleString()}</Figure> joined this month
              {joinedLastMonth !== joinedThisMonth ? <>, {joinedThisMonth > joinedLastMonth ? "up" : "down"} from {joinedLastMonth.toLocaleString()} last month</> : <>, the same as last month</>}
              , and <Figure>{percent(members.signedInLast30Days, members.approved)}</Figure> of approved members signed in during the last 30 days.
            </>
          )}
        >
          <TrendChart
            data={memberGrowth.map((m) => ({ month: monthLabel(m.year, m.month), Members: m.total }))}
            xKey="month"
            series={[{ key: "Members", label: "Members on the platform", color: PRIMARY }]}
            variant="area"
            height={200}
            loading={loading}
            emptyMessage="No members yet"
            valueFormatter={(v) => v.toLocaleString()}
          />
        </InsightSection>

        {(loading || money) && (
          <InsightSection
            question="Is money moving, and what do we earn from it?"
            loading={loading}
            action={<Link href="/reports" className="font-semibold text-primary underline underline-offset-4">Revenue by institution</Link>}
            answer={money && (
              <>
                Institutions have collected <Figure>{formatCurrency(money.thisYearCollected)}</Figure> so far this year and we earned{" "}
                <Figure>{formatCurrency(money.thisYearEarned)}</Figure> on it
                {money.thisYearCollected > 0 && <> ({((money.thisYearEarned / money.thisYearCollected) * 100).toFixed(1)}%)</>}
                {money.lastYearToDateCollected > 0
                  ? <>. That is {formatCurrency(Math.abs(collectedDifference))} {collectedDifference >= 0 ? "more" : "less"} collected than by this date last year.</>
                  : <>. Nothing had been collected by this date last year to compare it with.</>}
              </>
            )}
          >
            <TrendChart
              // Collected only: our earnings are a couple of percent of it, so on a shared axis they are a flat
              // line along the bottom that says nothing. The sentence above carries the earnings figure.
              data={(money?.months ?? []).map((m) => ({ month: monthLabel(m.year, m.month), Collected: m.collected }))}
              xKey="month"
              series={[{ key: "Collected", label: "Collected by institutions", color: PRIMARY }]}
              variant="bar"
              height={220}
              loading={loading}
              emptyMessage="No payments in the last 12 months"
              valueFormatter={compactCurrency}
            />
          </InsightSection>
        )}

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          {(loading || money) && (
            <InsightSection
              question="Who is carrying the platform?"
              loading={loading}
              answer={leaders.length === 0
                ? <>No institution has taken a payment in the last 90 days.</>
                : <>
                    <Figure>{leaders[0].name}</Figure> collected the most in the last 90 days: <Figure>{formatCurrency(leaders[0].collected)}</Figure>
                    {leaders.length > 1 && <>, {percent(leaders[0].collected, leadersTotal)} of what the top {leaders.length} took together</>}.
                  </>}
            >
              {leaders.length > 0 && (
                <RankingBarChart
                  data={leaders.map((l) => ({ label: l.name, value: l.collected }))}
                  color={PRIMARY}
                  orientation="horizontal"
                  height={Math.max(120, leaders.length * 34)}
                  valueFormatter={compactCurrency}
                />
              )}
            </InsightSection>
          )}

          <InsightSection
            question="Who has gone quiet?"
            loading={loading}
            answer={quiet.length === 0
              ? <>Every institution with members has had someone sign in during the last 30 days.</>
              : <>
                  <Figure>{quiet.length.toLocaleString()}</Figure> {quiet.length === 1 ? "institution has" : "institutions have"} members but nobody has signed in for 30 days.
                  These are the ones worth a call.
                </>}
          >
            {quiet.length > 0 && (
              <ul className="divide-y divide-border border-t border-border">
                {quiet.map((q) => (
                  <li key={q.institutionId} className="py-3">
                    <Link href={`/institutions/${q.institutionId}`} className="text-[14px] font-semibold text-foreground hover:text-primary">{q.name}</Link>
                    <p className="mt-0.5 text-[13px] text-muted-foreground">
                      {q.members.toLocaleString()} {q.members === 1 ? "member" : "members"} ·{" "}
                      {q.lastSignIn ? `Last sign-in ${formatDate(q.lastSignIn)}` : "Nobody has ever signed in"} ·{" "}
                      {q.lastPayment ? `Last payment ${formatDate(q.lastPayment)}` : "No payments yet"}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </InsightSection>
        </div>
      </div>
    </div>
  );
}
