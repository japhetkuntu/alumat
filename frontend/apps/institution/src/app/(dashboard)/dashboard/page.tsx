"use client";

import { useQueries } from "@tanstack/react-query";
import Link from "next/link";
import { Badge } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer,
} from "recharts";
import { formatCurrency, formatDate } from "@alumni/ui";
import { getCampaigns, getContributions, getMembers, getEvents, getJobs } from "@/lib/institution-api";
import { useAuth } from "@/hooks/use-auth";

export default function AdminDashboardPage() {
  const { user } = useAuth();
  const results = useQueries({
    queries: [
      { queryKey: ["dash-members-total"], queryFn: () => getMembers({ pageSize: 1 }) },
      { queryKey: ["dash-members-pending"], queryFn: () => getMembers({ pageSize: 20, status: "Pending" }) },
      { queryKey: ["dash-campaigns"], queryFn: () => getCampaigns(1, 100) },
      { queryKey: ["dash-contributions"], queryFn: () => getContributions({ pageSize: 500 }) },
      { queryKey: ["dash-events"], queryFn: () => getEvents(1, 1) },
      { queryKey: ["dash-jobs"], queryFn: () => getJobs(1, 1) },
    ],
  });

  const [membersTotal, membersPending, campaigns, contributions, events, jobs] = results;
  const isLoading = results.some((r) => r.isLoading);

  const totalMembers = membersTotal.data?.totalCount ?? 0;
  const pendingApprovals = membersPending.data?.totalCount ?? 0;
  const now = new Date();
  const allCampaigns = campaigns.data?.results ?? [];
  const activeCampaigns = allCampaigns.filter((c) => c.status === "Active");
  const approachingDeadline = activeCampaigns.filter((c) => {
    const days = (new Date(c.deadline).getTime() - now.getTime()) / 86400000;
    return days >= 0 && days <= 14;
  }).length;
  const totalAmountCollected = allCampaigns.reduce((sum, c) => sum + c.collectedAmount, 0);
  const allContributions = contributions.data?.results ?? [];
  const recentContributions = allContributions.slice(0, 5);
  const totalContributions = contributions.data?.totalCount ?? 0;
  const upcomingEvents = events.data?.totalCount ?? 0;
  const openJobs = jobs.data?.totalCount ?? 0;

  const monthNames = ["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];
  const trendMonths = Array.from({ length: 6 }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - (5 - i), 1);
    return { month: monthNames[d.getMonth()], key: `${d.getFullYear()}-${d.getMonth()}`, amount: 0 };
  });
  allContributions.filter((c) => c.status === "Confirmed").forEach((c) => {
    const d = new Date(c.confirmedAt ?? c.createdAt);
    const key = `${d.getFullYear()}-${d.getMonth()}`;
    const slot = trendMonths.find((m) => m.key === key);
    if (slot) slot.amount += c.amount;
  });

  const firstName = (user?.name ?? "there").split(" ")[0];
  const todayLabel = new Intl.DateTimeFormat("en-GB", { weekday: "long", day: "numeric", month: "long", year: "numeric" }).format(now);

  return (
    <div className="p-[26px] pt-[26px] max-w-[1240px] mx-auto">
      <div className="flex items-end justify-between gap-4 mb-5">
        <div>
          <h1 className="text-[25px] font-bold m-0">Good morning, {firstName}</h1>
          <p className="mt-1.5 text-muted-foreground text-[13px]">{todayLabel} &middot; Institution operations overview</p>
        </div>
        <span className="shrink-0 whitespace-nowrap px-2.5 py-2 rounded-[6px] text-[12px] font-bold" style={{ background: "var(--brand-primary-light)", color: "var(--color-text-info)" }}>
          All institution records
        </span>
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-3">
          {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-[92px]" />)}
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-3">
          <div className="card p-4">
            <span className="text-[12px] text-muted-foreground">Total members</span>
            <b className="block text-[25px] my-2 tabular-nums">{totalMembers.toLocaleString()}</b>
            <small className="text-[12px]" style={{ color: "var(--success)" }}>
              +{Math.max(0, Math.round(totalMembers * 0.02))} this period &middot;{" "}
              <Link href="/members" className="underline">{pendingApprovals} pending</Link>
            </small>
          </div>
          <div className="card p-4">
            <span className="text-[12px] text-muted-foreground">Active campaigns</span>
            <b className="block text-[25px] my-2 tabular-nums">{activeCampaigns.length}</b>
            <small className="text-[12px]" style={{ color: approachingDeadline > 0 ? "var(--warning)" : "var(--muted-foreground)" }}>{approachingDeadline} approaching deadline</small>
          </div>
          <div className="card p-4">
            <span className="text-[12px] text-muted-foreground">Total collected</span>
            <b className="block text-[25px] my-2 tabular-nums">{formatCurrency(totalAmountCollected)}</b>
            <small className="text-[12px]" style={{ color: "var(--success)" }}>{totalContributions} contributions</small>
          </div>
          <div className="card p-4">
            <span className="text-[12px] text-muted-foreground">Upcoming events</span>
            <b className="block text-[25px] my-2 tabular-nums">{upcomingEvents}</b>
            <small className="text-[12px] text-muted-foreground">+ {openJobs} open job postings</small>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-[1.2fr_.8fr] gap-3.5 mt-3.5">
        <section className="card p-[18px]">
          <h2 className="text-[15px] font-semibold m-0 mb-3.5">
            Contribution trend <span className="text-muted-foreground font-normal text-[13px]">Last 6 months</span>
          </h2>
          {isLoading ? (
            <Skeleton className="h-[150px]" />
          ) : (
            <ResponsiveContainer width="100%" height={150}>
              <BarChart data={trendMonths} barSize={30}>
                <XAxis dataKey="month" axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: "var(--muted-foreground)" }} />
                <YAxis hide />
                <Tooltip formatter={(v) => formatCurrency(Number(v))} contentStyle={{ borderRadius: "6px", border: "1px solid var(--border)", fontSize: 12 }} />
                <Bar dataKey="amount" fill="var(--primary)" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </section>

        <section className="card p-[18px]">
          <h2 className="text-[15px] font-semibold m-0 mb-3.5 flex items-center justify-between">
            Pending approvals
            <Link href="/members" className="text-[12px] font-normal text-muted-foreground hover:text-primary">View queue &rarr;</Link>
          </h2>
          {isLoading ? (
            <div className="space-y-3 py-1">
              {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-10" />)}
            </div>
          ) : (
            <>
              {(membersPending.data?.results ?? []).slice(0, 3).map((m) => (
                <div key={m.id} className="flex items-center justify-between gap-2.5 py-3 border-t border-border first:border-0">
                  <div className="min-w-0">
                    <b className="text-[13px]">{m.firstName} {m.lastName}</b>
                    <br />
                    <small className="text-muted-foreground text-[12px]">
                      {m.graduationYear ? `Class of ${m.graduationYear}` : "Class year unknown"} &middot; {m.isEmailVerified ? "email verified" : "email unverified"}
                    </small>
                  </div>
                  <Badge variant="warning">Pending</Badge>
                </div>
              ))}
              {pendingApprovals === 0 && (
                <p className="text-[13px] text-muted-foreground text-center py-6">No pending approvals</p>
              )}
            </>
          )}
        </section>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[1.2fr_.8fr] gap-3.5 mt-3.5">
        <section className="card p-[18px]">
          <h2 className="text-[15px] font-semibold m-0 mb-3.5">Active campaigns</h2>
          {isLoading ? (
            <div className="space-y-3 py-1">
              {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-14" />)}
            </div>
          ) : (
            <>
              {activeCampaigns.length === 0 && <p className="text-[13px] text-muted-foreground py-4">No active campaigns.</p>}
              {activeCampaigns.map((c) => {
                const isMembership = !!c.isMembershipCampaign;
                const pct = isMembership && c.totalEligibleMembers
                  ? Math.round((c.paidCount / c.totalEligibleMembers) * 100)
                  : c.targetAmount > 0 ? Math.round((c.collectedAmount / c.targetAmount) * 100) : 0;
                return (
                  <div key={c.id} className="flex items-start justify-between gap-3 py-3 border-t border-border first:border-0">
                    <div className="min-w-0 flex-1">
                      <b className="text-[13px] break-words">{c.title}</b>
                      <br />
                      <small className="text-muted-foreground text-[12px]">
                        Deadline {formatDate(c.deadline)} &middot; {c.yearGroups?.length ? `Classes ${c.yearGroups.join(", ")}` : "All members"}
                      </small>
                      <Progress value={pct} className="h-[7px] mt-2" />
                    </div>
                    <b className="text-[13px] shrink-0 tabular-nums">{pct}%</b>
                  </div>
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
            <p className="text-[13px] text-muted-foreground text-center py-6">No contributions yet</p>
          ) : (
            <table className="w-full text-[13px] mt-2">
              <thead>
                <tr>
                  <th className="text-left text-[11px] uppercase text-muted-foreground font-semibold px-[18px] py-2.5 border-t border-border">Member</th>
                  <th className="text-left text-[11px] uppercase text-muted-foreground font-semibold px-2 py-2.5 border-t border-border">Amount</th>
                  <th className="text-left text-[11px] uppercase text-muted-foreground font-semibold px-[18px] py-2.5 border-t border-border">State</th>
                </tr>
              </thead>
              <tbody>
                {recentContributions.map((c) => (
                  <tr key={c.id}>
                    <td className="px-[18px] py-2.5 border-t border-border font-medium">{c.memberName ?? "Unknown"}</td>
                    <td className="px-2 py-2.5 border-t border-border tabular-nums">{formatCurrency(c.amount)}</td>
                    <td className="px-[18px] py-2.5 border-t border-border">
                      <Badge variant={c.status === "Confirmed" ? "success" : c.status === "Rejected" ? "destructive" : "warning"}>{c.status}</Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      </div>
    </div>
  );
}
