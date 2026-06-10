"use client";

import { useQueries, useQuery } from "@tanstack/react-query";
import {
  CreditCard, Calendar, TrendingUp, ChevronRight, Award,
  AlertTriangle, CheckCircle2, Clock, ArrowRight,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { StatSkeleton } from "@/components/ui/skeleton";
import Link from "next/link";
import { formatCurrency, formatDate } from "@/lib/utils";
import { cn } from "@/lib/utils";
import {
  getMyCampaigns,
  getMyContributions,
  getEvents,
  getMyRsvps,
  getMyMembershipStatus,
  getMyCurrentYearUnpaidMembershipCampaigns,
  getMyProfile,
} from "@/lib/member-api";
import type { Campaign } from "@/types";

/* ─────────────────────────────────────────────────────────────────────────
   MEMBERSHIP CARD — looks like a physical card, universally understood
   ───────────────────────────────────────────────────────────────────────── */
function MembershipCard({
  profile,
  membershipStatus,
  membershipCampaign,
  isPensioner,
  getMemberAmount,
}: {
  profile: any;
  membershipStatus: any;
  membershipCampaign: Campaign | null;
  isPensioner: boolean;
  getMemberAmount: (c: Campaign) => number;
}) {
  const isActive = membershipStatus?.isMembershipActive;
  const expiry = membershipStatus?.membershipExpiry;

  const expiryDaysLeft = (() => {
    if (!isActive || !expiry) return null;
    const days = Math.ceil((new Date(expiry).getTime() - Date.now()) / 86_400_000);
    return days > 0 && days <= 30 ? days : null;
  })();

  return (
    <div
      className="relative overflow-hidden rounded-2xl p-6 sm:p-8"
      style={{
        background: isActive
          ? "linear-gradient(135deg, var(--primary) 0%, color-mix(in oklch, var(--primary) 75%, black) 100%)"
          : "linear-gradient(135deg, #4b5563 0%, #1f2937 100%)",
        color: "white",
      }}
    >
      {/* Subtle texture rings */}
      <div className="absolute -right-16 -top-16 w-64 h-64 rounded-full border border-white/10" />
      <div className="absolute -right-8 -top-8 w-40 h-40 rounded-full border border-white/10" />

      <div className="relative flex flex-col sm:flex-row sm:items-start sm:justify-between gap-6">

        {/* Left — identity */}
        <div className="space-y-1">
          <p className="text-white/60 text-[12px] font-semibold tracking-[0.1em] uppercase">
            UMaT Alumni · Member card
          </p>
          <p className="text-[22px] sm:text-[26px] font-bold leading-tight">
            {profile?.firstName
              ? `${profile.firstName} ${profile.lastName ?? ""}`
              : "—"}
          </p>
          {profile?.graduationYear && (
            <p className="text-white/70 text-[14px]">
              Class of {profile.graduationYear}
              {profile.departmentName ? ` · ${profile.departmentName}` : ""}
            </p>
          )}
        </div>

        {/* Right — status */}
        <div className="flex flex-col items-start sm:items-end gap-3 shrink-0">
          <div
            className={cn(
              "inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[13px] font-bold",
              isActive
                ? "bg-white/20 text-white"
                : "bg-white/15 text-white/80",
            )}
          >
            {isActive
              ? <><CheckCircle2 size={13} /> Active</>
              : <><Clock size={13} /> Inactive</>}
          </div>
          {isActive && expiry && (
            <p className="text-white/60 text-[12px]">
              Valid until {formatDate(expiry)}
            </p>
          )}
          {/* Certificate link — only when active */}
          {isActive && (
            <Link href="/membership-certificate">
              <button className="flex items-center gap-1.5 text-[12px] text-white/70 hover:text-white transition-colors">
                <Award size={13} /> View certificate
              </button>
            </Link>
          )}
        </div>
      </div>

      {/* Action area — unpaid dues */}
      {membershipCampaign && !isActive && (
        <div className="relative mt-6 pt-5 border-t border-white/20">
          <p className="text-white/80 text-[14px] mb-3">
            Your membership is inactive. Pay your dues to activate it.
          </p>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p className="text-white font-bold text-[18px]">
                {formatCurrency(getMemberAmount(membershipCampaign))}
                {isPensioner ? (
                  <span className="text-white/60 text-[12px] font-normal ml-1.5">pensioner rate</span>
                ) : null}
              </p>
              <p className="text-white/60 text-[12px]">
                Due {formatDate(membershipCampaign.deadline)}
              </p>
            </div>
            <Link href={`/contributions/${membershipCampaign.id}`}>
              <Button
                className="bg-white font-bold gap-2 hover:bg-white/90"
                style={{ color: "var(--primary)", height: 44 }}
              >
                Pay now <ArrowRight size={15} />
              </Button>
            </Link>
          </div>
        </div>
      )}

      {/* Expiry warning — active but expiring soon */}
      {isActive && expiryDaysLeft !== null && (
        <div className="relative mt-5 pt-5 border-t border-white/20 flex flex-wrap items-center justify-between gap-3">
          <p className="text-white/80 text-[14px]">
            Expires in <span className="font-bold text-white">{expiryDaysLeft} day{expiryDaysLeft !== 1 ? "s" : ""}</span>. Renew now to stay active.
          </p>
          {membershipCampaign && (
            <Link href={`/contributions/${membershipCampaign.id}`}>
              <Button className="bg-white font-bold hover:bg-white/90" style={{ color: "var(--primary)", height: 40 }}>
                Renew
              </Button>
            </Link>
          )}
        </div>
      )}

      {/* Active + all good */}
      {isActive && expiryDaysLeft === null && !membershipCampaign && (
        <div className="relative mt-5 pt-5 border-t border-white/20">
          <p className="text-white/70 text-[14px] flex items-center gap-2">
            <CheckCircle2 size={15} className="text-white/60" />
            You're all set for this year.
          </p>
        </div>
      )}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   ARREARS BANNER
   ───────────────────────────────────────────────────────────────────────── */
function ArrearsBanner({
  membershipStatus,
}: {
  membershipStatus: any;
}) {
  if (!membershipStatus?.isMembershipActive || !membershipStatus?.hasArrears) return null;
  return (
    <div
      className="rounded-2xl p-5 sm:p-6 space-y-4"
      style={{
        background: "rgba(245,158,11,0.07)",
        border: "1px solid rgba(245,158,11,0.3)",
      }}
    >
      <div className="flex items-start gap-4">
        <div
          className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0"
          style={{ background: "rgba(245,158,11,0.15)" }}
        >
          <AlertTriangle size={18} style={{ color: "#d97706" }} />
        </div>
        <div>
          <p className="font-bold text-[15px]" style={{ color: "var(--foreground)" }}>
            You have unpaid dues from previous years
          </p>
          <p className="text-[13.5px] mt-1 leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
            Your current membership is active, but you have{" "}
            <span className="font-semibold" style={{ color: "var(--foreground)" }}>
              {membershipStatus.arrearsCount} unpaid year{membershipStatus.arrearsCount !== 1 ? "s" : ""}
            </span>
            . Clearing them keeps your record in good standing.
          </p>
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex flex-wrap gap-2">
          {membershipStatus.arrearsYears.map((year: number) => (
            <span
              key={year}
              className="inline-flex items-center px-3 py-1 rounded-full text-[12px] font-bold"
              style={{
                background: "rgba(245,158,11,0.15)",
                color: "#b45309",
                border: "1px solid rgba(245,158,11,0.3)",
              }}
            >
              {year}
            </span>
          ))}
        </div>
        <Link href="/contributions">
          <Button
            size="sm"
            className="gap-1.5 font-semibold"
            style={{ background: "#d97706", border: "none", color: "white", height: 38 }}
          >
            Clear arrears <ArrowRight size={13} />
          </Button>
        </Link>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   DASH STAT — self-contained tile, no overflow surprises
   ───────────────────────────────────────────────────────────────────────── */
function DashStat({
  label,
  value,
  sub,
  icon: Icon,
}: {
  label: string;
  value: string | number;
  sub: string;
  icon: React.ElementType;
}) {
  return (
    <div
      className="rounded-2xl border p-4 flex flex-col gap-3 min-w-0"
      style={{ background: "var(--background)", borderColor: "var(--border)" }}
    >
      {/* Icon */}
      <div
        className="w-9 h-9 rounded-xl flex items-center justify-center shrink-0"
        style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
      >
        <Icon size={16} style={{ color: "var(--primary)" }} />
      </div>

      {/* Value — clamps to one line, scales down if needed */}
      <p
        className="font-[family-name:var(--font-display)] font-bold leading-none tabular-nums"
        style={{
          fontSize: "clamp(1.25rem, 3.5vw, 1.65rem)",
          color: "var(--foreground)",
          letterSpacing: "-0.02em",
          wordBreak: "break-word",
          overflowWrap: "anywhere",
        }}
      >
        {value}
      </p>

      {/* Label + sub */}
      <div className="mt-auto pt-2 border-t" style={{ borderColor: "var(--border)" }}>
        <p className="text-[13px] font-semibold leading-tight" style={{ color: "var(--foreground)" }}>
          {label}
        </p>
        <p className="text-[11.5px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
          {sub}
        </p>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   CAMPAIGN ROW
   ───────────────────────────────────────────────────────────────────────── */
function CampaignRow({
  campaign,
  amount,
  isPensioner,
  href,
  variant = "pay",
}: {
  campaign: Campaign;
  amount: number;
  isPensioner: boolean;
  href: string;
  variant?: "pay" | "view";
}) {
  return (
    <div
      className="flex flex-wrap items-center justify-between gap-4 p-4 rounded-xl border"
      style={{ borderColor: "var(--border)", background: "var(--background)" }}
    >
      <div className="min-w-0 flex-1">
        <p className="text-[14.5px] font-semibold leading-snug" style={{ color: "var(--foreground)" }}>
          {campaign.title}
        </p>
        <p className="text-[13px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
          {formatCurrency(amount)}
          {isPensioner ? " · pensioner rate" : ""} · Due {formatDate(campaign.deadline)}
        </p>
      </div>
      <Link href={href} className="shrink-0">
        <Button
          size="sm"
          variant={variant === "view" ? "outline" : "default"}
          className="gap-1.5 font-semibold"
          style={{ height: 38 }}
        >
          {variant === "pay" ? "Pay now" : "View"}
          <ArrowRight size={13} />
        </Button>
      </Link>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PAGE
   ───────────────────────────────────────────────────────────────────────── */
export default function MemberDashboardPage() {
  const results = useQueries({
    queries: [
      { queryKey: ["m-campaigns"],             queryFn: () => getMyCampaigns(1, 50)                    },
      { queryKey: ["m-contributions-recent"],  queryFn: () => getMyContributions({ pageSize: 500 })    },
      { queryKey: ["m-events", "upcoming"],    queryFn: () => getEvents(1, 50, "Upcoming")             },
      { queryKey: ["m-rsvps"],                 queryFn: () => getMyRsvps()                             },
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
    isPensioner && c.pensionerAmountPerMember != null
      ? c.pensionerAmountPerMember
      : c.amountPerMember;

  const currentYear = new Date().getFullYear();
  const activeCampaigns = (campaigns.data?.results ?? []).filter((c) => c.status === "Active");
  const contributionsList = contributions.data?.results ?? [];
  const confirmedContributions = contributionsList.filter((c) => c.status === "Confirmed");
  const paidMembershipCampaignIds = new Set(confirmedContributions.map((c) => c.campaignId));

  const unpaidCurrentMembershipCampaigns = unpaidMembershipCampaignsQuery.data ?? [];
  const membershipCampaign = unpaidCurrentMembershipCampaigns[0] ?? null;

  const activeMembershipCampaigns = activeCampaigns.filter((c) => c.isMembershipCampaign);
  const futureMembershipCampaigns = activeMembershipCampaigns.filter(
    (c) => c.membershipYear && c.membershipYear > currentYear && !paidMembershipCampaignIds.has(c.id),
  );

  const totalPaid = confirmedContributions.reduce((sum, c) => sum + c.amount, 0);
  const totalPaidThisYear = confirmedContributions
    .filter((c) => new Date(c.createdAt).getFullYear() === currentYear)
    .reduce((sum, c) => sum + c.amount, 0);

  const upcomingEvents = events.data?.results ?? [];
  const upcomingEventsCount = events.data?.totalCount ?? 0;
  const myRsvpIds = new Set((rsvps.data ?? []).map((r: any) => r.eventId));

  const nonMembershipActiveCampaigns = activeCampaigns.filter((c) => !c.isMembershipCampaign);

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      {/* ── Greeting ── */}
      <div className="animate-in fade-in slide-in-from-bottom-3 duration-500">
        <h1
          className="font-[family-name:var(--font-display)] tracking-tight"
          style={{ fontSize: "clamp(1.5rem, 3vw, 2rem)", fontWeight: 700, color: "var(--foreground)" }}
        >
          {profile?.firstName ? `Welcome back, ${profile.firstName}.` : "Welcome back."}
        </h1>
        <p className="mt-1 text-[14px]" style={{ color: "var(--muted-foreground)" }}>
          Here's what's happening in your alumni community.
        </p>
      </div>

      {/* ── Membership card ── */}
      <div className="animate-in fade-in slide-in-from-bottom-3 duration-500 delay-75">
        <MembershipCard
          profile={profile}
          membershipStatus={membershipStatus.data}
          membershipCampaign={membershipCampaign}
          isPensioner={isPensioner}
          getMemberAmount={getMemberAmount}
        />
      </div>

      {/* ── Arrears banner ── */}
      {membershipStatus.isSuccess && (
        <ArrearsBanner membershipStatus={membershipStatus.data} />
      )}

      {/* ── Stat tiles ── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 animate-in fade-in duration-500 delay-100">
        {isLoading
          ? Array.from({ length: 4 }).map((_, i) => <StatSkeleton key={i} />)
          : (
            <>
              <DashStat
                label="Active campaigns"
                value={activeCampaigns.length}
                sub="Open for contributions"
                icon={TrendingUp}
              />
              <DashStat
                label="Total contributed"
                value={formatCurrency(totalPaid)}
                sub="All-time confirmed"
                icon={CreditCard}
              />
              <DashStat
                label="This year"
                value={formatCurrency(totalPaidThisYear)}
                sub={`Contributed in ${currentYear}`}
                icon={TrendingUp}
              />
              <DashStat
                label="Upcoming events"
                value={upcomingEventsCount}
                sub="Events you can join"
                icon={Calendar}
              />
            </>
          )}
      </div>

      {/* ── Unpaid current-year membership campaigns ── */}
      {unpaidCurrentMembershipCampaigns.length > 0 && (
        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-[15px] font-bold" style={{ color: "var(--foreground)" }}>
                Dues to pay
              </h2>
              <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
                Pay these to activate or renew your membership
              </p>
            </div>
            <span
              className="w-6 h-6 rounded-full flex items-center justify-center text-[11px] font-bold text-destructive"
              style={{ background: "rgba(239,68,68,0.1)", border: "1px solid rgba(239,68,68,0.2)" }}
            >
              {unpaidCurrentMembershipCampaigns.length}
            </span>
          </div>
          <div className="space-y-2">
            {unpaidCurrentMembershipCampaigns.map((c) => (
              <CampaignRow
                key={c.id}
                campaign={c}
                amount={getMemberAmount(c)}
                isPensioner={isPensioner}
                href={`/contributions/${c.id}`}
                variant="pay"
              />
            ))}
          </div>
        </section>
      )}

      {/* ── Future (early renewal) campaigns ── */}
      {futureMembershipCampaigns.length > 0 && (
        <section className="space-y-3">
          <div>
            <h2 className="text-[15px] font-bold" style={{ color: "var(--foreground)" }}>
              Early renewal
            </h2>
            <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
              Optional — pay ahead to secure upcoming membership years
            </p>
          </div>
          <div className="space-y-2">
            {futureMembershipCampaigns.map((c) => (
              <CampaignRow
                key={c.id}
                campaign={c}
                amount={getMemberAmount(c)}
                isPensioner={isPensioner}
                href={`/contributions/${c.id}`}
                variant="view"
              />
            ))}
          </div>
        </section>
      )}

      {/* ── Active non-membership campaigns ── */}
      {nonMembershipActiveCampaigns.length > 0 && (
        <section>
          <Card>
            <CardHeader className="flex flex-row items-center justify-between pb-4">
              <div>
                <CardTitle className="text-[15px] font-bold">Open campaigns</CardTitle>
                <p className="text-[13px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                  Alumni-led campaigns you can contribute to
                </p>
              </div>
              <Link href="/contributions">
                <Button size="sm" variant="ghost" className="text-[13px] gap-1 font-semibold">
                  All <ChevronRight size={13} />
                </Button>
              </Link>
            </CardHeader>
            <CardContent className="space-y-5 pt-0">
              {nonMembershipActiveCampaigns.map((c) => {
                const pct = c.targetAmount > 0
                  ? Math.round((c.collectedAmount / c.targetAmount) * 100)
                  : 0;
                return (
                  <div key={c.id}>
                    <div className="flex items-start justify-between gap-3 mb-2">
                      <p className="text-[14px] font-semibold flex-1 leading-snug" style={{ color: "var(--foreground)" }}>
                        {c.title}
                      </p>
                      <p className="text-[14px] font-bold shrink-0" style={{ color: "var(--primary)" }}>
                        {formatCurrency(c.amountPerMember)}
                      </p>
                    </div>
                    <Progress value={pct} className="h-2 mb-1.5" />
                    <div className="flex items-center justify-between text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>
                      <span>{pct}% of target reached</span>
                      <span>Due {formatDate(c.deadline)}</span>
                    </div>
                  </div>
                );
              })}
            </CardContent>
          </Card>
        </section>
      )}

      {/* ── Events + Recent activity ── */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 sm:gap-6">

        {/* Events */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-4 border-b" style={{ borderColor: "var(--border)" }}>
            <div>
              <CardTitle className="text-[15px] font-bold">Upcoming events</CardTitle>
              <p className="text-[13px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                Events open to all alumni
              </p>
            </div>
            <Link href="/events">
              <Button size="sm" variant="ghost" className="text-[13px] gap-1 font-semibold">
                All <ChevronRight size={13} />
              </Button>
            </Link>
          </CardHeader>
          <CardContent className="pt-4 space-y-1">
            {upcomingEvents.slice(0, 3).map((e: any) => (
              <div
                key={e.id}
                className="flex items-center gap-4 p-3 rounded-xl transition-colors hover:bg-secondary group"
              >
                <div
                  className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0 transition-transform group-hover:scale-105"
                  style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
                >
                  <Calendar size={17} style={{ color: "var(--primary)" }} />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-[14px] font-semibold leading-snug truncate" style={{ color: "var(--foreground)" }}>
                    {e.title}
                  </p>
                  <p className="text-[12.5px] mt-0.5 truncate" style={{ color: "var(--muted-foreground)" }}>
                    {formatDate(e.startDate)}{e.venue ? ` · ${e.venue}` : ""}
                  </p>
                </div>
                {myRsvpIds.has(e.id)
                  ? <Badge variant="success" className="text-[11px] font-bold shrink-0">Going</Badge>
                  : <Badge variant="outline" className="text-[11px] font-bold shrink-0">Open</Badge>}
              </div>
            ))}
            {upcomingEvents.length === 0 && !isLoading && (
              <div className="py-10 text-center space-y-2">
                <div
                  className="w-12 h-12 rounded-full flex items-center justify-center mx-auto"
                  style={{ background: "var(--secondary)" }}
                >
                  <Calendar size={20} style={{ color: "var(--muted-foreground)" }} />
                </div>
                <p className="text-[14px]" style={{ color: "var(--muted-foreground)" }}>
                  No upcoming events yet
                </p>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Recent contributions */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-4 border-b" style={{ borderColor: "var(--border)" }}>
            <div>
              <CardTitle className="text-[15px] font-bold">Recent payments</CardTitle>
              <p className="text-[13px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                Your latest contributions
              </p>
            </div>
            <Link href="/contributions">
              <Button size="sm" variant="ghost" className="text-[13px] gap-1 font-semibold">
                History <ChevronRight size={13} />
              </Button>
            </Link>
          </CardHeader>
          <CardContent className="pt-4 space-y-1">
            {contributionsList.slice(0, 5).map((c: any) => (
              <div
                key={c.id}
                className="flex items-center gap-4 p-3 rounded-xl transition-colors hover:bg-secondary group"
              >
                <div
                  className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0 transition-transform group-hover:scale-105"
                  style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
                >
                  <CreditCard size={17} style={{ color: "var(--primary)" }} />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-[14px] font-semibold leading-snug truncate" style={{ color: "var(--foreground)" }}>
                    {c.campaignTitle ?? "Contribution"}
                  </p>
                  <p className="text-[12.5px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                    {formatDate(c.confirmedAt ?? c.createdAt)}
                  </p>
                </div>
                <div className="text-right shrink-0">
                  <p className="text-[14px] font-bold" style={{ color: "var(--foreground)" }}>
                    {formatCurrency(c.amount)}
                  </p>
                  <Badge
                    variant={c.status === "Confirmed" ? "success" : c.status === "Pending" ? "warning" : "destructive"}
                    className="text-[10px] font-bold uppercase tracking-wide mt-0.5"
                  >
                    {c.status}
                  </Badge>
                </div>
              </div>
            ))}
            {contributionsList.length === 0 && !isLoading && (
              <div className="py-10 text-center space-y-2">
                <div
                  className="w-12 h-12 rounded-full flex items-center justify-center mx-auto"
                  style={{ background: "var(--secondary)" }}
                >
                  <CreditCard size={20} style={{ color: "var(--muted-foreground)" }} />
                </div>
                <p className="text-[14px]" style={{ color: "var(--muted-foreground)" }}>
                  No contributions yet
                </p>
                <Link href="/contributions">
                  <Button size="sm" variant="outline" className="mt-1 font-semibold">
                    Browse campaigns
                  </Button>
                </Link>
              </div>
            )}
          </CardContent>
        </Card>

      </div>
    </div>
  );
}
