"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Table, TableBody, TableCell, TableEmpty, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { formatDate, formatCurrency } from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import { getInstitutions, type InstitutionListItem } from "@/lib/platform-api";
import { InstitutionStatus } from "@/types";

const STATUS_FILTERS: { label: string; value: InstitutionStatus | "All" }[] = [
  { label: "All", value: "All" },
  { label: "Trial", value: "Trial" },
  { label: "Active", value: "Active" },
  { label: "Suspended", value: "Suspended" },
  { label: "Cancelled", value: "Cancelled" },
];

const statusBadge: Record<string, { label: string; variant: "info" | "success" | "warning" | "destructive" }> = {
  Trial: { label: "Trial", variant: "info" },
  Active: { label: "Active", variant: "success" },
  Suspended: { label: "Suspended", variant: "destructive" },
  Cancelled: { label: "Cancelled", variant: "warning" },
};

export default function InstitutionsPage() {
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<InstitutionStatus | "All">("All");
  const [exporting, setExporting] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ["institutions", search, status],
    queryFn: () =>
      getInstitutions({
        page: 1,
        pageSize: 50,
        search: search.trim() || undefined,
        status: status === "All" ? undefined : status,
      }),
  });

  const results = data?.results ?? [];

  async function exportCsv() {
    setExporting(true);
    try {
      const all = await getInstitutions({
        page: 1,
        pageSize: 5000,
        search: search.trim() || undefined,
        status: status === "All" ? undefined : status,
      });
      const rows = all.results ?? [];
      if (!rows.length) {
        toast.error("No institutions to export");
        return;
      }
      const headers: (keyof InstitutionListItem)[] = [
        "name", "slug", "status", "contactName", "contactEmail",
        "memberCount", "platformFeePercentage", "revenue", "onboardedAt",
      ];
      const csv = [
        headers.join(","),
        ...rows.map((inst) => headers.map((h) => `"${String(inst[h] ?? "").replace(/"/g, '""')}"`).join(",")),
      ].join("\n");
      const a = document.createElement("a");
      a.href = URL.createObjectURL(new Blob([csv], { type: "text/csv" }));
      a.download = `institutions-${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      toast.success(`Exported ${rows.length} institutions`);
    } catch (e) {
      toast.error(handleApiError(e));
    } finally {
      setExporting(false);
    }
  }

  return (
    <div className="p-7 max-w-[1500px]">
      <div className="flex items-end justify-between mb-1">
        <div>
          <h1 className="text-[24px] font-bold">Institutions</h1>
          <p className="text-muted-foreground text-[13px] mt-1">A complete registry of tenant institutions and their operational state.</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" disabled={exporting} onClick={exportCsv}>
            {exporting ? "Exporting…" : "Export CSV"}
          </Button>
          <Link href="/institutions/new">
            <Button>Add institution</Button>
          </Link>
        </div>
      </div>

      <Card className="mt-5 mb-4">
        <div className="p-4 flex flex-col sm:flex-row gap-3 sm:items-center">
          <Input
            placeholder="Search by name, domain, or contact"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="sm:w-[320px]"
          />
          <div className="flex gap-2 flex-wrap">
            {STATUS_FILTERS.map((f) => (
              <button
                key={f.value}
                onClick={() => setStatus(f.value)}
                className={`text-[12.5px] font-medium px-3 py-1.5 border transition-colors ${
                  status === f.value
                    ? "bg-primary/10 text-primary border-primary/30"
                    : "bg-background text-muted-foreground border-border hover:bg-muted"
                }`}
              >
                {f.label}
              </button>
            ))}
          </div>
        </div>
      </Card>

      <Card>
        <div className="px-5 py-4 border-b border-border flex items-center justify-between">
          <p className="text-[14px] font-semibold">
            All institutions <span className="text-muted-foreground font-normal">{results.length} results</span>
          </p>
        </div>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Institution</TableHead>
              <TableHead>Members</TableHead>
              <TableHead>Revenue</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Onboarded</TableHead>
              <TableHead>Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {!isLoading && results.length === 0 && <TableEmpty title="No institutions match your filters" colSpan={6} />}
            {results.map((inst) => (
              <TableRow key={inst.id}>
                <TableCell>
                  <div className="flex items-center gap-3">
                    <UserAvatar src={inst.logoUrl} name={inst.name} size="sm" />
                    <div className="min-w-0">
                      <p className="font-semibold truncate">{inst.name}</p>
                      <p className="text-[12px] text-muted-foreground font-mono truncate">
                        {inst.customDomain ?? inst.memberPortalUrl.replace(/^https?:\/\//, "")} &middot; {inst.contactEmail}
                      </p>
                    </div>
                  </div>
                </TableCell>
                <TableCell className="tabular-nums">{inst.memberCount.toLocaleString()}</TableCell>
                <TableCell className="tabular-nums font-medium">{formatCurrency(inst.revenue, "GHS")}</TableCell>
                <TableCell>
                  <Badge variant={(statusBadge[inst.status] ?? statusBadge.Trial).variant}>
                    {(statusBadge[inst.status] ?? statusBadge.Trial).label}
                  </Badge>
                </TableCell>
                <TableCell>{formatDate(inst.onboardedAt)}</TableCell>
                <TableCell>
                  <div className="flex gap-2">
                    <Link href={`/institutions/${inst.id}`}>
                      <Button size="sm" variant="outline">View</Button>
                    </Link>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
    </div>
  );
}
