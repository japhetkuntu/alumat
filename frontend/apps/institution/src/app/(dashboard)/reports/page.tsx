"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Download, Users, Calendar, Activity, Layers, DollarSign, Loader2 } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { StatSkeleton, CardSkeleton } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { getCampaigns, getReportSummary, exportReportCsv, type MemberReportExportFilters } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";

const MEMBER_STATUS_OPTIONS = [
  { value: "", label: "Any status" },
  { value: "Active", label: "Active" },
  { value: "Pending", label: "Pending" },
  { value: "Suspended", label: "Suspended" },
  { value: "Banned", label: "Banned" },
];

type ExportEntity = "campaigns" | "members" | "contributions" | "events" | "jobs";

export default function AdminReportsPage() {
  const [memberStatusFilter, setMemberStatusFilter] = useState("");
  const [memberYearFrom, setMemberYearFrom] = useState("");
  const [memberYearTo, setMemberYearTo] = useState("");
  const [memberProfession, setMemberProfession] = useState("");
  const [memberLocation, setMemberLocation] = useState("");
  const [exporting, setExporting] = useState<ExportEntity | null>(null);

  // Only these two feed anything on this page — a prior version also fetched
  // members/contributions/events/jobs (each just for a `pageSize: 1` count)
  // and gated the whole page's loading state on all six requests together,
  // so the stat cards stayed skeletons until the slowest of six calls
  // resolved instead of the two that actually matter.
  const summaryQuery = useQuery({
    queryKey: ["report-summary"],
    queryFn: () => getReportSummary(),
  });

  const campaignsQuery = useQuery({
    queryKey: ["report-campaigns"],
    queryFn: () => getCampaigns(1, 100),
  });

  useEffect(() => {
    if (summaryQuery.isError) toast.error("Unable to load report summary metrics");
    if (campaignsQuery.isError) toast.error("Unable to load fundraiser details");
  }, [summaryQuery.isError, campaignsQuery.isError]);

  const campaigns = campaignsQuery.data?.results ?? [];
  const totalCampaigns = summaryQuery.data?.totalCampaigns ?? 0;
  const activeCampaigns = summaryQuery.data?.activeCampaigns ?? 0;
  const closedCampaigns = summaryQuery.data?.closedCampaigns ?? 0;
  const totalMembers = summaryQuery.data?.totalMembers ?? 0;
  const totalContributions = summaryQuery.data?.totalContributions ?? 0;
  const totalCollected = summaryQuery.data?.totalCollected ?? 0;
  const totalEvents = summaryQuery.data?.totalEvents ?? 0;
  const isLoading = summaryQuery.isLoading || campaignsQuery.isLoading;

  // Downloads straight from the backend's own scoped CSV builder (see
  // ReportService.ExportEntityCsvAsync) — server-side, not capped at any
  // client page size, and consistently scoped with everything else a
  // ScopedAdmin can see elsewhere in the portal.
  async function handleExport(entity: ExportEntity, filters?: MemberReportExportFilters) {
    setExporting(entity);
    try {
      await exportReportCsv(entity, filters);
      toast.success(`${entity[0].toUpperCase()}${entity.slice(1)} report downloaded`);
    } catch (e) {
      toast.error(handleApiError(e));
    } finally {
      setExporting(null);
    }
  }

  const exportMembers = () => handleExport("members", {
    status: memberStatusFilter || undefined,
    graduationYearFrom: memberYearFrom ? Number(memberYearFrom) : undefined,
    graduationYearTo: memberYearTo ? Number(memberYearTo) : undefined,
    jobTitleContains: memberProfession || undefined,
    locationContains: memberLocation || undefined,
  });

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <div>
        <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Reports &amp; Exports</h1>
        <p className="text-muted-foreground text-[13px] mt-1.5">Evidence for community health, fundraising, and operational follow-up.</p>
      </div>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-5">
        {isLoading ? (
          Array.from({ length: 5 }).map((_, i) => <StatSkeleton key={i} />)
        ) : [
          { label: "Total Members", value: totalMembers.toLocaleString(), icon: Users, color: "text-blue-600" },
          { label: "Total Contributions", value: totalContributions.toLocaleString(), icon: Activity, color: "text-teal-500" },
          { label: "Total Collected", value: formatCurrency(totalCollected), icon: DollarSign, color: "text-success" },
          { label: "Fundraisers", value: totalCampaigns.toLocaleString(), icon: Layers, color: "text-purple-600" },
          { label: "Events", value: totalEvents.toLocaleString(), icon: Calendar, color: "text-indigo-600" },
        ].map((s, i) => (
          <Card key={s.label} className="stagger-item hover:shadow-md transition-shadow" style={{ animationDelay: `${i * 50}ms` }}>
            <CardContent className="p-5 flex items-center gap-3">
              <div className={`${s.color} rounded-xl bg-muted/50 p-2.5`}><s.icon size={20} /></div>
              <div>
                <p className="text-lg font-bold tracking-tight">{s.value}</p>
                <p className="text-[13px] text-muted-foreground">{s.label}</p>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <CardTitle className="text-base">Fundraiser &amp; Dues Performance</CardTitle>
          <Button size="sm" variant="outline" className="gap-1 h-9 px-3.5 w-full sm:w-auto" onClick={() => handleExport("campaigns")} disabled={exporting === "campaigns"}>
            {exporting === "campaigns" ? <><Loader2 size={13} className="animate-spin" />Exporting…</> : <><Download size={13} />Export Fundraisers CSV</>}
          </Button>
        </CardHeader>
        <CardContent className="space-y-3.5">
          {campaignsQuery.isLoading ? (
            <div className="space-y-4">{Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}</div>
          ) : campaigns.length === 0 ? (
            <p className="text-muted-foreground text-center py-4">No fundraisers or dues yet</p>
          ) : (
            campaigns.map((c) => {
              const isMembership = !!c.isMembershipCampaign;
              const pct = isMembership && c.totalEligibleMembers
                ? Math.round((c.paidCount / c.totalEligibleMembers) * 100)
                : c.targetAmount > 0 ? Math.round((c.collectedAmount / c.targetAmount) * 100) : 0;
              return (
                <div key={c.id} className="space-y-1 min-w-0">
                  <div className="flex items-start justify-between gap-3 text-sm min-w-0">
                    <span className="font-medium min-w-0 flex-1 break-words leading-snug">{c.title}</span>
                    <span className="text-muted-foreground shrink-0 whitespace-nowrap text-right">{isMembership ? `${c.paidCount}/${c.totalEligibleMembers ?? '?'} paid (${pct}%)` : `${formatCurrency(c.collectedAmount)} / ${formatCurrency(c.targetAmount)} (${pct}%)`}</span>
                  </div>
                  <Progress value={pct} className="h-2" />
                  <p className="text-xs text-muted-foreground">{c.paidCount} members paid · Status: {c.status}</p>
                </div>
              );
            })
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Fundraiser &amp; Dues Status Breakdown</CardTitle></CardHeader>
        <CardContent className="space-y-2">
          <p className="text-sm">Active: <strong>{activeCampaigns}</strong> · Closed: <strong>{closedCampaigns}</strong> · Total: <strong>{totalCampaigns}</strong></p>
          <Progress value={totalCampaigns > 0 ? Math.round((activeCampaigns / totalCampaigns) * 100) : 0} tone="accent" />
          <p className="text-xs text-muted-foreground">Active share: {totalCampaigns ? Math.round((activeCampaigns / totalCampaigns) * 100) : 0}%</p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <CardTitle className="text-base">Member Roster Export</CardTitle>
            <p className="text-xs text-muted-foreground mt-1">
              Narrow the roster below, then export — e.g. all alumni working in Healthcare, or all alumni based in Kumasi.
            </p>
          </div>
          <Button size="sm" variant="outline" className="gap-1 h-9 px-3.5 w-full sm:w-auto shrink-0" onClick={exportMembers} disabled={exporting === "members"}>
            {exporting === "members" ? <><Loader2 size={13} className="animate-spin" />Exporting…</> : <><Download size={13} />Export Members CSV</>}
          </Button>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="grid grid-cols-1 sm:grid-cols-5 gap-3">
            <div className="space-y-1.5">
              <Label className="text-[11px] text-muted-foreground font-normal">Status</Label>
              <FormSelect value={memberStatusFilter} onValueChange={setMemberStatusFilter} options={MEMBER_STATUS_OPTIONS} placeholder="Any status" />
            </div>
            <div className="space-y-1.5">
              <Label className="text-[11px] text-muted-foreground font-normal">Year from</Label>
              <Input type="number" value={memberYearFrom} onChange={(e) => setMemberYearFrom(e.target.value)} placeholder="e.g. 1980" />
            </div>
            <div className="space-y-1.5">
              <Label className="text-[11px] text-muted-foreground font-normal">Year to</Label>
              <Input type="number" value={memberYearTo} onChange={(e) => setMemberYearTo(e.target.value)} placeholder="e.g. 1995" />
            </div>
            <div className="space-y-1.5">
              <Label className="text-[11px] text-muted-foreground font-normal">Profession contains</Label>
              <Input value={memberProfession} onChange={(e) => setMemberProfession(e.target.value)} placeholder="e.g. Healthcare" />
            </div>
            <div className="space-y-1.5">
              <Label className="text-[11px] text-muted-foreground font-normal">Location contains</Label>
              <Input value={memberLocation} onChange={(e) => setMemberLocation(e.target.value)} placeholder="e.g. Kumasi" />
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">More Exports</CardTitle></CardHeader>
        <CardContent className="flex flex-wrap gap-2.5">
          <Button size="sm" variant="outline" className="gap-1 h-9 px-3.5" onClick={() => handleExport("contributions")} disabled={exporting === "contributions"}>
            {exporting === "contributions" ? <><Loader2 size={13} className="animate-spin" />Exporting…</> : <><Download size={13} />Contributions CSV</>}
          </Button>
          <Button size="sm" variant="outline" className="gap-1 h-9 px-3.5" onClick={() => handleExport("events")} disabled={exporting === "events"}>
            {exporting === "events" ? <><Loader2 size={13} className="animate-spin" />Exporting…</> : <><Download size={13} />Events CSV</>}
          </Button>
          <Button size="sm" variant="outline" className="gap-1 h-9 px-3.5" onClick={() => handleExport("jobs")} disabled={exporting === "jobs"}>
            {exporting === "jobs" ? <><Loader2 size={13} className="animate-spin" />Exporting…</> : <><Download size={13} />Jobs CSV</>}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
