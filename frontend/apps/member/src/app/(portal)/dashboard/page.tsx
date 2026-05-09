"use client";

import { useQueries, useQuery } from "@tanstack/react-query";
import { CreditCard, Calendar, TrendingUp, ChevronRight, Award, AlertTriangle, CheckCircle2, Clock, ArrowRight } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { StatCard } from "@/components/shared/stat-card";
import { StatSkeleton } from "@/components/ui/skeleton";
import Link from "next/link";
import { formatCurrency, formatDate } from "@/lib/utils";
import {
  getMyCampaigns,
  getMyContributions,
  getEvents,
  getMyRsvps,
  getMyMembershipStatus,
  getMyCurrentYearUnpaidMembershipCampaigns,
  renewMembership,
  getMyProfile,
} from "@/lib/member-api";
import type { Campaign } from "@/types";

export default function MemberDashboardPage() {
  const results = useQueries({
    queries: [
      { queryKey: ["m-campaigns"], queryFn: () => getMyCampaigns(1, 50) },
      { queryKey: ["m-contributions-recent"], queryFn: () => getMyContributions({ pageSize: 500 }) },
      { queryKey: ["m-events", "upcoming"], queryFn: () => getEvents(1, 50, "Upcoming") },
      { queryKey: ["m-rsvps"], queryFn: () => getMyRsvps() },
    ],
  });

  const [campaigns, contributions, events, rsvps] = results;
  const isLoading = results.some((r) => r.isLoading);

  const membershipStatus = useQuery({
    queryKey: ["m-membership-status"],
    queryFn: getMyMembershipStatus,
    retry: false,
    staleTime: 5 * 60 * 1000,
  });

  const unpaidMembershipCampaignsQuery = useQuery({
    queryKey: ["m-membership-current-unpaid"],
    queryFn: getMyCurrentYearUnpaidMembershipCampaigns,
    retry: false,
    staleTime: 5 * 60 * 1000,
  });

  const { data: profile } = useQuery({
    queryKey: ["m-profile"],
    queryFn: getMyProfile,
  });

  const isPensioner = profile?.employmentStatus === "Pensioner";
  const getMemberAmount = (c: Campaign) =>
    isPensioner && c.pensionerAmountPerMember != null ? c.pensionerAmountPerMember : c.amountPerMember;


  const activeCampaigns = (campaigns.data?.results ?? []).filter((c) => c.status === "Active");
  const activeMembershipCampaigns = activeCampaigns.filter((c) => c.isMembershipCampaign);

  const contributionsList = contributions.data?.results ?? [];
  const confirmedContributions = contributionsList.filter((c) => c.status === "Confirmed");
  const paidMembershipCampaignIds = new Set(confirmedContributions.map((c) => c.campaignId));

  const currentYear = new Date().getFullYear();
  const unpaidCurrentMembershipCampaigns = unpaidMembershipCampaignsQuery.data ?? [];
  const futureMembershipCampaigns = activeMembershipCampaigns.filter((c) => c.membershipYear && c.membershipYear > currentYear && !paidMembershipCampaignIds.has(c.id));

  const membershipCampaign = unpaidCurrentMembershipCampaigns[0] ?? null;

  const totalPaid = confirmedContributions.reduce((sum, c) => sum + c.amount, 0);

  const totalPaidThisYear = confirmedContributions
    .filter((c) => new Date(c.createdAt).getFullYear() === currentYear)
    .reduce((sum, c) => sum + c.amount, 0);

  const upcomingEvents = events.data?.results ?? [];
  const upcomingEventsCount = events.data?.totalCount ?? 0;

  const myRsvps = rsvps.data ?? [];
  const myRsvpIds = new Set(myRsvps.map((r) => r.eventId));

  const membershipMessage = membershipStatus.isSuccess
    ? membershipStatus.data.isMembershipActive
      ? `Active until ${formatDate(membershipStatus.data.membershipExpiry ?? "")}`
      : "Your current year membership is unpaid. Pay now to activate your membership."
    : "Loading membership status...";

  // Compute expiry warning for inline banner
  const expiryDaysLeft = (() => {
    if (!membershipStatus.data?.isMembershipActive || !membershipStatus.data.membershipExpiry) return null;
    const days = Math.ceil((new Date(membershipStatus.data.membershipExpiry).getTime() - Date.now()) / (1000 * 60 * 60 * 24));
    return days > 0 && days <= 30 ? days : null;
  })();

  return (
    <div className="p-4 sm:p-6 lg:p-8 space-y-6 sm:space-y-8 max-w-[1800px] mx-auto">
      <header className="space-y-1 animate-in fade-in slide-in-from-bottom-3 duration-700">
        <h1 className="text-3xl font-extrabold tracking-tight text-foreground">
          {profile?.firstName ? `Welcome back, ${profile.firstName}` : "Dashboard"} 👋
        </h1>
        <p className="text-muted-foreground text-sm font-normal">Here&apos;s what&apos;s happening in your alumni community.</p>
      </header>

      {/* Inline membership warning banner */}
      {membershipStatus.isSuccess && !membershipStatus.data.isMembershipActive && (
        <div className="flex items-start gap-3 p-4 rounded-xl bg-destructive/5 border border-destructive/20 text-sm animate-in fade-in duration-500">
          <AlertTriangle size={16} className="text-destructive shrink-0 mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold text-destructive">Membership inactive</p>
            <p className="text-muted-foreground mt-0.5">Pay your current year&apos;s membership campaign to activate your membership and access all benefits.</p>
          </div>
        </div>
      )}
      {expiryDaysLeft !== null && (
        <div className="flex items-start gap-3 p-4 rounded-xl bg-amber-500/5 border border-amber-500/20 text-sm animate-in fade-in duration-500">
          <AlertTriangle size={16} className="text-amber-600 shrink-0 mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold text-amber-700 dark:text-amber-400">Membership expiring soon</p>
            <p className="text-muted-foreground mt-0.5">Your membership expires in {expiryDaysLeft} day{expiryDaysLeft === 1 ? "" : "s"}. Renew to continue enjoying benefits.</p>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 sm:gap-6">
        {isLoading ? (
          Array.from({ length: 4 }).map((_, i) => <StatSkeleton key={i} />)
        ) : (
          <>
            <div className="animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100"><StatCard title="Active Campaigns" value={activeCampaigns.length} subtitle="Open for contributions" icon={TrendingUp} iconClassName="bg-primary/10 text-primary" /></div>
            <div className="animate-in fade-in slide-in-from-bottom-4 duration-700 delay-200"><StatCard title="Total Contributed" value={formatCurrency(totalPaid)} subtitle="All-time confirmed" icon={CreditCard} iconClassName="bg-primary/10 text-primary" /></div>
            <div className="animate-in fade-in slide-in-from-bottom-4 duration-700 delay-300"><StatCard title="This Year" value={formatCurrency(totalPaidThisYear)} subtitle="Contributed this year" icon={TrendingUp} iconClassName="bg-primary/10 text-primary" /></div>
            <div className="animate-in fade-in slide-in-from-bottom-4 duration-700 delay-400"><StatCard title="Upcoming Events" value={upcomingEventsCount} subtitle="Events you can join" icon={Calendar} iconClassName="bg-primary/10 text-primary" /></div>
          </>
        )}
      </div>

      {/* ── Membership status card ──────────────────── */}
      <Card className="overflow-hidden">
        <CardHeader className="pb-3">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="space-y-0.5">
              <CardTitle className="text-base">Membership Status</CardTitle>
              <p className="text-xs text-muted-foreground">{membershipMessage}</p>
            </div>
            <div className="flex items-center gap-2 flex-shrink-0">
              {paidMembershipCampaignIds.size > 0 && (
                <Link href="/membership-certificate">
                  <Button size="sm" variant="ghost" className="text-xs font-bold gap-1 text-primary hover:text-primary hover:bg-primary/5">
                    <Award size={14} />Certificate
                  </Button>
                </Link>
              )}
              <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold ${
                membershipStatus.data?.isMembershipActive
                  ? "bg-green-500/10 text-green-700 dark:text-green-400 ring-1 ring-green-500/25"
                  : "bg-destructive/10 text-destructive ring-1 ring-destructive/25"
              }`}>
                {membershipStatus.data?.isMembershipActive
                  ? <><CheckCircle2 size={11} />Active</>
                  : <><Clock size={11} />Inactive</>}
              </span>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {membershipCampaign ? (
            <div className="flex flex-wrap items-center justify-between gap-3 p-3 rounded-xl bg-muted/40 border border-border/60">
              <div className="space-y-0.5">
                <p className="text-sm font-semibold text-foreground">{membershipCampaign.title}</p>
                <p className="text-xs text-muted-foreground">
                  {formatCurrency(getMemberAmount(membershipCampaign))}{isPensioner ? " (pensioner rate)" : ""} · Due {formatDate(membershipCampaign.deadline)}
                </p>
              </div>
              <Link href={`/contributions/${membershipCampaign.id}`}>
                <Button size="sm" className="gap-1.5 shrink-0">
                  Pay now <ArrowRight size={13} />
                </Button>
              </Link>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">No active membership campaign right now.</p>
          )}
        </CardContent>
      </Card>

      {/* ── Arrears warning — active but past-year campaigns unpaid ─── */}
      {membershipStatus.data?.isMembershipActive && membershipStatus.data?.hasArrears && (
        <div className="rounded-2xl border border-amber-500/30 bg-amber-500/5 p-4 sm:p-5 space-y-4">
          <div className="flex items-start gap-3">
            <div className="w-9 h-9 rounded-xl bg-amber-500/15 flex items-center justify-center flex-shrink-0">
              <AlertTriangle size={17} className="text-amber-600 dark:text-amber-400" />
            </div>
            <div className="space-y-1 min-w-0">
              <p className="text-sm font-bold text-amber-700 dark:text-amber-400">Outstanding Membership Arrears</p>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Your {new Date().getFullYear()} membership is active, but you have{" "}
                <span className="font-semibold text-foreground">{membershipStatus.data.arrearsCount} unpaid year{membershipStatus.data.arrearsCount !== 1 ? "s" : ""}</span>.
                Clearing arrears keeps your record in good standing.
              </p>
            </div>
          </div>

          {/* Year pills */}
          <div className="flex flex-wrap gap-2">
            {membershipStatus.data.arrearsYears.map((year) => (
              <span
                key={year}
                className="inline-flex items-center px-3 py-1 rounded-full bg-amber-500/15 text-amber-700 dark:text-amber-300 text-xs font-bold ring-1 ring-amber-500/25"
              >
                {year}
              </span>
            ))}
          </div>

          <Link href="/contributions">
            <Button
              size="sm"
              className="w-full sm:w-auto gap-1.5 bg-amber-600 hover:bg-amber-700 text-white border-0"
            >
              Clear arrears <ArrowRight size={13} />
            </Button>
          </Link>
        </div>
      )}

      {unpaidCurrentMembershipCampaigns.length > 0 && (
        <Card>
          <CardHeader className="pb-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <CardTitle className="text-base">Unpaid Membership Campaigns</CardTitle>
                <p className="text-xs text-muted-foreground mt-0.5">Pay these to activate or maintain your membership</p>
              </div>
              <span className="inline-flex items-center justify-center w-6 h-6 rounded-full bg-destructive/10 text-destructive text-xs font-bold ring-1 ring-destructive/20">
                {unpaidCurrentMembershipCampaigns.length}
              </span>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {unpaidCurrentMembershipCampaigns.map((c) => (
              <div key={c.id} className="flex flex-wrap items-center justify-between gap-3 p-3 rounded-xl border border-border/70 bg-muted/20">
                <div className="min-w-0">
                  <p className="text-sm font-semibold text-foreground truncate">{c.title}</p>
                  <p className="text-xs text-muted-foreground">
                    {formatCurrency(getMemberAmount(c))}{isPensioner ? " (pensioner rate)" : ""} · Due {formatDate(c.deadline)}
                  </p>
                </div>
                <Link href={`/contributions/${c.id}`} className="shrink-0">
                  <Button size="sm" className="gap-1.5">Pay now <ArrowRight size={13} /></Button>
                </Link>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {futureMembershipCampaigns.length > 0 && (
        <Card>
          <CardHeader className="pb-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <CardTitle className="text-base">Early Renewal Available</CardTitle>
                <p className="text-xs text-muted-foreground mt-0.5">Optional — pay ahead to secure future membership years</p>
              </div>
              <span className="inline-flex items-center justify-center w-6 h-6 rounded-full bg-primary/10 text-primary text-xs font-bold ring-1 ring-primary/20">
                {futureMembershipCampaigns.length}
              </span>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {futureMembershipCampaigns.map((c) => (
              <div key={c.id} className="flex flex-wrap items-center justify-between gap-3 p-3 rounded-xl border border-border/70 bg-muted/20">
                <div className="min-w-0">
                  <p className="text-sm font-semibold text-foreground truncate">{c.title}</p>
                  <p className="text-xs text-muted-foreground">
                    {c.membershipYear} · {formatCurrency(getMemberAmount(c))}{isPensioner ? " (pensioner rate)" : ""}
                  </p>
                </div>
                <Link href={`/contributions/${c.id}`} className="shrink-0">
                  <Button size="sm" variant="outline" className="gap-1.5">View <ArrowRight size={13} /></Button>
                </Link>
              </div>
            ))}
          </CardContent>
        </Card>
      )}


      {activeCampaigns.length > 0 && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-3">
            <CardTitle className="text-base">Active Campaigns</CardTitle>
            <Link href="/contributions"><Button size="sm" variant="ghost" className="text-xs gap-1">View all <ChevronRight size={12} /></Button></Link>
          </CardHeader>
          <CardContent className="space-y-5">
            {activeCampaigns.map((c) => {
              const isMembership = !!c.isMembershipCampaign;
              const pct = isMembership && c.totalEligibleMembers
                ? Math.round((c.paidCount / c.totalEligibleMembers) * 100)
                : c.targetAmount > 0 ? Math.round((c.collectedAmount / c.targetAmount) * 100) : 0;
              return (
                <div key={c.id}>
                  <div className="flex items-start justify-between gap-3 mb-1 min-w-0">
                    <span className="font-medium text-sm min-w-0 flex-1 leading-snug break-words">{c.title}</span>
                    <span className="text-sm font-semibold shrink-0 text-right whitespace-nowrap">{isMembership ? formatCurrency(getMemberAmount(c)) : formatCurrency(c.amountPerMember)}</span>
                  </div>
                  <Progress value={pct} className="h-2" />
                  <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 mt-1 min-w-0">
                    <span className="text-xs text-muted-foreground break-words">{isMembership ? `${c.paidCount}/${c.totalEligibleMembers ?? '?'} members paid` : `${pct}% of target reached`}</span>
                    <span className="text-xs text-muted-foreground whitespace-nowrap">Due {formatDate(c.deadline)}</span>
                  </div>
                </div>
              );
            })}
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 sm:gap-6 lg:gap-8">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-6 border-b border-border/30">
            <div className="space-y-1">
              <CardTitle className="text-lg font-bold">Upcoming Events</CardTitle>
              <CardDescription>Join your fellow alumni at these events</CardDescription>
            </div>
            <Link href="/events"><Button size="sm" variant="ghost" className="text-xs font-semibold gap-1 hover:bg-primary/5 hover:text-primary">All events <ChevronRight size={14} /></Button></Link>
          </CardHeader>
          <CardContent className="pt-6 space-y-4">
            {upcomingEvents.slice(0, 3).map((e) => (
              <div key={e.id} className="flex items-center justify-between p-3 rounded-xl hover:bg-muted/50 transition-colors group">
                <div className="flex items-center gap-3 sm:gap-4 min-w-0 flex-1">
                  <div className="w-9 h-9 sm:w-10 sm:h-10 rounded-lg bg-primary/5 flex items-center justify-center text-primary group-hover:scale-110 transition-transform shrink-0">
                    <Calendar size={18} />
                  </div>
                  <div className="min-w-0">
                    <p className="text-sm font-bold group-hover:text-primary transition-colors truncate">{e.title}</p>
                    <p className="text-[12px] text-muted-foreground font-medium truncate">{formatDate(e.startDate)} · {e.venue}</p>
                  </div>
                </div>
                {myRsvpIds.has(e.id)
                  ? <Badge variant="success" className="font-bold">RSVPd</Badge>
                  : <Badge variant="outline" className="font-bold">Open</Badge>}
              </div>
            ))}
            {upcomingEvents.length === 0 && !isLoading && (
              <div className="py-12 text-center space-y-2">
                <div className="w-12 h-12 bg-muted/30 rounded-full flex items-center justify-center mx-auto text-muted-foreground/50">
                  <Calendar size={20} />
                </div>
                <p className="text-sm text-muted-foreground font-medium">No upcoming events found</p>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-6 border-b border-border/30">
            <div className="space-y-1">
              <CardTitle className="text-lg font-bold">Recent Activity</CardTitle>
              <CardDescription>Your latest contributions and updates</CardDescription>
            </div>
            <Link href="/contributions"><Button size="sm" variant="ghost" className="text-xs font-semibold gap-1 hover:bg-primary/5 hover:text-primary">History <ChevronRight size={14} /></Button></Link>
          </CardHeader>
          <CardContent className="pt-6 space-y-4">
            {(contributions.data?.results ?? []).slice(0, 5).map((c) => (
              <div key={c.id} className="flex items-center justify-between p-3 rounded-xl hover:bg-muted/50 transition-colors group">
                <div className="flex items-center gap-3 sm:gap-4 min-w-0 flex-1">
                  <div className="w-9 h-9 sm:w-10 sm:h-10 rounded-lg bg-primary/5 flex items-center justify-center text-primary group-hover:scale-110 transition-transform shrink-0">
                    <CreditCard size={18} />
                  </div>
                  <div className="min-w-0">
                    <p className="text-sm font-bold group-hover:text-primary transition-colors truncate">{c.campaignTitle ?? "Campaign Contribution"}</p>
                    <p className="text-[12px] text-muted-foreground font-medium">{formatDate(c.confirmedAt ?? c.createdAt)}</p>
                  </div>
                </div>
                <div className="text-right">
                  <p className="text-sm font-bold">{formatCurrency(c.amount)}</p>
                  <Badge variant={c.status === "Confirmed" ? "success" : c.status === "Pending" ? "warning" : "destructive"} className="text-[10px] h-4 font-bold uppercase tracking-wider">{c.status}</Badge>
                </div>
              </div>
            ))}
            {(contributions.data?.results.length === 0) && !isLoading && (
              <div className="py-12 text-center space-y-2">
                <div className="w-12 h-12 bg-muted/30 rounded-full flex items-center justify-center mx-auto text-muted-foreground/50">
                  <TrendingUp size={20} />
                </div>
                <p className="text-sm text-muted-foreground font-medium">No contributions yet</p>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
