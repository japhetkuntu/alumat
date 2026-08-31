"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Card, CardContent, StatCard } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { Wallet, Building2, Users, Sparkles } from "lucide-react";
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

  const attentionList = institutions.filter((i) => i.status === "Suspended");
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

      {/* Revenue leads — the one figure platform staff check first when
          scanning fleet health, everything else demoted to a supporting row. */}
      <div className="grid grid-cols-1 lg:grid-cols-[minmax(280px,1.3fr)_2fr] gap-4 mb-5 items-stretch">
        <StatCard
          icon={Wallet}
          variant="hero"
          label="Platform revenue"
          value={formatCurrency(summary?.revenue ?? 0, "GHS")}
          sub="Platform fee collected across all institutions"
        />
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <StatCard
            icon={Building2}
            label="Total institutions"
            value={summary?.totalInstitutions ?? "—"}
            sub={`${summary?.activeCount ?? 0} active · ${summary?.trialCount ?? 0} trial`}
          />
          <StatCard
            icon={Users}
            label="Total members"
            value={(summary?.totalMembers ?? 0).toLocaleString()}
            sub={<span style={{ color: "var(--success)" }}>Across every institution</span>}
          />
          <StatCard
            icon={Sparkles}
            tone="accent"
            label="New institutions"
            value={summary?.newInstitutionsThisMonth ?? "—"}
            sub="This month"
          />
        </div>
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

        <Card style={{ borderColor: attentionList.length > 0 ? "var(--border-emphasis)" : undefined }}>
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
                  <p className="text-[12px] text-muted-foreground truncate">Suspended</p>
                </div>
                <Badge variant="destructive">Suspended</Badge>
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
                <p className="text-[12px] text-muted-foreground font-mono truncate">{inst.memberPortalUrl.replace(/^https?:\/\//, "")}</p>
              </div>
              <Badge variant={inst.status === "Trial" ? "info" : "success"}>{inst.status}</Badge>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
