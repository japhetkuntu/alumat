"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import {
  ChangeStat, Figure, InsightSection, LoadError, RankingBarChart, ShareBars, TrendChart,
  formatCurrency, monthLabel, percent,
} from "@alumni/ui";
import { getAnalytics } from "@/lib/institution-api";
import { useFeatures } from "@/hooks/use-institution-features";
import { useInstitutionNavTheme } from "@/components/institution/institution-layout";

const PRIMARY = "var(--brand-primary-500, var(--primary))";
const ACCENT = "var(--brand-accent-500, var(--brand-accent))";

/** "GH₵95K" — short enough for a chart axis, where a full amount with pesewas crowds out the chart. */
const compactCurrency = (value: number) =>
  new Intl.NumberFormat("en-GH", { style: "currency", currency: "GHS", notation: "compact", maximumFractionDigits: 1 }).format(value);

/** How many year groups the "who still owes" chart shows — the ones where a reminder would reach the most people. */
const OWING_SHOWN = 8;

export default function AnalyticsPage() {
  const features = useFeatures();
  const { data: navTheme } = useInstitutionNavTheme();
  const isCommunity = navTheme?.organizationType === "Community";
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["institution-analytics"],
    queryFn: getAnalytics,
    enabled: features.ready,
    staleTime: 5 * 60_000,
  });
  const loading = !features.ready || isLoading;

  const contributionsOn = features.enabled("Contributions");
  const eventsOn = features.enabled("Events");
  const forumOn = features.enabled("Forum");

  if (isError && !data) {
    return (
      <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto">
        <LoadError title="Analytics couldn’t load" onRetry={() => void refetch()} />
      </div>
    );
  }

  const members = data?.members;
  const growth = data?.growth ?? [];
  const thisMonth = growth.at(-1)?.count ?? 0;
  const lastMonth = growth.at(-2)?.count ?? 0;
  const joinedInYear = growth.reduce((sum, m) => sum + m.count, 0);

  const dues = data?.dues ?? null;
  const owing = (dues?.byYearGroup ?? [])
    .map((g) => ({ label: `Class of ${g.yearGroup}`, value: g.eligible - g.paid }))
    .filter((g) => g.value > 0)
    .sort((a, b) => b.value - a.value)
    .slice(0, OWING_SHOWN);

  const money = data?.money ?? null;
  const moneySeries = [
    contributionsOn && { key: "Contributions", label: "Fundraisers and dues", color: PRIMARY },
    features.enabled("Store") && { key: "Store", label: "Store", color: ACCENT },
    features.enabled("Services") && { key: "Services", label: "Services", color: "var(--chart-3, #f59e0b)" },
  ].filter((s): s is { key: string; label: string; color: string } => Boolean(s));
  const moneyMonths: Record<string, string | number>[] = (money?.months ?? []).map((m) => ({
    month: monthLabel(m.year, m.month), Contributions: m.contributions, Store: m.store, Services: m.services,
  }));
  const moneyDifference = money ? money.thisYear - money.lastYearToDate : 0;

  const composition = data?.composition;
  const largestYearGroup = [...(composition?.byYearGroup ?? [])].sort((a, b) => b.count - a.count)[0];
  const topLocation = composition?.byLocation[0];
  const topDepartment = composition?.byDepartment[0];

  const activity = data?.activity;

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-4">
      <div>
        <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Analytics</h1>
        <p className="text-muted-foreground text-[13px] mt-1.5">
          The questions worth asking about your community, each answered from your own data. Figures refresh every few minutes.
        </p>
      </div>

      <InsightSection
        question="What happened in the last 30 days?"
        loading={loading}
        answer={activity && (
          <>
            <Figure>{activity.newMembers.last30Days.toLocaleString()}</Figure> {activity.newMembers.last30Days === 1 ? "person" : "people"} joined
            {contributionsOn && <>, <Figure>{activity.payments.last30Days.toLocaleString()}</Figure> {activity.payments.last30Days === 1 ? "payment" : "payments"} came in</>}
            {members && <>, and <Figure>{members.signedInLast30Days.toLocaleString()}</Figure> {members.signedInLast30Days === 1 ? "member" : "members"} signed in</>}.
          </>
        )}
      >
        {activity && (
          <div className="grid grid-cols-2 gap-x-6 gap-y-6 lg:grid-cols-4">
            <ChangeStat label="New members" current={activity.newMembers.last30Days} previous={activity.newMembers.previous30Days} />
            {contributionsOn && <ChangeStat label="Payments received" current={activity.payments.last30Days} previous={activity.payments.previous30Days} />}
            {eventsOn && <ChangeStat label="Event sign-ups" current={activity.eventSignUps.last30Days} previous={activity.eventSignUps.previous30Days} />}
            {forumOn && <ChangeStat label="Forum posts and replies" current={activity.forumPosts.last30Days} previous={activity.forumPosts.previous30Days} />}
          </div>
        )}
      </InsightSection>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <InsightSection
          question="Is the community growing?"
          loading={loading}
          answer={members && (
            <>
              You have <Figure>{members.total.toLocaleString()}</Figure> members. <Figure>{thisMonth.toLocaleString()}</Figure> joined this month
              {lastMonth !== thisMonth ? <>, {thisMonth > lastMonth ? "up" : "down"} from {lastMonth.toLocaleString()} last month</> : <>, the same as last month</>}
              , and {joinedInYear.toLocaleString()} over the last 12 months.
            </>
          )}
        >
          <TrendChart
            data={growth.map((m) => ({ month: monthLabel(m.year, m.month), Joined: m.count }))}
            xKey="month"
            series={[{ key: "Joined", label: "Members who joined", color: PRIMARY }]}
            variant="bar"
            height={190}
            loading={loading}
            emptyMessage="Nobody has joined in the last 12 months"
            valueFormatter={(v) => v.toLocaleString()}
          />
        </InsightSection>

        <InsightSection
          question="Are members actually showing up?"
          loading={loading}
          action={members && members.pending > 0
            ? <Link href="/members" className="font-semibold text-primary underline underline-offset-4">{members.pending.toLocaleString()} waiting for approval</Link>
            : undefined}
          answer={members && (
            members.approved === 0
              ? <>No members have been approved yet, so there is nobody to measure.</>
              : <>
                  <Figure>{percent(members.signedInEver, members.approved)}</Figure> of approved members have signed in at least once, and{" "}
                  <Figure>{percent(members.signedInLast30Days, members.approved)}</Figure> came back in the last 30 days.
                </>
          )}
        >
          {members && members.approved > 0 && (
            <ShareBars items={[
              { label: "Approved members", value: members.approved },
              { label: "Signed in at least once", value: members.signedInEver },
              { label: "Signed in during the last 30 days", value: members.signedInLast30Days },
            ]} />
          )}
        </InsightSection>
      </div>

      {contributionsOn && (
        <InsightSection
          question={`Are ${dues?.year ?? new Date().getFullYear()} dues being paid?`}
          loading={loading}
          action={dues && <Link href="/reports" className="font-semibold text-primary underline underline-offset-4">Get the list of who owes</Link>}
          answer={dues
            ? dues.eligible === 0
              ? <>Dues are set for {dues.year}, but no approved member is due to pay them yet.</>
              : <>
                  <Figure>{dues.paid.toLocaleString()}</Figure> of <Figure>{dues.eligible.toLocaleString()}</Figure> members have paid
                  (<Figure>{percent(dues.paid, dues.eligible)}</Figure>), bringing in <Figure>{formatCurrency(dues.collected)}</Figure>.{" "}
                  {dues.eligible - dues.paid > 0
                    ? <>{(dues.eligible - dues.paid).toLocaleString()} still owe.</>
                    : <>Everyone has paid.</>}
                </>
            : <>No dues have been set for {new Date().getFullYear()}, so there is nothing to collect or to owe.</>}
        >
          {dues && dues.eligible > 0 && (
            <div className={owing.length > 0 && !isCommunity ? "grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]" : undefined}>
              <ShareBars color={ACCENT} items={[
                { label: "Due to pay", value: dues.eligible },
                { label: "Paid", value: dues.paid },
              ]} />
              {owing.length > 0 && !isCommunity && (
                <div>
                  <p className="mb-2 text-[13px] text-muted-foreground">Year groups with the most members still owing: where a reminder reaches the most people</p>
                  <RankingBarChart
                    data={owing}
                    color={ACCENT}
                    orientation="horizontal"
                    height={Math.max(120, owing.length * 34)}
                    valueFormatter={(v) => `${v.toLocaleString()} owing`}
                  />
                </div>
              )}
            </div>
          )}
        </InsightSection>
      )}

      {(loading || money) && moneySeries.length > 0 && (
        <InsightSection
          question="Where is the money coming from?"
          loading={loading}
          answer={money && (
            <>
              <Figure>{formatCurrency(money.thisYear)}</Figure> received so far this year
              {contributionsOn && money.payersThisYear > 0 && <> from <Figure>{money.payersThisYear.toLocaleString()}</Figure> {money.payersThisYear === 1 ? "member" : "members"}</>}
              {money.lastYearToDate > 0
                ? <>: {formatCurrency(Math.abs(moneyDifference))} {moneyDifference >= 0 ? "more" : "less"} than by this date last year ({formatCurrency(money.lastYearToDate)}).</>
                : <>. Nothing had come in by this date last year to compare it with.</>}
            </>
          )}
        >
          <TrendChart
            data={moneyMonths}
            xKey="month"
            series={moneySeries}
            variant="bar"
            stacked
            height={220}
            loading={loading}
            emptyMessage="No payments in the last 12 months"
            valueFormatter={compactCurrency}
          />
        </InsightSection>
      )}

      <InsightSection
        question="Who are our members?"
        loading={loading}
        answer={composition && (
          members?.approved === 0
            ? <>There are no approved members to describe yet.</>
            : <>
                {!isCommunity && largestYearGroup && <>The largest year group is the class of <Figure>{largestYearGroup.label}</Figure> ({largestYearGroup.count.toLocaleString()}). </>}
                {topDepartment && <>Most belong to <Figure>{topDepartment.label}</Figure> ({topDepartment.count.toLocaleString()}). </>}
                {topLocation
                  ? <>More are in <Figure>{topLocation.label}</Figure> than anywhere else ({topLocation.count.toLocaleString()})</>
                  : <>Nobody has said where they are based</>}
                {composition.withoutLocation > 0 && topLocation && <>; {composition.withoutLocation.toLocaleString()} haven’t said where they are</>}.
              </>
        )}
      >
        {composition && (members?.approved ?? 0) > 0 && (
          <div className="grid grid-cols-1 gap-8 lg:grid-cols-3">
            {!isCommunity && (
              <div>
                <p className="mb-2 text-[13px] text-muted-foreground">By graduation year</p>
                <RankingBarChart
                  data={composition.byYearGroup.map((s) => ({ label: s.label, value: s.count }))}
                  color={PRIMARY}
                  height={260}
                  emptyMessage="No graduation years recorded"
                  valueFormatter={(v) => v.toLocaleString()}
                />
              </div>
            )}
            <div>
              <p className="mb-2 text-[13px] text-muted-foreground">Largest departments</p>
              <RankingBarChart
                data={composition.byDepartment.map((s) => ({ label: s.label, value: s.count }))}
                color={PRIMARY}
                orientation="horizontal"
                height={220}
                emptyMessage="No departments recorded"
                valueFormatter={(v) => `${v.toLocaleString()} members`}
              />
            </div>
            <div>
              <p className="mb-2 text-[13px] text-muted-foreground">Where they are based</p>
              <RankingBarChart
                data={composition.byLocation.map((s) => ({ label: s.label, value: s.count }))}
                color={ACCENT}
                orientation="horizontal"
                height={220}
                emptyMessage="No member has added a location yet"
                valueFormatter={(v) => `${v.toLocaleString()} members`}
              />
            </div>
          </div>
        )}
      </InsightSection>
    </div>
  );
}
