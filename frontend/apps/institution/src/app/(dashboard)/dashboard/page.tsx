"use client";

import { useQueries, useQuery } from "@tanstack/react-query";
import { Button } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Badge } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import { StatCard, StatCardSkeleton } from "@alumni/ui";
import { TrendChart, DonutChart } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { getCampaigns, getContributions, getMembers, getEvents, getJobs, getBatches, getStoreOrders, getServiceRequests, getPayoutForecast, getRevenueTrend } from "@/lib/institution-api";
import { useAuth } from "@/hooks/use-auth";
import { InviteKitCard } from "@/components/institution/invite-kit-card";
import { useFeatures } from "@/hooks/use-institution-features";

const STATUS_COLORS: Record<string, string> = {
  Successful: "var(--success, #16a34a)",
  Pending: "var(--warning, #d97706)",
  Failed: "var(--destructive, #dc2626)",
  Rejected: "var(--destructive, #dc2626)",
};

// Paystack settles our subaccount split straight to the institution's own
// bank account at the start of every working day — SuperAdmins are the ones
// who reconcile that account, so this is the one dashboard panel scoped to
// them alone, not every Admin.
function PayoutPanel() {
  const { data, isLoading } = useQuery({ queryKey: ["payout-forecast"], queryFn: getPayoutForecast, staleTime: 5 * 60 * 1000 });

  if (isLoading) {
    return (
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5 mt-3.5">
        {Array.from({ length: 2 }).map((_, i) => <div key={i} className="card p-[18px] h-[118px] skeleton" />)}
      </div>
    );
  }
  if (!data) return null;

  if (!data.payoutsConfigured) {
    return (
      <div className="card p-[18px] mt-3.5">
        <p className="text-[13px] text-muted-foreground">
          Settlement banking isn&apos;t set up yet.{" "}
          <Link href="/settings" className="text-accent font-semibold hover:underline">Set up payouts</Link> to start seeing expected payouts here.
        </p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5 mt-3.5">
      <div className="card p-[18px] min-w-0">
        <p className="text-[13px] font-semibold mb-3">Last payout</p>
        <p
          className="font-[family-name:var(--font-display)] font-bold tabular-nums text-foreground"
          style={{ fontSize: "clamp(1.1rem, 3vw, 1.6rem)", letterSpacing: "-0.02em", wordBreak: "break-word", overflowWrap: "anywhere" }}
        >
          {formatCurrency(data.lastPayout.amount)}
        </p>
        <p className="text-[12px] text-muted-foreground mt-1.5">
          Settled {formatDate(data.lastPayout.date)} &middot; should already be in your account &middot; {data.lastPayout.transactionCount} transaction{data.lastPayout.transactionCount === 1 ? "" : "s"}
        </p>
      </div>
      <div className="card p-[18px] min-w-0" style={{ borderColor: "var(--border-emphasis)" }}>
        <p className="text-[13px] font-semibold mb-3">Next payout</p>
        <p
          className="font-[family-name:var(--font-display)] font-bold tabular-nums text-foreground"
          style={{ fontSize: "clamp(1.1rem, 3vw, 1.6rem)", letterSpacing: "-0.02em", wordBreak: "break-word", overflowWrap: "anywhere" }}
        >
          {formatCurrency(data.nextPayout.amount)}
        </p>
        <p className="text-[12px] text-muted-foreground mt-1.5">
          Expected {formatDate(data.nextPayout.date)} morning &middot; still accumulating &middot; {data.nextPayout.transactionCount} transaction{data.nextPayout.transactionCount === 1 ? "" : "s"}
        </p>
      </div>
      <p className="sm:col-span-2 text-[12px] text-muted-foreground -mt-2">Estimated from your confirmed transactions, not a bank-confirmed figure.</p>
    </div>
  );
}

export default function AdminDashboardPage() {
  const router = useRouter();
  const { user, isScopedAdmin } = useAuth();
  // A switched-off feature's API answers 403, so its data is never requested and its
  // tiles/sections are never rendered: the dashboard is simply built from what this institution uses.
  const features = useFeatures();
  const contributionsOn = features.enabled("Contributions");
  const eventsOn = features.enabled("Events");
  const jobsOn = features.enabled("Jobs");
  const storeOn = features.enabled("Store");
  const servicesOn = features.enabled("Services");
  const moneyOn = contributionsOn || storeOn || servicesOn;
  const results = useQueries({
    queries: [
      { queryKey: ["dash-members-total"], queryFn: () => getMembers({ pageSize: 1 }) },
      { queryKey: ["dash-members-pending"], queryFn: () => getMembers({ pageSize: 20, status: "Pending" }) },
      { queryKey: ["dash-campaigns"], enabled: contributionsOn, queryFn: () => getCampaigns(1, 100) },
      { queryKey: ["dash-contributions"], enabled: contributionsOn, queryFn: () => getContributions({ pageSize: 500 }) },
      { queryKey: ["dash-events"], enabled: eventsOn, queryFn: () => getEvents(1, 1) },
      { queryKey: ["dash-jobs"], enabled: jobsOn, queryFn: () => getJobs(1, 1) },
      { queryKey: ["dash-batches"], queryFn: getBatches },
      { queryKey: ["dash-store-orders"], enabled: storeOn, queryFn: () => getStoreOrders(1, 500) },
      { queryKey: ["dash-service-requests"], enabled: servicesOn, queryFn: () => getServiceRequests(1, 500) },
      { queryKey: ["dash-revenue-trend"], enabled: moneyOn, queryFn: () => getRevenueTrend(6) },
    ],
  });

  const [membersTotal, membersPending, campaigns, contributions, events, jobs, batches, storeOrders, serviceRequests, revenueTrend] = results;
  const isLoading = !features.ready || results.some((r) => r.isLoading);

  const totalMembers = membersTotal.data?.totalCount ?? 0;
  const pendingApprovals = membersPending.data?.totalCount ?? 0;
  const now = new Date();
  const allCampaigns = campaigns.data?.results ?? [];
  const activeCampaigns = allCampaigns.filter((c) => c.status === "Active");
  const approachingDeadline = activeCampaigns.filter((c) => {
    const days = (new Date(c.deadline).getTime() - now.getTime()) / 86400000;
    return days >= 0 && days <= 14;
  }).length;
  const allContributions = contributions.data?.results ?? [];
  const recentContributions = allContributions.slice(0, 5);
  const totalContributions = contributions.data?.totalCount ?? 0;
  const upcomingEvents = events.data?.totalCount ?? 0;
  const openJobs = jobs.data?.totalCount ?? 0;

  const allStoreOrders = storeOrders.data?.results ?? [];
  const allServiceRequests = serviceRequests.data?.results ?? [];

  const totalAmountCollected = allCampaigns.reduce((sum, c) => sum + c.collectedAmount, 0)
    + allStoreOrders.filter((o) => o.status === "Successful").reduce((sum, o) => sum + o.totalAmount, 0)
    + allServiceRequests.filter((r) => r.paymentStatus === "Successful").reduce((sum, r) => sum + r.amount, 0);

  // The chart shows the latest six calendar months, totalled by the server (so it stays exact at any volume).
  // Older history is on the Reports page.
  const monthNames = ["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];
  const trendMonths: Record<string, string | number>[] = (revenueTrend.data?.months ?? []).map((m) => ({
    month: monthNames[m.month - 1],
    ...(contributionsOn && { Contributions: m.contributions }),
    ...(storeOn && { Store: m.store }),
    ...(servicesOn && { Services: m.services }),
  }));
  const trendSeries = [
    contributionsOn && { key: "Contributions", label: "Contributions", color: "var(--brand-primary-500, var(--primary))" },
    storeOn && { key: "Store", label: "Store", color: "var(--brand-accent-500, var(--brand-accent))" },
    servicesOn && { key: "Services", label: "Services", color: "var(--chart-3, #f59e0b)" },
  ].filter((x): x is { key: string; label: string; color: string } => Boolean(x));
  const trendSources = trendSeries.map((x) => x.label).join(" + ");
  const statusCounts = revenueTrend.data?.statusCounts ?? {};
  const statusPieData = Object.entries(statusCounts).map(([status, count]) => ({
    label: status,
    value: count,
    color: STATUS_COLORS[status] ?? "var(--muted-foreground)",
  }));
  const totalPayments = statusPieData.reduce((sum, d) => sum + d.value, 0);

  const firstName = user?.name?.trim()?.split(" ")[0] || "";
  const greeting = firstName ? `Good morning, ${firstName}` : "Welcome back";
  const todayLabel = new Intl.DateTimeFormat("en-GB", { weekday: "long", day: "numeric", month: "long", year: "numeric" }).format(now);

  return (
    <div className="p-4 sm:p-[26px] pt-[26px] max-w-[1240px] mx-auto">
      <div className="flex items-end justify-between gap-4 mb-5 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">{greeting}</h1>
          <p className="mt-1.5 text-muted-foreground text-[13px]">{todayLabel} &middot; Institution operations overview</p>
        </div>
        <span className="shrink-0 whitespace-nowrap px-2.5 py-2 rounded-none text-[12px] font-bold" style={{ background: "var(--brand-primary-light)", color: "var(--brand-primary-700, var(--color-text-info))" }}>
          All institution records
        </span>
      </div>

      {(() => {
        // Supporting tiles: only for features that are on. With money off there is no hero,
        // so these simply fill the row on their own.
        const supporting = [
          <Link key="members" href="/members" className="block h-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary">
            <StatCard
              tone="primary"
              label="Total members"
              value={totalMembers.toLocaleString()}
              sub={
                <span style={{ color: "var(--success)" }}>
                  +{Math.max(0, Math.round(totalMembers * 0.02))} this period &middot;{" "}
                  <span className="underline">{pendingApprovals} pending</span>
                </span>
              }
            />
          </Link>,
          contributionsOn && (
            <Link key="campaigns" href="/campaigns" className="block h-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary">
              <StatCard
                tone="accent"
                label="Active fundraisers & dues"
                value={activeCampaigns.length}
                sub={
                  <span style={{ color: approachingDeadline > 0 ? "var(--warning)" : undefined }}>
                    {approachingDeadline} approaching deadline
                  </span>
                }
              />
            </Link>
          ),
          eventsOn && (
            <Link key="events" href="/events" className="block h-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary">
              <StatCard
                tone="accent"
                label="Upcoming events"
                value={upcomingEvents}
                sub={jobsOn ? `+ ${openJobs} open job postings` : undefined}
              />
            </Link>
          ),
          !eventsOn && jobsOn && (
            <Link key="jobs" href="/jobs" className="block h-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary">
              <StatCard tone="accent" label="Open job postings" value={openJobs} />
            </Link>
          ),
        ].filter(Boolean);
        const cols = ["", "sm:grid-cols-1", "sm:grid-cols-2", "sm:grid-cols-3"][Math.min(supporting.length, 3)];
        const expectedSupporting = 1 + (contributionsOn ? 1 : 0) + (eventsOn || jobsOn ? 1 : 0);
        const heroHref = contributionsOn ? "/contributions" : storeOn ? "/store" : "/services";

        if (isLoading) {
          return (
            <div className={`grid grid-cols-1 ${moneyOn || !features.ready ? "lg:grid-cols-[minmax(280px,1.3fr)_2fr]" : ""} gap-3.5 items-stretch`}>
              {(moneyOn || !features.ready) && <StatCardSkeleton variant="hero" />}
              <div className={`grid grid-cols-2 ${["", "sm:grid-cols-1", "sm:grid-cols-2", "sm:grid-cols-3"][Math.min(features.ready ? expectedSupporting : 3, 3)]} gap-3 max-sm:[&>*:last-child:nth-child(odd)]:col-span-2 [&>a>*]:h-full`}>
                {Array.from({ length: features.ready ? expectedSupporting : 3 }).map((_, i) => <StatCardSkeleton key={i} />)}
              </div>
            </div>
          );
        }
        // Money leads when the institution takes payments — one dominant "total collected"
        // figure with the other metrics demoted to a supporting row.
        return (
          <div className={`grid grid-cols-1 ${moneyOn ? "lg:grid-cols-[minmax(280px,1.3fr)_2fr]" : ""} gap-3.5 items-stretch`}>
            {moneyOn && (
              <Link href={heroHref} className="block h-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary">
                <StatCard
                  tone="primary"
                  variant="hero"
                  label="Total collected"
                  value={formatCurrency(totalAmountCollected)}
                  sub={contributionsOn ? <span style={{ color: "var(--success)" }}>{totalContributions} contributions</span> : undefined}
                />
              </Link>
            )}
            <div className={`grid grid-cols-2 ${cols} gap-3 max-sm:[&>*:last-child:nth-child(odd)]:col-span-2 [&>a>*]:h-full`}>{supporting}</div>
          </div>
        );
      })()}

      {!isLoading && (
        <InviteKitCard totalMembers={totalMembers} upcomingEvents={upcomingEvents} activeCampaignCount={activeCampaigns.length} />
      )}

      {user?.role === "SuperAdmin" && moneyOn && <PayoutPanel />}

      {moneyOn && <div className="grid grid-cols-1 lg:grid-cols-[1.2fr_.8fr] gap-3.5 mt-3.5">
        <section className="card p-[18px]">
          <h2 className="text-[15px] font-semibold m-0 mb-3.5 flex items-baseline justify-between gap-3">
            <span>
              Revenue trend <span className="text-muted-foreground font-normal text-[13px]">Last 6 months, {trendSources}</span>
            </span>
            <Link href="/analytics" className="inline-block -my-2.5 shrink-0 py-2.5 text-[13px] font-normal text-muted-foreground hover:text-accent">Full analytics &rarr;</Link>
          </h2>
          <TrendChart
            data={trendMonths}
            xKey="month"
            series={trendSeries}
            variant="bar"
            stacked
            height={150}
            loading={isLoading}
            emptyMessage="No payments recorded yet this period"
            valueFormatter={(v) => formatCurrency(v)}
          />
        </section>

        <section className="card p-[18px]">
          <h2 className="text-[15px] font-semibold m-0 mb-3.5">Payment status mix</h2>
          <DonutChart
            data={statusPieData}
            centerValue={totalPayments || undefined}
            centerLabel="payments"
            height={150}
            loading={isLoading}
            emptyMessage="No payments yet"
            valueFormatter={(v) => v.toLocaleString()}
          />
        </section>
      </div>}

      <div className="grid grid-cols-1 gap-3.5 mt-3.5">
        {/* Emphasis border — this card asks for action, the chart cards above only inform */}
        <section className="card p-[18px]" style={{ borderColor: pendingApprovals > 0 ? "var(--border-emphasis)" : undefined }}>
          <h2 className="text-[15px] font-semibold m-0 mb-3.5 flex items-center justify-between">
            Pending approvals
            <Link href="/members" className="inline-block -my-2.5 py-2.5 text-[13px] font-normal text-muted-foreground hover:text-accent">View queue &rarr;</Link>
          </h2>
          {isLoading ? (
            <div className="space-y-3 py-1">
              {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-10" />)}
            </div>
          ) : (
            <>
              {(membersPending.data?.results ?? []).slice(0, 3).map((m) => (
                <Link key={m.id} href={`/members/${m.id}`} className="flex items-center justify-between gap-2.5 py-3 border-t border-border first:border-0 transition-colors hover:bg-muted/40">
                  <div className="min-w-0">
                    <b className="text-[13px]">{m.firstName} {m.lastName}</b>
                    <br />
                    <small className="text-muted-foreground text-[12px]">
                      {m.graduationYear ? `Cohort ${m.graduationYear}` : "Cohort unknown"} &middot; {m.isEmailVerified ? "email verified" : "email unverified"}
                    </small>
                  </div>
                  <Badge variant="warning">Pending</Badge>
                </Link>
              ))}
              {pendingApprovals === 0 && (
                <p className="text-[13px] text-muted-foreground text-center py-6">No pending approvals</p>
              )}
            </>
          )}
        </section>
      </div>

      {contributionsOn && <div className="grid grid-cols-1 lg:grid-cols-[1.2fr_.8fr] gap-3.5 mt-3.5">
        <section className="card p-[18px]">
          <h2 className="text-[15px] font-semibold m-0 mb-3.5">Active fundraisers &amp; dues</h2>
          {isLoading ? (
            <div className="space-y-3 py-1">
              {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-14" />)}
            </div>
          ) : (
            <>
              {activeCampaigns.length === 0 && <p className="text-[13px] text-muted-foreground py-4">No active fundraisers or dues.</p>}
              {activeCampaigns.map((c) => {
                const isMembership = !!c.isMembershipCampaign;
                const pct = isMembership && c.totalEligibleMembers
                  ? Math.round((c.paidCount / c.totalEligibleMembers) * 100)
                  : c.targetAmount > 0 ? Math.round((c.collectedAmount / c.targetAmount) * 100) : 0;
                return (
                  <Link key={c.id} href={`/campaigns/${c.id}`} className="flex items-start justify-between gap-3 py-3 border-t border-border first:border-0 transition-colors hover:bg-muted/40">
                    <div className="min-w-0 flex-1">
                      <b className="text-[13px] break-words">{c.title}</b>
                      <br />
                      <small className="text-muted-foreground text-[12px]">
                        Deadline {formatDate(c.deadline)} &middot; {c.yearGroups?.length ? `Cohorts ${c.yearGroups.join(", ")}` : "All members"}
                      </small>
                      <Progress value={pct} className="h-[7px] mt-2" />
                    </div>
                    <b className="text-[13px] shrink-0 tabular-nums">{pct}%</b>
                  </Link>
                );
              })}
            </>
          )}
        </section>

        <section className="card overflow-hidden">
          <h2 className="text-[15px] font-semibold m-0 p-[18px] pb-0">Recent contributions</h2>
          {isLoading ? (
            <div className="space-y-3 p-[18px]">
              {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-8" />)}
            </div>
          ) : recentContributions.length === 0 ? (
            <EmptyState className="py-8" title="No contributions yet" description="Payments appear here as soon as members contribute to a fundraiser." action={<Link href="/campaigns"><Button size="sm" className="font-semibold">Create a fundraiser</Button></Link>} />
          ) : (
            <table className="w-full text-[13px] mt-2">
              <thead>
                <tr>
                  <th className="text-left text-[12px] uppercase text-muted-foreground font-semibold px-[18px] py-2.5 border-t border-border">Member</th>
                  <th className="text-left text-[12px] uppercase text-muted-foreground font-semibold px-2 py-2.5 border-t border-border">Amount</th>
                  <th className="text-left text-[12px] uppercase text-muted-foreground font-semibold px-[18px] py-2.5 border-t border-border">State</th>
                </tr>
              </thead>
              <tbody>
                {recentContributions.map((c) => (
                  <tr key={c.id} className="cursor-pointer transition-colors hover:bg-muted/40" onClick={() => router.push(c.campaignId ? `/campaigns/${c.campaignId}` : "/contributions")}>
                    <td className="px-[18px] py-2.5 border-t border-border font-medium">
                      <Link href={c.campaignId ? `/campaigns/${c.campaignId}` : "/contributions"} onClick={(e) => e.stopPropagation()}>{c.memberName ?? "Unknown"}</Link>
                    </td>
                    <td className="px-2 py-2.5 border-t border-border tabular-nums">{formatCurrency(c.amount)}</td>
                    <td className="px-[18px] py-2.5 border-t border-border">
                      <Badge variant={c.status === "Successful" ? "success" : c.status === "Rejected" ? "destructive" : "warning"}>{c.status}</Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      </div>}
    </div>
  );
}
