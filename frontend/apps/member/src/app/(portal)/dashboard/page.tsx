"use client";

import { useState } from "react";
import { useQueries, useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  CreditCard, Calendar, TrendingUp, ChevronRight, Award,
  AlertTriangle, CheckCircle2, Clock, ArrowRight, Heart,
  Briefcase, Star, Sparkles, UsersRound,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { StatSkeleton } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import Link from "next/link";
import { formatCurrency, formatDate } from "@alumni/ui";
import { formatDistanceToNow } from "date-fns";
import { cn } from "@alumni/ui";
import {
  getMyCampaigns,
  getMyContributions,
  getEvents,
  getMyRsvps,
  getMyMembershipStatus,
  getMyCurrentYearUnpaidMembershipCampaigns,
  getMyProfile,
  getClassNotes,
  getJobs,
  getSpotlights,
  toggleClassNoteLike,
  getMyCommunities,
} from "@/lib/member-api";
import type { Community } from "@/lib/member-api";
import type { Campaign } from "@/types";
import type { MemberProfileResponse, MembershipStatusResponse } from "@/lib/member-api";

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
  profile: MemberProfileResponse | undefined;
  membershipStatus: MembershipStatusResponse | undefined;
  membershipCampaign: Campaign | null;
  isPensioner: boolean;
  getMemberAmount: (c: Campaign) => number;
}) {
  const [now] = useState(() => Date.now());
  const isActive = membershipStatus?.isMembershipActive;
  const expiry = membershipStatus?.membershipExpiry;

  const expiryDaysLeft = (() => {
    if (!isActive || !expiry) return null;
    const days = Math.ceil((new Date(expiry).getTime() - now) / 86_400_000);
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
            Alumni · Member card
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
            You&apos;re all set for this year.
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
  membershipStatus: MembershipStatusResponse | undefined;
}) {
  if (!membershipStatus?.isMembershipActive || !membershipStatus?.hasArrears) return null;
  return (
    <div className="rounded-2xl p-5 sm:p-6 space-y-4 bg-warning/10 border border-warning/30">
      <div className="flex items-start gap-4">
        <div className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0 bg-warning/15">
          <AlertTriangle size={18} className="text-warning" />
        </div>
        <div>
          <p className="font-bold text-[15px] text-foreground">
            You have unpaid dues from previous years
          </p>
          <p className="text-[13.5px] mt-1 leading-relaxed text-muted-foreground">
            Your current membership is active, but you have{" "}
            <span className="font-semibold text-foreground">
              {membershipStatus.arrearsCount} unpaid year{membershipStatus.arrearsCount !== 1 ? "s" : ""}
            </span>
            . Clearing them keeps your record in good standing.
          </p>
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex flex-wrap gap-2">
          {membershipStatus.arrearsYears.map((year: number) => (
            <Badge key={year} variant="warning" className="text-[12px] font-bold px-3 py-1 rounded-full">
              {year}
            </Badge>
          ))}
        </div>
        <Link href="/contributions">
          <Button
            size="sm"
            className="gap-1.5 font-semibold bg-warning text-warning-foreground border-none hover:bg-warning/90"
            style={{ height: 38 }}
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
    <div className="rounded-2xl border border-border bg-card p-4 flex flex-col gap-3 min-w-0 shadow-[var(--card-shadow)] transition-shadow hover:shadow-[var(--card-shadow-hover)]">
      {/* Icon */}
      <div
        className="w-9 h-9 rounded-xl flex items-center justify-center shrink-0"
        style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
      >
        <Icon size={16} style={{ color: "var(--primary)" }} />
      </div>

      {/* Value — clamps to one line, scales down if needed */}
      <p
        className="font-[family-name:var(--font-display)] font-bold leading-none tabular-nums text-foreground"
        style={{
          fontSize: "clamp(1.25rem, 3.5vw, 1.65rem)",
          letterSpacing: "-0.02em",
          wordBreak: "break-word",
          overflowWrap: "anywhere",
        }}
      >
        {value}
      </p>

      {/* Label + sub */}
      <div className="mt-auto pt-2 border-t border-border">
        <p className="text-[13px] font-semibold leading-tight text-foreground">
          {label}
        </p>
        <p className="text-[11.5px] mt-0.5 text-muted-foreground">
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
   WHAT'S NEW — the daily-return hook. A quick pulse of what's happened
   since the last visit, so there's a reason to open this beyond dues season.
   ───────────────────────────────────────────────────────────────────────── */
function PulseCard({
  icon: Icon, title, href, seeAllLabel = "See all", children,
}: {
  icon: React.ElementType; title: string; href: string; seeAllLabel?: string; children: React.ReactNode;
}) {
  return (
    <Card className="flex flex-col">
      <CardHeader className="flex flex-row items-center justify-between pb-4 border-b" style={{ borderColor: "var(--border)" }}>
        <div className="flex items-center gap-2">
          <Icon size={15} style={{ color: "var(--primary)" }} />
          <CardTitle className="text-[14.5px] font-bold">{title}</CardTitle>
        </div>
        <Link href={href}>
          <Button size="sm" variant="ghost" className="text-[12.5px] gap-1 font-semibold">
            {seeAllLabel} <ChevronRight size={12} />
          </Button>
        </Link>
      </CardHeader>
      <CardContent className="pt-3 space-y-1 flex-1">
        {children}
      </CardContent>
    </Card>
  );
}

function PulseEmpty({ label }: { label: string }) {
  return (
    <p className="text-[13px] py-6 text-center" style={{ color: "var(--muted-foreground)" }}>
      {label}
    </p>
  );
}

function ClassNotesPulse() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: ["m-dash-class-notes"],
    queryFn: () => getClassNotes(1, 3),
  });
  const likeMut = useMutation({
    mutationFn: (id: string) => toggleClassNoteLike(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["m-dash-class-notes"] }),
  });
  const notes = data?.results ?? [];

  return (
    <PulseCard icon={Sparkles} title="Class notes" href="/class-notes">
      {isLoading ? (
        <div className="space-y-3 py-1">{Array.from({ length: 2 }).map((_, i) => <div key={i} className="h-10 rounded-lg animate-pulse bg-secondary" />)}</div>
      ) : notes.length === 0 ? (
        <PulseEmpty label="No posts yet — be the first to share an update." />
      ) : (
        notes.map((n) => (
          <div key={n.id} className="flex gap-3 p-2.5 rounded-xl transition-colors hover:bg-secondary">
            <UserAvatar name={n.authorName ?? "Member"} size="sm" />
            <div className="flex-1 min-w-0">
              <p className="text-[13px] leading-snug" style={{ color: "var(--foreground)" }}>
                <span className="font-semibold">{n.authorName ?? "Member"}</span>{" "}
                <span style={{ color: "var(--muted-foreground)" }}>{n.content}</span>
              </p>
              <div className="flex items-center gap-3 mt-1">
                <button
                  onClick={() => likeMut.mutate(n.id)}
                  className={cn(
                    "flex items-center gap-1 text-[11.5px] font-semibold transition-colors",
                    n.isLikedByMe ? "text-destructive" : "hover:text-destructive"
                  )}
                  style={{ color: n.isLikedByMe ? undefined : "var(--muted-foreground)" }}
                >
                  <Heart size={11} className={cn(n.isLikedByMe && "fill-current")} />
                  {n.likeCount > 0 ? n.likeCount : "Like"}
                </button>
                <span className="text-[11px]" style={{ color: "var(--muted-foreground)" }}>
                  {formatDistanceToNow(new Date(n.createdAt), { addSuffix: true })}
                </span>
              </div>
            </div>
          </div>
        ))
      )}
    </PulseCard>
  );
}

function JobsPulse() {
  const { data, isLoading } = useQuery({
    queryKey: ["m-dash-jobs"],
    queryFn: () => getJobs(1, 3),
  });
  const jobs = data?.results ?? [];

  return (
    <PulseCard icon={Briefcase} title="New jobs" href="/jobs">
      {isLoading ? (
        <div className="space-y-3 py-1">{Array.from({ length: 2 }).map((_, i) => <div key={i} className="h-10 rounded-lg animate-pulse bg-secondary" />)}</div>
      ) : jobs.length === 0 ? (
        <PulseEmpty label="No open roles right now — check back soon." />
      ) : (
        jobs.map((j) => (
          <Link key={j.id} href={`/jobs/${j.id}`} className="flex items-start gap-3 p-2.5 rounded-xl transition-colors hover:bg-secondary group">
            <div className="w-9 h-9 rounded-lg flex items-center justify-center shrink-0" style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}>
              <Briefcase size={14} style={{ color: "var(--primary)" }} />
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-[13px] font-semibold leading-snug truncate group-hover:text-primary transition-colors" style={{ color: "var(--foreground)" }}>
                {j.title}
              </p>
              <p className="text-[11.5px] mt-0.5 truncate" style={{ color: "var(--muted-foreground)" }}>
                {j.company}{j.location ? ` · ${j.location}` : ""}
              </p>
            </div>
          </Link>
        ))
      )}
    </PulseCard>
  );
}

function SpotlightPulse() {
  const { data, isLoading } = useQuery({
    queryKey: ["m-dash-spotlight"],
    queryFn: () => getSpotlights(1, 1),
  });
  const spotlight = data?.results?.[0];

  return (
    <PulseCard icon={Star} title="Spotlight" href="/spotlights">
      {isLoading ? (
        <div className="h-24 rounded-lg animate-pulse bg-secondary" />
      ) : !spotlight ? (
        <PulseEmpty label="No spotlights yet." />
      ) : (
        <Link href="/spotlights" className="block p-2.5 rounded-xl transition-colors hover:bg-secondary group">
          <div className="flex items-center gap-3">
            <UserAvatar name={spotlight.memberName ?? "Alumnus"} src={spotlight.imageUrl} size="default" />
            <div className="min-w-0">
              <p className="text-[13.5px] font-semibold leading-snug group-hover:text-primary transition-colors" style={{ color: "var(--foreground)" }}>
                {spotlight.memberName ?? "Alumnus"}
              </p>
              {spotlight.memberGraduationYear && (
                <p className="text-[11.5px]" style={{ color: "var(--muted-foreground)" }}>Class of {spotlight.memberGraduationYear}</p>
              )}
            </div>
          </div>
          <p className="text-[13px] mt-2.5 leading-relaxed line-clamp-2" style={{ color: "var(--muted-foreground)" }}>
            {spotlight.title}
          </p>
        </Link>
      )}
    </PulseCard>
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

  // Community campaigns are scoped per-community on the backend (never
  // returned by the general /campaigns call), so pulling them onto the
  // dashboard needs one query per community the member has actually joined.
  const { data: myCommunities = [] } = useQuery({
    queryKey: ["m-my-communities"],
    queryFn: getMyCommunities,
  });
  const approvedCommunities = myCommunities.filter((c) => c.myStatus === "Approved");

  const communityCampaignResults = useQueries({
    queries: approvedCommunities.map((c) => ({
      queryKey: ["m-community-campaigns-dash", c.id],
      queryFn: async () => ({ community: c, campaigns: (await getMyCampaigns(1, 20, c.id)).results }),
    })),
  });
  const communityCampaignsLoading = approvedCommunities.length > 0 && communityCampaignResults.some((r) => r.isLoading);
  const communityCampaigns = communityCampaignResults
    .flatMap((r) => {
      const entry = r.data as { community: Community; campaigns: Campaign[] } | undefined;
      if (!entry) return [];
      return entry.campaigns
        .filter((c) => c.status === "Active" && !c.isMembershipCampaign)
        .map((c) => ({ campaign: c, community: entry.community }));
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
  const myRsvpIds = new Set((rsvps.data ?? []).map((r) => r.eventId));

  const nonMembershipActiveCampaigns = activeCampaigns.filter((c) => !c.isMembershipCampaign);

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      {/* ── Greeting ── */}
      <div className="animate-in fade-in slide-in-from-bottom-3 duration-500">
        <PageHeader
          eyebrow={new Date().toLocaleDateString(undefined, { weekday: "long", day: "numeric", month: "long", year: "numeric" })}
          title={profile?.firstName ? `Welcome back, ${profile.firstName}.` : "Welcome back."}
          description="Here's what's happening in your alumni community."
        />
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
                      <div className="text-right shrink-0">
                        <p className="text-[14px] font-bold" style={{ color: "var(--primary)" }}>
                          {formatCurrency(c.amountPerMember)}
                        </p>
                        <p className="text-[10px] font-normal" style={{ color: "var(--muted-foreground)" }}>
                          Per member
                        </p>
                      </div>
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

      {/* ── Community campaigns — scoped to communities you've joined, invisible from the general campaigns list ── */}
      {(communityCampaignsLoading || communityCampaigns.length > 0) && (
        <section>
          <Card>
            <CardHeader className="flex flex-row items-center justify-between pb-4">
              <div>
                <CardTitle className="text-[15px] font-bold flex items-center gap-2">
                  <UsersRound size={15} style={{ color: "var(--primary)" }} />
                  Community campaigns
                </CardTitle>
                <p className="text-[13px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                  Contribution campaigns from communities you've joined
                </p>
              </div>
              <Link href="/communities">
                <Button size="sm" variant="ghost" className="text-[13px] gap-1 font-semibold">
                  All <ChevronRight size={13} />
                </Button>
              </Link>
            </CardHeader>
            <CardContent className="space-y-5 pt-0">
              {communityCampaignsLoading ? (
                <div className="space-y-3">{Array.from({ length: 2 }).map((_, i) => <div key={i} className="h-16 rounded-xl animate-pulse bg-secondary" />)}</div>
              ) : (
                communityCampaigns.map(({ campaign: c, community }) => {
                  const pct = c.targetAmount > 0
                    ? Math.round((c.collectedAmount / c.targetAmount) * 100)
                    : 0;
                  return (
                    <Link key={c.id} href={`/contributions/${c.id}`} className="block group">
                      <div className="flex items-start justify-between gap-3 mb-2">
                        <div className="min-w-0 flex-1">
                          <p className="text-[10.5px] font-bold uppercase tracking-wide truncate" style={{ color: "var(--primary)" }}>
                            {community.name}
                          </p>
                          <p className="text-[14px] font-semibold leading-snug group-hover:text-primary transition-colors" style={{ color: "var(--foreground)" }}>
                            {c.title}
                          </p>
                        </div>
                        <div className="text-right shrink-0">
                          <p className="text-[14px] font-bold" style={{ color: "var(--primary)" }}>
                            {formatCurrency(c.amountPerMember)}
                          </p>
                          <p className="text-[10px] font-normal" style={{ color: "var(--muted-foreground)" }}>
                            Per member
                          </p>
                        </div>
                      </div>
                      <Progress value={pct} className="h-2 mb-1.5" />
                      <div className="flex items-center justify-between text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>
                        <span>{pct}% of target reached</span>
                        <span>Due {formatDate(c.deadline)}</span>
                      </div>
                    </Link>
                  );
                })
              )}
            </CardContent>
          </Card>
        </section>
      )}

      {/* ── What's new — the reason to open this outside of dues season ── */}
      <section className="space-y-3 animate-in fade-in slide-in-from-bottom-3 duration-500 delay-100">
        <div>
          <h2 className="text-[15px] font-bold" style={{ color: "var(--foreground)" }}>
            What&apos;s new
          </h2>
          <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
            Since you last checked in
          </p>
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 sm:gap-6">
          <ClassNotesPulse />
          <JobsPulse />
          <SpotlightPulse />
        </div>
      </section>

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
            {upcomingEvents.slice(0, 3).map((e) => (
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
            {contributionsList.slice(0, 5).map((c) => (
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
