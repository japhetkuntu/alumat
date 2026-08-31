"use client";

import { useQuery } from "@tanstack/react-query";
import { Card, CardContent, StatCard } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Table, TableBody, TableCell, TableEmpty, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { Landmark, Clock3 } from "lucide-react";
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Legend,
  PieChart, Pie, Cell,
} from "recharts";
import { getInstitutions, getAllPayments, getPayoutForecast } from "@/lib/platform-api";

const STATUS_COLORS: Record<string, string> = {
  Successful: "var(--success, #16a34a)",
  Pending: "var(--warning, #d97706)",
  Failed: "var(--destructive, #dc2626)",
  Rejected: "var(--destructive, #dc2626)",
};

export default function BillingPage() {
  const { data } = useQuery({
    queryKey: ["institutions", { page: 1, pageSize: 200 }],
    queryFn: () => getInstitutions({ page: 1, pageSize: 200 }),
  });
  const institutions = data?.results ?? [];

  const { data: paymentsData, isLoading: paymentsLoading } = useQuery({
    queryKey: ["platform-all-payments-analytics"],
    queryFn: () => getAllPayments(1, 1000),
  });
  const payments = paymentsData?.results ?? [];

  const { data: payoutForecast, isLoading: payoutsLoading } = useQuery({
    queryKey: ["platform-payout-forecast"],
    queryFn: getPayoutForecast,
    staleTime: 5 * 60 * 1000,
  });

  const activeInstitutions = institutions.filter((i) => i.status === "Active");
  const trialCount = institutions.filter((i) => i.status === "Trial").length;
  const totalRevenue = institutions.reduce((s, i) => s + i.revenue, 0);

  // Last 6 months, Successful payments only, split by source.
  const now = new Date();
  const monthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
  const trendMonths = Array.from({ length: 6 }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - (5 - i), 1);
    return { month: monthNames[d.getMonth()], key: `${d.getFullYear()}-${d.getMonth()}`, Contributions: 0, Store: 0 };
  });
  payments.filter((p) => p.status === "Successful").forEach((p) => {
    const d = new Date(p.confirmedAt ?? p.createdAt);
    const key = `${d.getFullYear()}-${d.getMonth()}`;
    const slot = trendMonths.find((m) => m.key === key);
    if (!slot) return;
    if (p.source === "Contribution") slot.Contributions += p.amount;
    else slot.Store += p.amount;
  });
  const hasTrendData = trendMonths.some((m) => m.Contributions > 0 || m.Store > 0);

  const statusCounts = payments.reduce<Record<string, number>>((acc, p) => {
    acc[p.status] = (acc[p.status] ?? 0) + 1;
    return acc;
  }, {});
  const statusPieData = Object.entries(statusCounts).map(([status, count]) => ({ status, count }));

  return (
    <div className="p-7 max-w-[1500px]">
      <h1 className="text-[24px] font-bold">Payments &amp; Revenue</h1>
      <p className="text-muted-foreground text-[13px] mt-1 mb-6">Platform-wide revenue operations across every institution.</p>

      <div className="grid grid-cols-2 lg:grid-cols-3 gap-4 mb-5">
        <Card><CardContent className="p-5"><p className="text-[12px] text-muted-foreground">Active institutions</p><p className="text-[24px] font-bold mt-1">{activeInstitutions.length}</p></CardContent></Card>
        <Card><CardContent className="p-5"><p className="text-[12px] text-muted-foreground">Trial institutions</p><p className="text-[24px] font-bold mt-1">{trialCount}</p></CardContent></Card>
        <Card><CardContent className="p-5"><p className="text-[12px] text-muted-foreground">Total platform revenue</p><p className="text-[24px] font-bold mt-1">{formatCurrency(totalRevenue, "GHS")}</p></CardContent></Card>
      </div>

      {/* Estimated from confirmed transactions, same formula/windows every
          institution's own SuperAdmins see on their side — never a Paystack-
          confirmed settlement figure, see the note below the table. */}
      <div className="mb-5">
        <p className="text-[14px] font-semibold mb-3">Expected payouts</p>
        {payoutsLoading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
            {Array.from({ length: 2 }).map((_, i) => <div key={i} className="card p-5 h-[128px] skeleton" />)}
          </div>
        ) : payoutForecast ? (
          <>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
              <StatCard
                icon={Landmark}
                variant="hero"
                label="Last payout, all institutions"
                value={formatCurrency(payoutForecast.totals.lastPayout.amount, "GHS")}
                sub={`Settled ${formatDate(payoutForecast.totals.lastPayout.date)} · ${payoutForecast.totals.lastPayout.transactionCount} transactions`}
              />
              <StatCard
                icon={Clock3}
                variant="hero"
                tone="accent"
                label="Next payout, all institutions"
                value={formatCurrency(payoutForecast.totals.nextPayout.amount, "GHS")}
                sub={`Expected ${formatDate(payoutForecast.totals.nextPayout.date)} morning · still accumulating`}
              />
            </div>
            <Card>
              <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Per institution</p></div>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Institution</TableHead>
                    <TableHead>Last payout</TableHead>
                    <TableHead>Next payout</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {payoutForecast.institutions.length === 0 && <TableEmpty title="No institutions yet" colSpan={3} />}
                  {payoutForecast.institutions.map((f) => (
                    <TableRow key={f.institutionId}>
                      <TableCell className="font-semibold">
                        {f.institutionName}
                        {!f.payoutsConfigured && <Badge variant="warning" className="ml-2">Not set up</Badge>}
                      </TableCell>
                      <TableCell className="tabular-nums">{formatCurrency(f.lastPayout.amount, "GHS")}</TableCell>
                      <TableCell className="tabular-nums">{formatCurrency(f.nextPayout.amount, "GHS")}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Card>
            <p className="text-[11px] text-muted-foreground mt-2">Estimated from confirmed transactions — not a figure confirmed by Paystack. Matches exactly what each institution's own SuperAdmins see on their dashboard.</p>
          </>
        ) : null}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[1.4fr_1fr] gap-4 mb-5 items-start">
        <Card>
          <div className="px-5 py-4 border-b border-border">
            <p className="text-[14px] font-semibold">Successful revenue trend</p>
            <p className="text-[12px] text-muted-foreground mt-0.5">Last 6 months, by source — every institution combined.</p>
          </div>
          <CardContent className="p-5">
            {paymentsLoading ? (
              <Skeleton className="h-[220px]" />
            ) : (
              <div className="relative">
                <ResponsiveContainer width="100%" height={220}>
                  <BarChart data={trendMonths} barSize={22}>
                    <XAxis dataKey="month" axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: "var(--muted-foreground)" }} />
                    <YAxis hide />
                    <Tooltip formatter={(v) => formatCurrency(Number(v), "GHS")} contentStyle={{ borderRadius: "6px", border: "1px solid var(--border)", fontSize: 12 }} />
                    <Legend wrapperStyle={{ fontSize: 12 }} />
                    <Bar dataKey="Contributions" stackId="a" fill="var(--primary)" radius={[0, 0, 0, 0]} />
                    <Bar dataKey="Store" stackId="a" fill="var(--brand-accent, #d97706)" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
                {!hasTrendData && (
                  <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
                    <p className="text-[13px] text-muted-foreground bg-background/80 px-3 py-1.5 rounded-md">
                      No successful payments recorded yet this period
                    </p>
                  </div>
                )}
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <div className="px-5 py-4 border-b border-border">
            <p className="text-[14px] font-semibold">Payment status mix</p>
            <p className="text-[12px] text-muted-foreground mt-0.5">Every payment, every institution.</p>
          </div>
          <CardContent className="p-5">
            {paymentsLoading ? (
              <Skeleton className="h-[220px]" />
            ) : statusPieData.length === 0 ? (
              <div className="h-[220px] flex items-center justify-center">
                <p className="text-[13px] text-muted-foreground">No payments yet</p>
              </div>
            ) : (
              <ResponsiveContainer width="100%" height={220}>
                <PieChart>
                  <Pie data={statusPieData} dataKey="count" nameKey="status" innerRadius={50} outerRadius={80} paddingAngle={2}>
                    {statusPieData.map((entry) => (
                      <Cell key={entry.status} fill={STATUS_COLORS[entry.status] ?? "var(--muted-foreground)"} />
                    ))}
                  </Pie>
                  <Tooltip contentStyle={{ borderRadius: "6px", border: "1px solid var(--border)", fontSize: 12 }} />
                  <Legend wrapperStyle={{ fontSize: 12 }} />
                </PieChart>
              </ResponsiveContainer>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">All institutions</p></div>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Institution</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Platform fee</TableHead>
              <TableHead>Revenue</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {institutions.length === 0 && <TableEmpty title="No institutions yet" colSpan={4} />}
            {institutions.map((inst) => (
              <TableRow key={inst.id}>
                <TableCell className="font-semibold">{inst.name}</TableCell>
                <TableCell>
                  <Badge variant={inst.status === "Suspended" ? "destructive" : inst.status === "Trial" ? "info" : "success"}>
                    {inst.status}
                  </Badge>
                </TableCell>
                <TableCell>{inst.platformFeePercentage}%</TableCell>
                <TableCell>{formatCurrency(inst.revenue, "GHS")}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
    </div>
  );
}
