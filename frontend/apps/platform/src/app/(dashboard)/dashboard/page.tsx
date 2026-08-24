"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { getDashboardSummary, getInstitutions } from "@/lib/platform-api";

export default function PlatformDashboardPage() {
  const { data: summary } = useQuery({
    queryKey: ["dashboard-summary"],
    queryFn: getDashboardSummary,
  });
  const { data: institutionsPage } = useQuery({
    queryKey: ["institutions", { page: 1, pageSize: 50 }],
    queryFn: () => getInstitutions({ page: 1, pageSize: 50 }),
  });
  const institutions = institutionsPage?.results ?? [];

  const attentionList = institutions.filter((i) => i.status === "Suspended" || i.memberCount / i.memberLimit > 0.85);
  const recentSignups = [...institutions].sort((a, b) => +new Date(b.onboardedAt) - +new Date(a.onboardedAt)).slice(0, 3);

  return (
    <div className="p-7 max-w-[1500px]">
      <div className="flex items-end justify-between mb-6">
        <div>
          <h1 className="text-[26px] font-bold">Platform overview</h1>
          <p className="text-muted-foreground text-[13px] mt-1">Platform health as of today &middot; every institution at a glance</p>
        </div>
        <Link href="/institutions/new">
          <Button>Add institution</Button>
        </Link>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-5">
        <Card>
          <CardContent className="p-5">
            <p className="text-[12px] text-muted-foreground">Total Institutions</p>
            <p className="text-[26px] font-bold mt-1">{summary?.totalInstitutions ?? "—"}</p>
            <p className="text-[12px] text-muted-foreground mt-1">
              {summary?.activeCount ?? 0} active &middot; {summary?.trialCount ?? 0} trial
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-5">
            <p className="text-[12px] text-muted-foreground">Total Members</p>
            <p className="text-[26px] font-bold mt-1">{(summary?.totalMembers ?? 0).toLocaleString()}</p>
            <p className="text-[12px] text-emerald-700 mt-1">Across every institution</p>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-5">
            <p className="text-[12px] text-muted-foreground">Platform Revenue</p>
            <p className="text-[26px] font-bold mt-1">{formatCurrency(summary?.revenue ?? 0, "USD")}</p>
            <p className="text-[12px] text-muted-foreground mt-1">
              Platform fee collected across all institutions &middot; MRR {formatCurrency(summary?.mrr ?? 0, "USD")}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-5">
            <p className="text-[12px] text-muted-foreground">New Institutions</p>
            <p className="text-[26px] font-bold mt-1">{summary?.newInstitutionsThisMonth ?? "—"}</p>
            <p className="text-[12px] text-muted-foreground mt-1">This month</p>
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[1.4fr_1fr] gap-4">
        <Card>
          <div className="px-5 py-4 border-b border-border flex items-center justify-between">
            <p className="text-[14px] font-semibold">Institution growth</p>
            <span className="text-[12px] text-muted-foreground">Last 6 months</span>
          </div>
          <CardContent className="p-5">
            {summary && summary.growthLast6Months.length > 0 && (
              <>
                <div className="flex items-end gap-4 h-[160px] border-b border-border px-2">
                  {summary.growthLast6Months.map((v, i) => (
                    <div key={i} className="flex-1 flex flex-col items-center justify-end gap-1">
                      <span className="text-[11px] text-muted-foreground">{v}</span>
                      <div
                        className="w-full rounded-t-[4px] bg-primary/70"
                        style={{ height: `${(v / Math.max(...summary.growthLast6Months, 1)) * 100}%` }}
                      />
                    </div>
                  ))}
                </div>
                <div className="flex justify-around mt-2">
                  {summary.growthMonthLabels.map((m, i) => (
                    <span key={`${m}-${i}`} className="text-[11px] text-muted-foreground">{m}</span>
                  ))}
                </div>
              </>
            )}
          </CardContent>
        </Card>

        <Card>
          <div className="px-5 py-4 border-b border-border flex items-center justify-between">
            <p className="text-[14px] font-semibold">Needs attention</p>
            <Link href="/institutions" className="text-[12px] font-semibold text-primary hover:underline">View all</Link>
          </div>
          <CardContent className="p-0">
            {attentionList.length === 0 && <p className="px-5 py-6 text-[13px] text-muted-foreground">Nothing needs attention right now.</p>}
            {attentionList.map((inst) => (
              <div key={inst.id} className="flex items-center justify-between px-5 py-3.5 border-b border-border last:border-0">
                <div className="min-w-0">
                  <p className="text-[13px] font-semibold truncate">{inst.name}</p>
                  <p className="text-[12px] text-muted-foreground truncate">
                    {inst.status === "Suspended" ? "Suspended" : `${Math.round((inst.memberCount / inst.memberLimit) * 100)}% of member limit`}
                  </p>
                </div>
                <Badge variant={inst.status === "Suspended" ? "destructive" : "warning"}>
                  {inst.status === "Suspended" ? "Suspended" : "Usage risk"}
                </Badge>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <Card className="mt-4">
        <div className="px-5 py-4 border-b border-border flex items-center justify-between">
          <p className="text-[14px] font-semibold">Recent signups</p>
          <Link href="/institutions" className="text-[12px] font-semibold text-primary hover:underline">View all institutions</Link>
        </div>
        <CardContent className="p-0">
          {recentSignups.length === 0 && <p className="px-5 py-6 text-[13px] text-muted-foreground">No institutions yet.</p>}
          {recentSignups.map((inst) => (
            <div key={inst.id} className="flex items-center justify-between px-5 py-3.5 border-b border-border last:border-0">
              <div className="min-w-0">
                <p className="text-[13px] font-semibold truncate">{inst.name}</p>
                <p className="text-[12px] text-muted-foreground font-mono truncate">{inst.slug}.yourplatform.com</p>
              </div>
              <Badge variant={inst.status === "Trial" ? "info" : "success"}>{inst.status}</Badge>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
