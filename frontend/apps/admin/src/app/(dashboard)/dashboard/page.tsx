"use client";

import { useQueries } from "@tanstack/react-query";
import { Users, Megaphone, CreditCard, Calendar, Clock } from "lucide-react";
import { UserAvatar } from "@/components/ui/user-avatar";
import { StatCard } from "@/components/shared/stat-card";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { StatSkeleton } from "@/components/ui/skeleton";
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
} from "recharts";
import { formatCurrency, formatDate } from "@/lib/utils";
import { getCampaigns, getContributions, getMembers, getEvents, getJobs } from "@/lib/admin-api";
import Link from "next/link";

export default function AdminDashboardPage() {
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
  const allCampaigns = campaigns.data?.results ?? [];
  const activeCampaigns = allCampaigns.filter((c) => c.status === "Active");
  const totalAmountCollected = allCampaigns.reduce((sum, c) => sum + c.collectedAmount, 0);
  const allContributions = contributions.data?.results ?? [];
  const recentContributions = allContributions.slice(0, 5);
  const totalContributions = contributions.data?.totalCount ?? 0;
  const upcomingEvents = events.data?.totalCount ?? 0;
  const openJobs = jobs.data?.totalCount ?? 0;

  const now = new Date();
  const monthNames = ["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];
  const trendMonths = Array.from({ length: 7 }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - (6 - i), 1);
    return { month: monthNames[d.getMonth()], key: `${d.getFullYear()}-${d.getMonth()}`, amount: 0 };
  });
  allContributions.filter((c) => c.status === "Confirmed").forEach((c) => {
    const d = new Date(c.confirmedAt ?? c.createdAt);
    const key = `${d.getFullYear()}-${d.getMonth()}`;
    const slot = trendMonths.find((m) => m.key === key);
    if (slot) slot.amount += c.amount;
  });

  return (
    <div className="p-4 sm:p-6 lg:p-8 space-y-8 max-w-[1800px] mx-auto page-enter">
      <div className="animate-in fade-in slide-in-from-bottom-3 duration-700">
        <h1 className="text-3xl font-extrabold tracking-tight">Dashboard</h1>
        <p className="text-muted-foreground text-sm mt-1 font-medium">Welcome back. Here&apos;s what&apos;s happening across the platform.</p>
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4">
          {Array.from({ length: 4 }).map((_, i) => <StatSkeleton key={i} />)}
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4">
          <div style={{ animationDelay: "0ms" }} className="stagger-item">
            <StatCard title="Total Members" value={totalMembers.toLocaleString()} subtitle={`${pendingApprovals} pending approval`} icon={Users} iconClassName="bg-blue-500/10 text-blue-600 dark:text-blue-400" />
          </div>
          <div style={{ animationDelay: "50ms" }} className="stagger-item">
            <StatCard title="Active Campaigns" value={activeCampaigns.length} subtitle="Accepting contributions" icon={Megaphone} iconClassName="bg-violet-500/10 text-violet-600 dark:text-violet-400" />
          </div>
          <div style={{ animationDelay: "100ms" }} className="stagger-item">
            <StatCard title="Total Collected" value={formatCurrency(totalAmountCollected)} subtitle={`${totalContributions} contributions`} icon={CreditCard} iconClassName="bg-emerald-500/10 text-emerald-600 dark:text-emerald-400" />
          </div>
          <div style={{ animationDelay: "150ms" }} className="stagger-item">
            <StatCard title="Upcoming Events" value={upcomingEvents} subtitle={`${openJobs} open jobs`} icon={Calendar} iconClassName="bg-amber-500/10 text-amber-600 dark:text-amber-400" />
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <Card className="lg:col-span-2">
          <CardHeader><CardTitle className="text-[15px]">Contribution Trend (last 7 months)</CardTitle></CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={trendMonths} barSize={28}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--border)" />
                <XAxis dataKey="month" axisLine={false} tickLine={false} tick={{ fontSize: 12, fill: "var(--muted-foreground)" }} />
                <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 12, fill: "var(--muted-foreground)" }} tickFormatter={(v: number) => v >= 1000 ? `${(v / 1000).toFixed(0)}K` : String(v)} />
                <Tooltip formatter={(v) => formatCurrency(Number(v))} contentStyle={{ borderRadius: "8px", border: "1px solid var(--border)", boxShadow: "0 4px 12px rgba(0,0,0,0.08)" }} />
                <Bar dataKey="amount" fill="var(--primary)" radius={[6, 6, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-[15px] flex items-center justify-between">
              Pending Approvals
              <Badge variant="warning">{pendingApprovals}</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {(membersPending.data?.results ?? []).slice(0, 3).map((m, i) => (
              <div key={m.id} className="flex items-center justify-between text-sm stagger-item" style={{ animationDelay: `${i * 50}ms` }}>
                <div className="flex items-center gap-2.5">
                  <UserAvatar src={m.profilePictureUrl} name={`${m.firstName} ${m.lastName}`} size="sm" />
                  <span className="truncate max-w-[140px] text-[13px]">{m.firstName} {m.lastName}</span>
                </div>
                <Clock size={12} className="text-muted-foreground/50" />
              </div>
            ))}
            {pendingApprovals === 0 && !isLoading && <p className="text-xs text-muted-foreground text-center py-4">No pending approvals</p>}
            {pendingApprovals > 0 && <Link href="/members" className="block text-xs text-primary text-center mt-3 hover:underline font-medium">View all {pendingApprovals}</Link>}
          </CardContent>
        </Card>
      </div>

      {activeCampaigns.length > 0 && (
        <Card>
          <CardHeader><CardTitle className="text-[15px]">Active Campaigns</CardTitle></CardHeader>
          <CardContent className="space-y-5">
            {activeCampaigns.map((c) => {
              const isMembership = !!c.isMembershipCampaign;
              const pct = isMembership && c.totalEligibleMembers
                ? Math.round((c.paidCount / c.totalEligibleMembers) * 100)
                : c.targetAmount > 0 ? Math.round((c.collectedAmount / c.targetAmount) * 100) : 0;
              return (
                <div key={c.id}>
                  <div className="flex items-start justify-between gap-3 mb-1.5 min-w-0">
                    <span className="font-medium text-[13px] min-w-0 flex-1 break-words leading-snug">{c.title}</span>
                    <span className="text-[12px] text-muted-foreground shrink-0 whitespace-nowrap text-right">{isMembership ? `${c.paidCount}/${c.totalEligibleMembers ?? '?'} paid` : `${formatCurrency(c.collectedAmount)} / ${formatCurrency(c.targetAmount)}`}</span>
                  </div>
                  <Progress value={pct} className="h-1.5" />
                  <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 mt-1.5 min-w-0">
                    <span className="text-[11px] text-muted-foreground break-words">{isMembership ? `${pct}% members paid` : `${pct}% reached · ${c.paidCount} paid`}</span>
                    <span className="text-[11px] text-muted-foreground whitespace-nowrap">Deadline: {formatDate(c.deadline)}</span>
                  </div>
                </div>
              );
            })}
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-[15px]">Recent Contributions</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {recentContributions.length === 0 && !isLoading ? (
            <p className="text-[13px] text-muted-foreground text-center py-6">No contributions yet</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[720px] text-[13px] table-auto">
                <thead>
                  <tr className="border-b text-left text-muted-foreground bg-muted/25">
                    <th className="pb-3 pt-3 px-6 font-medium">Member</th>
                    <th className="pb-3 pt-3 px-4 font-medium">Campaign</th>
                    <th className="pb-3 pt-3 px-4 font-medium">Amount</th>
                    <th className="pb-3 pt-3 px-4 font-medium">Method</th>
                    <th className="pb-3 pt-3 px-4 font-medium">Date</th>
                  </tr>
                </thead>
                <tbody>
                  {recentContributions.map((c) => (
                    <tr key={c.id} className="border-b last:border-0 hover:bg-muted/30 transition-colors">
                      <td className="py-3 px-6 font-medium">{c.memberName ?? "Unknown"}</td>
                      <td className="py-3 px-4 text-muted-foreground truncate max-w-[180px]">{c.campaignTitle ?? "Unknown Campaign"}</td>
                      <td className="py-3 px-4 font-medium">{formatCurrency(c.amount)}</td>
                      <td className="py-3 px-4"><Badge variant={c.paymentMethod === "Paystack" ? "info" : "secondary"} className="text-[11px]">{c.paymentMethod}</Badge></td>
                      <td className="py-3 px-4 text-muted-foreground">{formatDate(c.confirmedAt ?? c.createdAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
