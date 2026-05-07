"use client";

import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { Download, Users, TrendingUp, Calendar, Briefcase, Activity, Layers, DollarSign } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { StatSkeleton, CardSkeleton } from "@/components/ui/skeleton";
import { formatCurrency } from "@/lib/utils";
import { getCampaigns, getMembers, getEvents, getJobs, getContributions, getReportSummary } from "@/lib/admin-api";
import { toast } from "sonner";

export default function AdminReportsPage() {
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
    queryKey: ["report-members-export"],
    queryFn: () => getMembers({ page: 1, pageSize: 2000 }),
    enabled: !membersQuery.isLoading,
  });

  const contributionsExportQuery = useQuery({
    queryKey: ["report-contributions-export"],
    queryFn: () => getContributions({ page: 1, pageSize: 2000 }),
    enabled: !contributionsQuery.isLoading,
  });

  const eventsExportQuery = useQuery({
    queryKey: ["report-events-export"],
    queryFn: () => getEvents(1, 2000),
    enabled: !eventsQuery.isLoading,
  });

  const jobsExportQuery = useQuery({
    queryKey: ["report-jobs-export"],
    queryFn: () => getJobs(1, 2000),
    enabled: !jobsQuery.isLoading,
  });

  useEffect(() => {
    if (summaryQuery.isError) toast.error("Unable to load report summary metrics");
    if (campaignsQuery.isError) toast.error("Unable to load campaign details");
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

  const exportMembers = () => {
    const members = membersExportQuery.data?.results ?? [];
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
      })),
    );
  };

  const exportContributions = () => {
    const contributions = contributionsExportQuery.data?.results ?? [];
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

  const exportEvents = () => {
    const events = eventsExportQuery.data?.results ?? [];
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

  const exportJobs = () => {
    const jobs = jobsExportQuery.data?.results ?? [];
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
    <div className="p-6 lg:p-8 space-y-6 page-enter">
      <div>
        <h1 className="text-[22px] font-bold tracking-tight">Reports</h1>
        <p className="text-muted-foreground text-[13px] mt-1">Overview of platform activity and data exports</p>
      </div>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-5">
        {isLoading ? (
          Array.from({ length: 5 }).map((_, i) => <StatSkeleton key={i} />)
        ) : [
          { label: "Total Members", value: totalMembers.toLocaleString(), icon: Users, color: "text-blue-600" },
          { label: "Total Contributions", value: totalContributions.toLocaleString(), icon: Activity, color: "text-teal-500" },
          { label: "Total Collected", value: formatCurrency(totalCollected), icon: DollarSign, color: "text-green-600" },
          { label: "Campaigns", value: totalCampaigns.toLocaleString(), icon: Layers, color: "text-purple-600" },
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
          <CardTitle className="text-base">Campaign Performance</CardTitle>
          <Button size="sm" variant="outline" className="gap-1 h-9 px-3.5 w-full sm:w-auto" onClick={exportCampaigns}>
            <Download size={13} />Export Campaigns CSV
          </Button>
        </CardHeader>
        <CardContent className="space-y-3.5">
          {campaignsQuery.isLoading ? (
            <div className="space-y-4">{Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}</div>
          ) : campaigns.length === 0 ? (
            <p className="text-muted-foreground text-center py-4">No campaigns yet</p>
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
        <CardHeader><CardTitle className="text-base">Campaign Status Breakdown</CardTitle></CardHeader>
        <CardContent className="space-y-2">
          <p className="text-sm">Active: <strong>{activeCampaigns}</strong> · Closed: <strong>{closedCampaigns}</strong> · Total: <strong>{totalCampaigns}</strong></p>
          <Progress value={totalCampaigns > 0 ? Math.round((activeCampaigns / totalCampaigns) * 100) : 0} />
          <p className="text-xs text-muted-foreground">Active campaign share: {totalCampaigns ? Math.round((activeCampaigns / totalCampaigns) * 100) : 0}%</p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Data Exports</CardTitle></CardHeader>
        <CardContent className="flex flex-wrap gap-2.5">
          <Button size="sm" variant="outline" className="gap-1 h-9 px-3.5" onClick={exportCampaigns}>
            <Download size={13} />Campaigns CSV
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-1 h-9 px-3.5"
            onClick={exportMembers}
            disabled={membersExportQuery.isLoading || !membersExportQuery.data?.results?.length}
          >
            <Download size={13} />Members CSV
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-1 h-9 px-3.5"
            onClick={exportContributions}
            disabled={contributionsExportQuery.isLoading || !contributionsExportQuery.data?.results?.length}
          >
            <Download size={13} />Contributions CSV
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-1 h-9 px-3.5"
            onClick={exportEvents}
            disabled={eventsExportQuery.isLoading || !eventsExportQuery.data?.results?.length}
          >
            <Download size={13} />Events CSV
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-1 h-9 px-3.5"
            onClick={exportJobs}
            disabled={jobsExportQuery.isLoading || !jobsExportQuery.data?.results?.length}
          >
            <Download size={13} />Jobs CSV
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

