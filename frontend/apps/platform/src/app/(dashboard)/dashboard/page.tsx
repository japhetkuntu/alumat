"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Card, CardContent, StatCard } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { TrendChart } from "@alumni/ui";
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

  const attentionList = institutions.filter((i) => i.status === "Suspended");
  const recentSignups = [...institutions].sort((a, b) => +new Date(b.onboardedAt) - +new Date(a.onboardedAt)).slice(0, 3);

  const growthData = (summary?.growthMonthLabels ?? []).map((month, i) => ({
    month,
    institutions: summary?.growthLast6Months[i] ?? 0,
  }));

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
          variant="hero"
          label="Platform revenue"
          value={formatCurrency(summary?.revenue ?? 0, "GHS")}
          sub="Platform fee collected across all institutions"
        />
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <StatCard
            label="Total institutions"
            value={summary?.totalInstitutions ?? "—"}
            sub={`${summary?.activeCount ?? 0} active · ${summary?.suspendedCount ?? 0} suspended`}
          />
          <StatCard
            label="Total members"
            value={(summary?.totalMembers ?? 0).toLocaleString()}
            sub={<span style={{ color: "var(--success)" }}>Across every institution</span>}
          />
          <StatCard
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
            <TrendChart
              data={growthData}
              xKey="month"
              series={[{ key: "institutions", label: "Institutions", color: "var(--brand-primary-500, var(--primary))" }]}
              variant="area"
              height={200}
              loading={!summary}
              emptyMessage="No institutions onboarded yet"
              valueFormatter={(v) => v.toLocaleString()}
            />
          </CardContent>
        </Card>

        <Card style={{ borderColor: attentionList.length > 0 ? "var(--border-emphasis)" : undefined }}>
          <div className="px-5 py-4 border-b border-border flex items-center justify-between">
            <p className="text-[14px] font-semibold">Needs attention</p>
            <Link href="/institutions" className="text-[12px] font-semibold text-accent hover:underline">View all</Link>
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
          <Link href="/institutions" className="text-[12px] font-semibold text-accent hover:underline">View all institutions</Link>
        </div>
        <CardContent className="p-0">
          {recentSignups.length === 0 && <p className="px-5 py-6 text-[13px] text-muted-foreground">No institutions yet.</p>}
          {recentSignups.map((inst) => (
            <div key={inst.id} className="flex items-center justify-between px-5 py-3.5 border-b border-border last:border-0">
              <div className="min-w-0">
                <p className="text-[13px] font-semibold truncate">{inst.name}</p>
                <p className="text-[12px] text-muted-foreground font-mono truncate">{inst.memberPortalUrl.replace(/^https?:\/\//, "")}</p>
              </div>
              <Badge variant={inst.status === "Suspended" ? "destructive" : "success"}>{inst.status}</Badge>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
