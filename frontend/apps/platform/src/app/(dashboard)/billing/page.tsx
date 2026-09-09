"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent, StatCard } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Table, TableBody, TableCell, TableEmpty, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import { TrendChart, DonutChart } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { Landmark, Clock3 } from "@alumni/ui";
import {
  getInstitutions, getAllPayments, getPayoutForecast,
  getPendingBatchPayouts, approveBatchPayout, rejectBatchPayout,
  getPendingInstitutionPayouts, approveInstitutionPayout, rejectInstitutionPayout,
} from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

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

  const qc = useQueryClient();
  const { data: pendingBatchPayouts = [], isLoading: pendingBatchPayoutsLoading } = useQuery({
    queryKey: ["platform-pending-batch-payouts"],
    queryFn: getPendingBatchPayouts,
    staleTime: 60 * 1000,
  });

  const approveMut = useMutation({
    mutationFn: (batchId: string) => approveBatchPayout(batchId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["platform-pending-batch-payouts"] }); toast.success("Batch payout approved"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const rejectMut = useMutation({
    mutationFn: (batchId: string) => rejectBatchPayout(batchId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["platform-pending-batch-payouts"] }); toast.success("Batch payout rejected"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const { data: pendingInstitutionPayouts = [], isLoading: pendingInstitutionPayoutsLoading } = useQuery({
    queryKey: ["platform-pending-institution-payouts"],
    queryFn: getPendingInstitutionPayouts,
    staleTime: 60 * 1000,
  });

  const approveInstitutionMut = useMutation({
    mutationFn: (institutionId: string) => approveInstitutionPayout(institutionId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["platform-pending-institution-payouts"] }); toast.success("Institution payout approved"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const rejectInstitutionMut = useMutation({
    mutationFn: (institutionId: string) => rejectInstitutionPayout(institutionId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["platform-pending-institution-payouts"] }); toast.success("Institution payout rejected"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const activeInstitutions = institutions.filter((i) => i.status === "Active");
  const suspendedCount = institutions.filter((i) => i.status === "Suspended").length;
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
  const statusCounts = payments.reduce<Record<string, number>>((acc, p) => {
    acc[p.status] = (acc[p.status] ?? 0) + 1;
    return acc;
  }, {});
  const statusPieData = Object.entries(statusCounts).map(([status, count]) => ({
    label: status,
    value: count,
    color: STATUS_COLORS[status] ?? "var(--muted-foreground)",
  }));
  const totalPayments = statusPieData.reduce((sum, d) => sum + d.value, 0);

  return (
    <div className="p-7 max-w-[1500px]">
      <h1 className="text-[24px] font-bold">Payments &amp; Revenue</h1>
      <p className="text-muted-foreground text-[13px] mt-1 mb-6">Platform-wide revenue operations across every institution.</p>

      <div className="grid grid-cols-2 lg:grid-cols-3 gap-4 mb-5">
        <Card><CardContent className="p-5"><p className="text-[12px] text-muted-foreground">Active institutions</p><p className="text-[24px] font-bold mt-1">{activeInstitutions.length}</p></CardContent></Card>
        <Card><CardContent className="p-5"><p className="text-[12px] text-muted-foreground">Suspended institutions</p><p className="text-[24px] font-bold mt-1">{suspendedCount}</p></CardContent></Card>
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
            <p className="text-[11px] text-muted-foreground mt-2">Estimated from confirmed transactions, not a figure confirmed by Paystack. Matches exactly what each institution's own SuperAdmins see on their dashboard.</p>
          </>
        ) : null}
      </div>

      <div className="mb-5">
        <p className="text-[14px] font-semibold mb-3">Batch payout approvals</p>
        <Card>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Institution</TableHead>
                <TableHead>Batch</TableHead>
                <TableHead>Account</TableHead>
                <TableHead>Submitted</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {pendingBatchPayoutsLoading ? (
                <TableRow><TableCell colSpan={5}><Skeleton className="h-6 w-full" /></TableCell></TableRow>
              ) : pendingBatchPayouts.length === 0 ? (
                <TableEmpty title="No pending batch payout setups" colSpan={5} />
              ) : pendingBatchPayouts.map((p) => (
                <TableRow key={p.batchId}>
                  <TableCell className="font-semibold">{p.institutionName}</TableCell>
                  <TableCell>{p.batchName} ({p.year})</TableCell>
                  <TableCell>
                    {p.useInstitutionAccount ? (
                      <Badge variant="secondary">Institution's own account</Badge>
                    ) : (
                      <span className="text-[13px]">{p.settlementBankName} · {p.settlementAccountNumber} · {p.settlementAccountName}</span>
                    )}
                  </TableCell>
                  <TableCell className="text-[13px] text-muted-foreground">{formatDate(p.submittedAt)}</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2 justify-end">
                      <Button size="sm" variant="outline" isLoading={rejectMut.isPending} onClick={() => rejectMut.mutate(p.batchId)}>Reject</Button>
                      <Button size="sm" isLoading={approveMut.isPending} onClick={() => approveMut.mutate(p.batchId)}>Approve</Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Card>
        <p className="text-[11px] text-muted-foreground mt-2">Approving creates (or links) the Paystack subaccount and switches the batch over immediately, same platform fee percentage as its institution.</p>
      </div>

      <div className="mb-5">
        <p className="text-[14px] font-semibold mb-3">Institution payout approvals</p>
        <Card>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Institution</TableHead>
                <TableHead>Account</TableHead>
                <TableHead>Submitted</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {pendingInstitutionPayoutsLoading ? (
                <TableRow><TableCell colSpan={4}><Skeleton className="h-6 w-full" /></TableCell></TableRow>
              ) : pendingInstitutionPayouts.length === 0 ? (
                <TableEmpty title="No pending institution payout setups" colSpan={4} />
              ) : pendingInstitutionPayouts.map((p) => (
                <TableRow key={p.institutionId}>
                  <TableCell className="font-semibold">{p.institutionName}</TableCell>
                  <TableCell>
                    <span className="text-[13px]">{p.settlementBankName} · {p.settlementAccountNumber} · {p.settlementAccountName}</span>
                  </TableCell>
                  <TableCell className="text-[13px] text-muted-foreground">{formatDate(p.submittedAt)}</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2 justify-end">
                      <Button size="sm" variant="outline" isLoading={rejectInstitutionMut.isPending} onClick={() => rejectInstitutionMut.mutate(p.institutionId)}>Reject</Button>
                      <Button size="sm" isLoading={approveInstitutionMut.isPending} onClick={() => approveInstitutionMut.mutate(p.institutionId)}>Approve</Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Card>
        <p className="text-[11px] text-muted-foreground mt-2">Approving creates (or links) the institution's own Paystack subaccount, same platform fee percentage already on file.</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[1.4fr_1fr] gap-4 mb-5 items-start">
        <Card>
          <div className="px-5 py-4 border-b border-border">
            <p className="text-[14px] font-semibold">Successful revenue trend</p>
            <p className="text-[12px] text-muted-foreground mt-0.5">Last 6 months, by source, every institution combined.</p>
          </div>
          <CardContent className="p-5">
            <TrendChart
              data={trendMonths}
              xKey="month"
              series={[
                { key: "Contributions", label: "Contributions", color: "var(--brand-primary-500, var(--primary))" },
                { key: "Store", label: "Store", color: "var(--brand-accent-500, var(--brand-accent))" },
              ]}
              variant="area"
              stacked
              height={220}
              loading={paymentsLoading}
              emptyMessage="No successful payments recorded yet this period"
              valueFormatter={(v) => formatCurrency(v, "GHS")}
            />
          </CardContent>
        </Card>

        <Card>
          <div className="px-5 py-4 border-b border-border">
            <p className="text-[14px] font-semibold">Payment status mix</p>
            <p className="text-[12px] text-muted-foreground mt-0.5">Every payment, every institution.</p>
          </div>
          <CardContent className="p-5">
            <DonutChart
              data={statusPieData}
              centerValue={totalPayments || undefined}
              centerLabel="payments"
              height={220}
              loading={paymentsLoading}
              emptyMessage="No payments yet"
              valueFormatter={(v) => v.toLocaleString()}
            />
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
                  <Badge variant={inst.status === "Suspended" ? "destructive" : "success"}>
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
