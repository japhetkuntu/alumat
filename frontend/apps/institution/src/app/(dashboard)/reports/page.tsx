"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Download, Users, TrendingUp, Calendar, Briefcase, Activity, Layers, DollarSign, Loader2 } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { StatSkeleton, CardSkeleton } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { getCampaigns, getMembers, getEvents, getJobs, getContributions, getReportSummary } from "@/lib/institution-api";
import { toast } from "sonner";

const MEMBER_STATUS_OPTIONS = [
  { value: "", label: "Any status" },
  { value: "Active", label: "Active" },
  { value: "Pending", label: "Pending" },
  { value: "Suspended", label: "Suspended" },
  { value: "Banned", label: "Banned" },
];

export default function AdminReportsPage() {
  const [memberStatusFilter, setMemberStatusFilter] = useState("");
  const [memberYearFrom, setMemberYearFrom] = useState("");
  const [memberYearTo, setMemberYearTo] = useState("");
  const [memberProfession, setMemberProfession] = useState("");
  const [memberLocation, setMemberLocation] = useState("");

  const summaryQuery = useQuery({
    queryKey: ["report-summary"],
    queryFn: () => getReportSummary(),
  });

  const campaignsQuery = useQuery({
    queryKey: ["report-campaigns"],
    queryFn: () => getCampaigns(1, 100),
  });

  const contributionsQuery = useQuery({
    queryKey: ["report-contributions"],
    queryFn: () => getContributions({ pageSize: 1 }),
  });

  const eventsQuery = useQuery({
    queryKey: ["report-events"],
    queryFn: () => getEvents(1, 1),
  });

  const jobsQuery = useQuery({
    queryKey: ["report-jobs"],
    queryFn: () => getJobs(1, 1),
  });

  const membersQuery = useQuery({
    queryKey: ["report-members"],
    queryFn: () => getMembers({ pageSize: 1 }),
  });

  const membersExportQuery = useQuery({
    queryKey: ["report-members-export", memberStatusFilter, memberYearFrom, memberYearTo, memberProfession, memberLocation],
    queryFn: () => getMembers({
      page: 1,
      pageSize: 2000,
      status: memberStatusFilter || undefined,
      graduationYearFrom: memberYearFrom ? Number(memberYearFrom) : undefined,
      graduationYearTo: memberYearTo ? Number(memberYearTo) : undefined,
      jobTitleContains: memberProfession || undefined,
      locationContains: memberLocation || undefined,
    }),
    enabled: false,
  });

  const contributionsExportQuery = useQuery({
    queryKey: ["report-contributions-export"],
    queryFn: () => getContributions({ page: 1, pageSize: 2000 }),
    enabled: false,
  });

  const eventsExportQuery = useQuery({
    queryKey: ["report-events-export"],
    queryFn: () => getEvents(1, 2000),
    enabled: false,
  });

  const jobsExportQuery = useQuery({
    queryKey: ["report-jobs-export"],
    queryFn: () => getJobs(1, 2000),
    enabled: false,
  });

  useEffect(() => {
    if (summaryQuery.isError) toast.error("Unable to load report summary metrics");
    if (campaignsQuery.isError) toast.error("Unable to load fundraiser details");
    if (membersQuery.isError) toast.error("Unable to load member metrics");
    if (contributionsQuery.isError) toast.error("Unable to load contribution metrics");
    if (eventsQuery.isError) toast.error("Unable to load event metrics");
    if (jobsQuery.isError) toast.error("Unable to load job metrics");
  }, [summaryQuery.isError, campaignsQuery.isError, membersQuery.isError, contributionsQuery.isError, eventsQuery.isError, jobsQuery.isError]);
  const campaigns = campaignsQuery.data?.results ?? [];
  const totalCampaigns = summaryQuery.data?.totalCampaigns ?? 0;
  const activeCampaigns = summaryQuery.data?.activeCampaigns ?? 0;
  const closedCampaigns = summaryQuery.data?.closedCampaigns ?? 0;
  const totalMembers = summaryQuery.data?.totalMembers ?? 0;
  const totalContributions = summaryQuery.data?.totalContributions ?? 0;
  const totalCollected = summaryQuery.data?.totalCollected ?? 0;
  const totalEvents = summaryQuery.data?.totalEvents ?? 0;
  const totalJobs = summaryQuery.data?.totalJobs ?? 0;
  const isLoading = summaryQuery.isLoading || campaignsQuery.isLoading || membersQuery.isLoading || contributionsQuery.isLoading || eventsQuery.isLoading || jobsQuery.isLoading;

  const makeCsv = (rows: Record<string, unknown>[]) => {
    if (rows.length === 0) return "";
    const keys = Object.keys(rows[0]);
    const csv = [
      keys.join(","),
      ...rows.map((row) => keys.map((key) => JSON.stringify(String(row[key] ?? ""))).join(",")),
    ].join("\n");
    return csv;
  };

  const downloadCsv = (filename: string, rows: Record<string, unknown>[]) => {
    const body = makeCsv(rows);
    if (!body) {
      toast.error("No data available for export");
      return;
    }

    const blob = new Blob([body], { type: "text/csv;charset=utf-8;" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    link.click();
    URL.revokeObjectURL(link.href);
    toast.success(`${filename} downloaded`);
  };

  const exportCampaigns = () => {
    downloadCsv(
      "campaigns-report.csv",
      campaigns.map((c) => ({
        id: c.id,
        title: c.title,
        status: c.status,
        targetAmount: c.targetAmount,
        collectedAmount: c.collectedAmount,
        paidCount: c.paidCount,
        yearGroups: c.yearGroups?.join("|") ?? "",
      })),
    );
  };

  const exportMembers = async () => {
    const result = await membersExportQuery.refetch();
    const members = result.data?.results ?? [];
    if (!members.length) { toast.error("No member data to export"); return; }
    downloadCsv(
      "members-report.csv",
      members.map((m) => ({
        id: m.id,
        firstName: m.firstName,
        lastName: m.lastName,
        email: m.email,
        graduationYear: m.graduationYear,
        department: m.departmentName,
        status: m.status,
        jobTitle: m.jobTitle ?? "",
        location: m.location ?? "",
      })),
    );
  };

  const exportContributions = async () => {
    const result = await contributionsExportQuery.refetch();
    const contributions = result.data?.results ?? [];
    if (!contributions.length) { toast.error("No contribution data to export"); return; }
    downloadCsv(
      "contributions-report.csv",
      contributions.map((c) => ({
        id: c.id,
        campaignId: c.campaignId,
        campaignTitle: c.campaignTitle,
        memberId: c.memberId,
        memberName: c.memberName,
        memberEmail: c.memberEmail,
        amount: c.amount,
        paymentMethod: c.paymentMethod,
        status: c.status,
        confirmedAt: c.confirmedAt,
        createdAt: c.createdAt,
      })),
    );
  };

  const exportEvents = async () => {
    const result = await eventsExportQuery.refetch();
    const events = result.data?.results ?? [];
    if (!events.length) { toast.error("No event data to export"); return; }
    downloadCsv(
      "events-report.csv",
      events.map((e) => ({
        id: e.id,
        title: e.title,
        startDate: e.startDate,
        endDate: e.endDate,
        venue: e.venue,
        status: e.status,
        capacity: e.capacity,
      })),
    );
  };

  const exportJobs = async () => {
    const result = await jobsExportQuery.refetch();
    const jobs = result.data?.results ?? [];
    if (!jobs.length) { toast.error("No job data to export"); return; }
    downloadCsv(
      "jobs-report.csv",
      jobs.map((j) => ({
        id: j.id,
        title: j.title,
        company: j.company,
        location: j.location,
        type: j.type,
        status: j.status,
        deadline: j.deadline,
      })),
    );
  };

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
          <Button size="sm" variant="outline" className="gap-1 h-9 px-3.5 w-full sm:w-auto" onClick={exportCampaigns}>
            <Download size={13} />Export Fundraisers CSV
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
          <Progress value={totalCampaigns > 0 ? Math.round((activeCampaigns / totalCampaigns) * 100) : 0} />
          <p className="text-xs text-muted-foreground">Active share: {totalCampaigns ? Math.round((activeCampaigns / totalCampaigns) * 100) : 0}%</p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Member Roster Filters</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <p className="text-xs text-muted-foreground -mt-1">
            Narrow the member roster before exporting — e.g. all alumni working in Healthcare, or all alumni based in Kumasi.
          </p>
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
        <CardHeader><CardTitle className="text-base">Data Exports</CardTitle></CardHeader>
        <CardContent className="flex flex-wrap gap-2.5">
          <Button size="sm" variant="outline" className="gap-1 h-9 px-3.5" onClick={exportCampaigns}>
            <Download size={13} />Fundraisers CSV
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-1 h-9 px-3.5"
            onClick={exportMembers}
            disabled={membersExportQuery.isFetching}
          >
            {membersExportQuery.isFetching ? <><Loader2 size={13} className="animate-spin" />Exporting…</> : <><Download size={13} />Members CSV</>}
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-1 h-9 px-3.5"
            onClick={exportContributions}
            disabled={contributionsExportQuery.isFetching}
          >
            {contributionsExportQuery.isFetching ? <><Loader2 size={13} className="animate-spin" />Exporting…</> : <><Download size={13} />Contributions CSV</>}
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-1 h-9 px-3.5"
            onClick={exportEvents}
            disabled={eventsExportQuery.isFetching}
          >
            {eventsExportQuery.isFetching ? <><Loader2 size={13} className="animate-spin" />Exporting…</> : <><Download size={13} />Events CSV</>}
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-1 h-9 px-3.5"
            onClick={exportJobs}
            disabled={jobsExportQuery.isFetching}
          >
            {jobsExportQuery.isFetching ? <><Loader2 size={13} className="animate-spin" />Exporting…</> : <><Download size={13} />Jobs CSV</>}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

