"use client";

import { useFeatures, useModuleActivity, useNavTheme } from "@/components/member/member-layout";
import { isFeatureDisabledError } from "@/lib/feature-errors";
import { useEffect, useRef, useState } from "react";
import { LoadError } from "@alumni/ui";
import { useQueries, useQuery, useQueryClient } from "@tanstack/react-query";
import { formatDistanceToNow } from "date-fns";
import {
  CreditCard, Calendar, ChevronRight, Award,
  AlertTriangle, CheckCircle2, Clock, ArrowRight,
  Briefcase, UsersRound,
} from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { StatCard, StatCardSkeleton } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import Link from "next/link";
import { formatCurrency, formatDate } from "@alumni/ui";
import { cn } from "@alumni/ui";
import {
  getMyCampaigns,
  getMyContributions,
  getMyContributionSummary,
  getEvents,
  getMyRsvps,
  getMyMembershipStatus,
  getMyCurrentYearUnpaidMembershipCampaigns,
  getMyProfile,
  getJobs,
  getMyCommunities,
  getHomeFeed,
  markHomeSeen,
} from "@/lib/member-api";
import type { Community, HomeFeed, HomeFeedItem, HomeFeedKind } from "@/lib/member-api";
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
      className="relative overflow-hidden rounded-2xl p-4 sm:p-8"
      style={{
        // Flat solid fill, no gradient — a hard blend between two brand
        // colors risks the same muddy, low-contrast look for an
        // unpredictable color pair. Secondary shows up instead as a solid
        // accent-colored ring below, never mixed into the background.
        background: isActive
          ? "var(--brand-primary-dark, var(--primary))"
          : "#1f2937",
        color: "white",
      }}
    >
      {/* Subtle texture rings — the outer one picks up the institution's
          accent color as a solid stroke when they have one, so secondary
          shows up as a clean flat outline rather than blended into the fill. */}
      <div className="absolute -right-10 -top-10 w-36 h-36 sm:-right-16 sm:-top-16 sm:w-64 sm:h-64 rounded-full border-2" style={{ borderColor: "var(--brand-accent, rgba(255,255,255,0.1))" }} />
      <div className="absolute -right-5 -top-5 w-24 h-24 sm:-right-8 sm:-top-8 sm:w-40 sm:h-40 rounded-full border border-white/10" />

      <div className="relative flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3 sm:gap-6">

        {/* Left — identity */}
        <div className="space-y-0.5 sm:space-y-1">
          <p className="text-white/60 text-[12px] sm:text-[12px] font-semibold tracking-[0.1em] uppercase">
            Member card
          </p>
          <p className="text-[18px] sm:text-[26px] font-bold leading-tight">
            {profile?.firstName
              ? `${profile.firstName} ${profile.lastName ?? ""}`
              : "—"}
          </p>
          {profile?.graduationYear && (
            <p className="text-white/70 text-[12.5px] sm:text-[14px]">
              Class of {profile.graduationYear}
              {profile.departmentName ? ` · ${profile.departmentName}` : ""}
            </p>
          )}
        </div>

        {/* Right — status */}
        <div className="flex flex-row sm:flex-col items-center sm:items-end justify-between sm:justify-start gap-2 sm:gap-3 shrink-0">
          <div
            className={cn(
              "inline-flex items-center gap-1.5 px-2.5 py-1 sm:px-3 sm:py-1.5 text-[12px] sm:text-[13px] font-bold",
              isActive
                ? "bg-white/20 text-white"
                : "bg-white/15 text-white/80",
            )}
          >
            {isActive
              ? <><CheckCircle2 size={12} /> Active</>
              : <><Clock size={12} /> Inactive</>}
          </div>
          <div className="flex items-center gap-3 sm:flex-col sm:items-end sm:gap-1.5">
            {isActive && expiry && (
              <p className="text-white/60 text-[12px] sm:text-[12px]">
                Valid until {formatDate(expiry)}
              </p>
            )}
            {/* Certificate link — only when active */}
            {isActive && (
              <Link href="/membership-certificate">
                <button className="flex items-center gap-1.5 text-[12px] sm:text-[12px] text-white/70 hover:text-white transition-colors whitespace-nowrap">
                  <Award size={12} /> Certificate
                </button>
              </Link>
            )}
          </div>
        </div>
      </div>

      {/* Action area — unpaid dues */}
      {membershipCampaign && !isActive && (
        <div className="relative mt-4 pt-4 sm:mt-6 sm:pt-5 border-t border-white/20">
          <p className="text-white/80 text-[13px] sm:text-[14px] mb-2.5 sm:mb-3">
            Your membership is inactive. Pay your dues to activate it.
          </p>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p className="text-white font-bold text-[16px] sm:text-[18px]">
                {formatCurrency(getMemberAmount(membershipCampaign))}
                {isPensioner ? (
                  <span className="text-white/60 text-[12px] font-normal ml-1.5">pensioner rate</span>
                ) : null}
              </p>
              <p className="text-white/60 text-[12px] sm:text-[12px]">
                Due {formatDate(membershipCampaign.deadline)}
              </p>
            </div>
            <Link href={`/contributions/${membershipCampaign.id}`}>
              <Button
                className="bg-white font-bold gap-2 hover:bg-white/90"
                style={{ color: "var(--primary)", height: 40 }}
              >
                Pay now <ArrowRight size={15} />
              </Button>
            </Link>
          </div>
        </div>
      )}

      {/* Expiry warning — active but expiring soon */}
      {isActive && expiryDaysLeft !== null && (
        <div className="relative mt-4 pt-4 sm:mt-5 sm:pt-5 border-t border-white/20 flex flex-wrap items-center justify-between gap-3">
          <p className="text-white/80 text-[13px] sm:text-[14px]">
            Expires in <span className="font-bold text-white">{expiryDaysLeft} day{expiryDaysLeft !== 1 ? "s" : ""}</span>. Renew now to stay active.
          </p>
          {membershipCampaign && (
            <Link href={`/contributions/${membershipCampaign.id}`}>
              <Button className="bg-white font-bold hover:bg-white/90" style={{ color: "var(--primary)", height: 36 }}>
                Renew
              </Button>
            </Link>
          )}
        </div>
      )}

      {/* Active + all good */}
      {isActive && expiryDaysLeft === null && !membershipCampaign && (
        <div className="relative mt-4 pt-4 sm:mt-5 sm:pt-5 border-t border-white/20">
          <p className="text-white/70 text-[13px] sm:text-[14px] flex items-center gap-2">
            <CheckCircle2 size={15} className="text-white/60" />
            You&apos;re all set for this year.
          </p>
        </div>
      )}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   MEMBERSHIP CARD SKELETON — a themeless shimmer, never the fixed gray of
   the settled "inactive" state, so a still-loading card can't be mistaken
   for an institution with no brand color of its own.
   ───────────────────────────────────────────────────────────────────────── */
function MembershipCardSkeleton() {
  return (
    <div className="relative overflow-hidden rounded-2xl p-4 sm:p-8 border border-border bg-card">
      <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3 sm:gap-6">
        <div className="space-y-2 sm:space-y-2.5">
          <div className="skeleton h-3 w-32 rounded-none" />
          <div className="skeleton h-6 w-40 rounded-none" />
          <div className="skeleton h-3.5 w-28 rounded-none" />
        </div>
        <div className="skeleton h-7 w-24 shrink-0" />
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   ARREARS BANNER
   ───────────────────────────────────────────────────────────────────────── */

/**
 * A quiet nudge toward the two things that make a profile actually useful to
 * the rest of the community — a bio/photo people recognize them by, and a
 * location so the Alumni Map isn't empty. Dismissible per-browser, and it
 * naturally stops appearing on its own once both are filled in, so it never
 * has to be dismissed at all if the member just does it.
 */
function ArrearsBanner({
  membershipStatus,
}: {
  membershipStatus: MembershipStatusResponse | undefined;
}) {
  if (membershipStatus?.activePolicy === "ApprovedOnly") return null;
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
            <Badge key={year} variant="warning" className="text-[12px] font-bold px-3 py-1">
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

/* Local helper kept only as a value-adapter — the actual tile is the shared
   @alumni/ui StatCard (also used by the institution dashboard). */
function DashStat({
  label,
  value,
  sub,
  tone,
  href,
}: {
  label: string;
  value: string | number;
  sub: string;
  tone?: "primary" | "accent";
  href: string;
}) {
  return (
    <Link href={href} className="block focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary">
      <StatCard label={label} value={value} sub={sub} tone={tone} />
    </Link>
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
   PEOPLE FEED — the reason to come back. What named members have done,
   split at the moment this member last opened home, so the first thing
   they read is who did what while they were away.
   ───────────────────────────────────────────────────────────────────────── */
const FEED_SENTENCE: Record<HomeFeedKind, string> = {
  MemberJoined: "joined",
  Spotlight: "was featured in Spotlight",
  Birthday: "celebrated a birthday",
  ForumThread: "started a conversation",
  MentorJoined: "is offering mentorship",
  BusinessListed: "listed a business",
};

function feedHref(item: HomeFeedItem): string {
  switch (item.kind) {
    case "ForumThread": return `/forum/${item.entityId}`;
    case "BusinessListed": return `/business-directory/${item.entityId}`;
    case "MentorJoined": return "/mentorship";
    case "Spotlight":
    case "Birthday": return "/spotlights";
    // The directory has no page per member; its search box, pre-filled, is the closest thing.
    case "MemberJoined": return `/directory?search=${encodeURIComponent(item.personName)}`;
  }
}

/** How many older items to keep under the new ones — enough to show the place is alive, not enough to bury what's new. */
const EARLIER_SHOWN = 6;

function FeedRow({ item, isNew, showCohort }: { item: HomeFeedItem; isNew: boolean; showCohort: boolean }) {
  const cohort = !showCohort ? null
    : item.sameYearGroup ? "Your year group"
    : item.personGraduationYear ? `Class of ${item.personGraduationYear}`
    : null;
  const meta = [cohort, showCohort && item.sameDepartment ? "Your department" : null].filter(Boolean).join(" · ");

  return (
    <Link href={feedHref(item)} className="flex items-start gap-3 p-2.5 sm:p-3 rounded-xl transition-colors hover:bg-secondary">
      <UserAvatar name={item.personName} src={item.personPhotoUrl ?? undefined} size="default" />
      <div className="flex-1 min-w-0">
        <p className="text-[14px] leading-snug text-foreground">
          <span className="font-semibold">{item.personName}</span>{" "}
          <span className="text-muted-foreground">{FEED_SENTENCE[item.kind]}</span>
        </p>
        {item.title && (
          <p className="text-[13.5px] mt-0.5 leading-snug line-clamp-2 text-foreground">{item.title}</p>
        )}
        <p className="text-[12.5px] mt-0.5 text-muted-foreground">
          {meta && <span className={cn(item.sameYearGroup && "font-semibold text-foreground")}>{meta} · </span>}
          {formatDistanceToNow(new Date(item.occurredAt), { addSuffix: true })}
        </p>
      </div>
      {isNew && <span className="shrink-0 text-[12px] font-bold text-primary">New</span>}
    </Link>
  );
}

function PeopleFeed({
  feed, isLoading, isError, onRetry, showCohort,
}: {
  feed: HomeFeed | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  showCohort: boolean;
}) {
  // The marker this visit opened with. Loading the feed moves the member's marker to now, so
  // without holding on to the first one, a background refetch would quietly un-mark everything new.
  const [opened, setOpened] = useState<{ lastSeenAt: string | null } | null>(null);
  if (feed && opened === null) setOpened({ lastSeenAt: feed.lastSeenAt ?? null });

  const queryClient = useQueryClient();
  const marked = useRef(false);
  useEffect(() => {
    if (!feed || marked.current) return;
    marked.current = true;
    const now = new Date().toISOString();
    markHomeSeen()
      // Keeps the cached feed honest for the next time this page mounts, before its own refetch lands.
      .then(() => queryClient.setQueryData<HomeFeed>(["m-home-feed"], (cached) => cached && { ...cached, lastSeenAt: now }))
      .catch(() => { marked.current = false; });
  }, [feed, queryClient]);

  const since = opened?.lastSeenAt ?? null;
  const items = feed?.items ?? [];
  const fresh = since ? items.filter((i) => i.occurredAt > since) : [];
  const earlier = (since ? items.filter((i) => i.occurredAt <= since) : items).slice(0, fresh.length > 0 ? EARLIER_SHOWN : undefined);
  const freshFromMyYear = showCohort ? fresh.filter((i) => i.sameYearGroup).length : 0;

  const away = since ? formatDistanceToNow(new Date(since)) : null;
  const heading = fresh.length > 0 ? "Since your last visit" : "Recently in your community";
  const sub = fresh.length > 0
    ? `${fresh.length} new in the last ${away}${freshFromMyYear > 0 ? `, ${freshFromMyYear} from your year group` : ""}`
    : away && items.length > 0
      ? `Nothing new since you were here ${away} ago`
      : "What other members have been doing";

  return (
    <section className="space-y-3">
      <div>
        <h2 className="text-[15px] font-bold text-foreground">{heading}</h2>
        <p className="text-[13px] text-muted-foreground">{sub}</p>
      </div>
      <Card>
        <CardContent className="p-2 sm:p-3">
          {isLoading ? (
            <div className="space-y-1" aria-busy="true">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="flex items-center gap-3 p-2.5 sm:p-3">
                  <div className="skeleton h-10 w-10 rounded-full shrink-0" />
                  <div className="flex-1 space-y-2">
                    <div className="skeleton h-3.5 w-2/3 rounded-none" />
                    <div className="skeleton h-3 w-1/3 rounded-none" />
                  </div>
                </div>
              ))}
            </div>
          ) : isError && !feed ? (
            <LoadError title="Your feed couldn’t load" onRetry={onRetry} />
          ) : items.length === 0 ? (
            <p className="p-3 text-[13.5px] leading-relaxed text-muted-foreground">
              Nothing from other members in the last while. When someone joins, starts a conversation or lists a business, you’ll see them here by name.
            </p>
          ) : (
            <>
              {fresh.map((item) => <FeedRow key={`${item.kind}-${item.entityId}-${item.personId}`} item={item} isNew showCohort={showCohort} />)}
              {fresh.length > 0 && earlier.length > 0 && (
                <p className="px-2.5 sm:px-3 pt-4 pb-1 text-[12px] font-bold uppercase tracking-[0.08em] text-muted-foreground">Earlier</p>
              )}
              {earlier.map((item) => <FeedRow key={`${item.kind}-${item.entityId}-${item.personId}`} item={item} isNew={false} showCohort={showCohort} />)}
            </>
          )}
        </CardContent>
      </Card>
    </section>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   BE THE FIRST — modules that are empty stay out of the menu, which would
   leave them empty forever if nobody could find the way in. The ones a
   member can fill themselves are offered here instead, as an invitation.
   ───────────────────────────────────────────────────────────────────────── */
const INVITATIONS: { feature: string; text: string; action: string; href: string }[] = [
  { feature: "Forum", text: "Nobody has started a conversation yet.", action: "Start the first one", href: "/forum" },
  { feature: "BusinessDirectory", text: "No member has listed a business yet.", action: "List yours", href: "/business-directory/mine" },
  { feature: "Mentorship", text: "Nobody is offering mentorship yet.", action: "Offer to mentor", href: "/mentorship" },
  { feature: "Spotlights", text: "No member stories have been shared yet.", action: "Share yours", href: "/spotlights" },
];

function BeTheFirst({ emptyModules }: { emptyModules: string[] }) {
  const features = useFeatures();
  const open = INVITATIONS.filter((i) => features.enabled(i.feature) && emptyModules.includes(i.feature));
  if (open.length === 0) return null;
  return (
    <section className="space-y-3">
      <div>
        <h2 className="text-[15px] font-bold text-foreground">Be the first</h2>
        <p className="text-[13px] text-muted-foreground">These open up for everyone once one person goes first</p>
      </div>
      <Card>
        <CardContent className="p-0 divide-y divide-border">
          {open.map((i) => (
            <div key={i.feature} className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1 px-4 py-3.5 sm:px-5">
              <p className="text-[14px] text-foreground">{i.text}</p>
              <Link href={i.href} className="text-[13.5px] font-semibold text-primary underline underline-offset-4">{i.action}</Link>
            </div>
          ))}
        </CardContent>
      </Card>
    </section>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   NEW JOBS — rendered only once there are jobs; an empty board says nothing.
   ───────────────────────────────────────────────────────────────────────── */
function JobsCard() {
  const { data } = useQuery({
    queryKey: ["m-dash-jobs"],
    queryFn: () => getJobs(1, 3),
  });
  const jobs = data?.results ?? [];
  if (jobs.length === 0) return null;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-4 border-b" style={{ borderColor: "var(--border)" }}>
        <div>
          <CardTitle className="text-[15px] font-bold">New jobs</CardTitle>
          <p className="text-[13px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
            Shared with your community
          </p>
        </div>
        <Link href="/jobs">
          <Button size="sm" variant="ghost" className="text-[13px] gap-1 font-semibold">
            All <ChevronRight size={13} />
          </Button>
        </Link>
      </CardHeader>
      <CardContent className="pt-4 space-y-1">
        {jobs.map((j) => (
          <Link key={j.id} href={`/jobs/${j.id}`} className="flex items-center gap-4 p-3 rounded-xl transition-colors hover:bg-secondary group">
            <div className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0" style={{ background: "var(--muted)", border: "1px solid var(--border)" }}>
              <Briefcase size={17} style={{ color: "var(--muted-foreground)" }} />
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-[14px] font-semibold leading-snug truncate" style={{ color: "var(--foreground)" }}>
                {j.title}
              </p>
              <p className="text-[12.5px] mt-0.5 truncate" style={{ color: "var(--muted-foreground)" }}>
                {j.company}{j.location ? ` · ${j.location}` : ""}
              </p>
            </div>
          </Link>
        ))}
      </CardContent>
    </Card>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PAGE
   ───────────────────────────────────────────────────────────────────────── */
export default function MemberDashboardPage() {
  const features = useFeatures();
  const contributionsEnabled = features.enabled("Contributions");
  const eventsEnabled = features.enabled("Events");
  const jobsEnabled = features.enabled("Jobs");
  const directoryEnabled = features.enabled("Directory");
  const communitiesEnabled = features.enabled("Communities");
  const results = useQueries({
    queries: [
      { queryKey: ["m-campaigns"],             enabled: contributionsEnabled, queryFn: () => getMyCampaigns(1, 50)                    },
      { queryKey: ["m-contributions-recent"],  enabled: contributionsEnabled, queryFn: () => getMyContributions({ pageSize: 5 })      },
      { queryKey: ["m-contribution-summary"],  enabled: contributionsEnabled, queryFn: getMyContributionSummary                        },
      { queryKey: ["m-events", "upcoming"],    enabled: eventsEnabled, queryFn: () => getEvents(1, 50, "Upcoming")             },
      { queryKey: ["m-rsvps"],                 enabled: eventsEnabled, queryFn: () => getMyRsvps()                             },
    ],
  });

  const [campaigns, contributions, contributionSummary, events, rsvps] = results;
  const isLoading = !features.ready || results.some((r) => r.isLoading);

  const membershipStatus = useQuery({
    queryKey: ["m-membership-status"],
    queryFn: getMyMembershipStatus,
    enabled: contributionsEnabled,
    retry: false,
    staleTime: 5 * 60 * 1000,
  });

  const unpaidMembershipCampaignsQuery = useQuery({
    queryKey: ["m-membership-current-unpaid"],
    queryFn: getMyCurrentYearUnpaidMembershipCampaigns,
    enabled: contributionsEnabled,
    retry: false,
    staleTime: 5 * 60 * 1000,
  });

  const profileQuery = useQuery({
    queryKey: ["m-profile"],
    queryFn: getMyProfile,
  });

  const profile = profileQuery.data;

  const feed = useQuery({ queryKey: ["m-home-feed"], queryFn: getHomeFeed });
  const emptyModules = useModuleActivity()?.empty ?? [];
  // Community-type institutions don't collect graduation years, so there is no year group to point at.
  const showCohort = useNavTheme().data?.organizationType !== "Community";

  // Community campaigns are scoped per-community on the backend (never
  // returned by the general /campaigns call), so pulling them onto the
  // dashboard needs one query per community the member has actually joined.
  const { data: myCommunities = [] } = useQuery({
    queryKey: ["m-my-communities"],
    queryFn: getMyCommunities,
    enabled: communitiesEnabled,
  });
  const approvedCommunities = myCommunities.filter((c) => c.myStatus === "Approved");

  const communityCampaignResults = useQueries({
    queries: (contributionsEnabled && communitiesEnabled ? approvedCommunities : []).map((c) => ({
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
  const activeCampaigns = (contributionsEnabled ? campaigns.data?.results ?? [] : []).filter((c) => c.status === "Active");
  const contributionsList = contributions.data?.results ?? [];
  const paidMembershipCampaignIds = new Set(contributionSummary.data?.paidCampaignIds ?? []);

  const unpaidCurrentMembershipCampaigns = contributionsEnabled ? unpaidMembershipCampaignsQuery.data ?? [] : [];
  const membershipCampaign = unpaidCurrentMembershipCampaigns[0] ?? null;

  const activeMembershipCampaigns = activeCampaigns.filter((c) => c.isMembershipCampaign);
  const futureMembershipCampaigns = activeMembershipCampaigns.filter(
    (c) => c.membershipYear && c.membershipYear > currentYear && !paidMembershipCampaignIds.has(c.id),
  );

  const totalPaid = contributionSummary.data?.totalPaid ?? 0;
  const totalPaidThisYear = contributionSummary.data?.totalPaidThisYear ?? 0;

  const upcomingEvents = eventsEnabled ? events.data?.results ?? [] : [];
  const upcomingEventsCount = events.data?.totalCount ?? 0;
  const myRsvpIds = new Set((rsvps.data ?? []).map((r) => r.eventId));

  const nonMembershipActiveCampaigns = activeCampaigns.filter((c) => !c.isMembershipCampaign);
  const failedQueries = [...results, membershipStatus, unpaidMembershipCampaignsQuery, profileQuery, ...communityCampaignResults].filter(query => query.isError && !isFeatureDisabledError(query.error));
  const hasNoActivity = features.ready && (!contributionsEnabled || campaigns.isSuccess) && (!eventsEnabled || events.isSuccess) && activeCampaigns.length === 0 && upcomingEvents.length === 0 && feed.isSuccess && feed.data.items.length === 0;
  const hasActivityFeatures = contributionsEnabled || eventsEnabled;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      {/* ── Greeting ── */}
      <div className="animate-in fade-in slide-in-from-bottom-3 duration-500">
        <PageHeader
          eyebrow={new Date().toLocaleDateString(undefined, { weekday: "long", day: "numeric", month: "long", year: "numeric" })}
          title={profile?.firstName ? `Welcome back, ${profile.firstName}.` : "Welcome back."}
          description="Here's what's happening in your member community."
        />
      </div>

      {failedQueries.length > 0 && (
        <LoadError
          className="py-6"
          title="Some of your dashboard couldn't load"
          description="The figures below may be incomplete. Try again."
          onRetry={() => failedQueries.forEach(query => void query.refetch())}
        />
      )}

      {/* ── Membership card ── */}
      <div className="animate-in fade-in slide-in-from-bottom-3 duration-500 delay-75">
        {membershipStatus.isLoading ? (
          <MembershipCardSkeleton />
        ) : membershipStatus.data ? (
          <MembershipCard
            profile={profile}
            membershipStatus={membershipStatus.data}
            membershipCampaign={membershipCampaign}
            isPensioner={isPensioner}
            getMemberAmount={getMemberAmount}
          />
        ) : null}
      </div>

      {/* ── Arrears banner ── */}
      {membershipStatus.isSuccess && (
        <ArrearsBanner membershipStatus={membershipStatus.data} />
      )}

      {/* ── People feed — skipped only when the whole portal is empty and the welcome below speaks for it ── */}
      {!hasNoActivity && (
        <div className="animate-in fade-in slide-in-from-bottom-3 duration-500 delay-100">
          <PeopleFeed feed={feed.data} isLoading={feed.isLoading} isError={feed.isError} onRetry={() => void feed.refetch()} showCohort={showCohort} />
        </div>
      )}

      {hasNoActivity && (
        <div className="border border-border bg-card p-6 sm:p-8" style={{ borderRadius: 20 }}>
          <p className="text-xs text-primary font-semibold uppercase tracking-wide">Make yourself at home</p>
          <h2 className="text-2xl font-semibold mt-3">Your community starts with its people.</h2>
          <p className="text-sm text-muted-foreground leading-relaxed mt-3 max-w-xl">
            {hasActivityFeatures
              ? "Introduce yourself while your institution prepares its next activities. Published events and campaigns will appear here when they are ready."
              : "Complete your profile so other members can find and recognise you."}
          </p>
          <div className="flex flex-wrap gap-3 mt-5">
            <Link href="/profile" className="text-sm font-semibold text-primary underline underline-offset-4">Complete your profile</Link>
            {directoryEnabled && <Link href="/directory" className="text-sm font-semibold text-primary underline underline-offset-4">Explore the member directory</Link>}
          </div>
        </div>
      )}

      {/* ── Stat tiles — only the ones whose feature is on; the grid is sized to however many there are ── */}
      {(() => {
        const tiles = [
          contributionsEnabled && campaigns.data && <DashStat key="open" href="/contributions" label="Open fundraisers" value={activeCampaigns.length} sub="Including dues" tone="primary" />,
          contributionsEnabled && contributionSummary.data && <DashStat key="total" href="/contributions" label="Total contributed" value={formatCurrency(totalPaid)} sub="All-time confirmed" tone="accent" />,
          contributionsEnabled && contributionSummary.data && <DashStat key="year" href="/contributions" label="This year" value={formatCurrency(totalPaidThisYear)} sub={`Contributed in ${currentYear}`} tone="primary" />,
          eventsEnabled && events.data && <DashStat key="events" href="/events" label="Upcoming events" value={upcomingEventsCount} sub="Events you can join" tone="accent" />,
        ].filter(Boolean);
        const expected = (contributionsEnabled ? 3 : 0) + (eventsEnabled ? 1 : 0);
        const count = isLoading ? (features.ready ? expected : 4) : tiles.length;
        if (count === 0) return null;
        const lgCols = ["", "lg:grid-cols-1", "lg:grid-cols-2", "lg:grid-cols-3", "lg:grid-cols-4"][Math.min(count, 4)];
        return (
          <div className={`grid grid-cols-2 ${lgCols} gap-3 sm:gap-4 items-start animate-in fade-in duration-500 delay-100 [&>*:last-child:nth-child(odd)]:col-span-2 lg:[&>*:last-child:nth-child(odd)]:col-span-1`}>
            {isLoading ? Array.from({ length: count }).map((_, i) => <StatCardSkeleton key={i} />) : tiles}
          </div>
        );
      })()}

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
              className="w-6 h-6 rounded-full flex items-center justify-center text-[12px] font-bold text-destructive"
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
              Optional: pay ahead to secure upcoming membership years
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
                <CardTitle className="text-[15px] font-bold">Open fundraisers</CardTitle>
                <p className="text-[13px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                  Alumni-led fundraisers you can contribute to
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
                        <p className="text-[12px] font-normal" style={{ color: "var(--muted-foreground)" }}>
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
                  Community fundraisers
                </CardTitle>
                <p className="text-[13px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                  Fundraisers from communities you’ve joined
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
                          <p className="text-[12px] font-bold uppercase tracking-wide truncate" style={{ color: "var(--primary)" }}>
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
                          <p className="text-[12px] font-normal" style={{ color: "var(--muted-foreground)" }}>
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

      <BeTheFirst emptyModules={emptyModules} />

      {/* ── Events, jobs, recent payments — each only when it has something to show; a card left alone takes the full row ── */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 sm:gap-6 lg:[&>*:only-child]:col-span-2 empty:hidden">

        {/* Events */}
        {eventsEnabled && (upcomingEvents.length > 0 || events.isLoading || events.isError) && <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-4 border-b" style={{ borderColor: "var(--border)" }}>
            <div>
              <CardTitle className="text-[15px] font-bold">Upcoming events</CardTitle>
              <p className="text-[13px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                Events open to your community
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
              <Link
                key={e.id}
                href={`/events/${e.id}`}
                className="flex items-center gap-4 p-3 rounded-xl transition-colors hover:bg-secondary group"
              >
                <div
                  className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0 transition-transform group-hover:scale-105"
                  style={{ background: "var(--muted)", border: "1px solid var(--border)" }}
                >
                  <Calendar size={17} style={{ color: "var(--muted-foreground)" }} />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-[14px] font-semibold leading-snug line-clamp-3" style={{ color: "var(--foreground)" }}>
                    {e.title}
                  </p>
                  <p className="text-[12.5px] mt-0.5 line-clamp-2" style={{ color: "var(--muted-foreground)" }}>
                    {formatDate(e.startDate)}{e.venue ? ` · ${e.venue}` : ""}
                  </p>
                </div>
                {myRsvpIds.has(e.id)
                  ? <Badge variant="success" className="text-[12px] font-bold shrink-0">Going</Badge>
                  : <Badge variant="outline" className="text-[12px] font-bold shrink-0">Open</Badge>}
              </Link>
            ))}
            {events.isError && !events.data && <LoadError title="Events couldn’t load" onRetry={() => void events.refetch()} />}
            {events.isLoading && <p className="text-sm text-muted-foreground py-5">Loading events…</p>}
          </CardContent>
        </Card>}

        {jobsEnabled && !emptyModules.includes("Jobs") && <JobsCard />}

        {/* Recent contributions */}
        {contributionsEnabled && (contributionsList.length > 0 || contributions.isLoading || contributions.isError) && <Card>
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
              <Link
                key={c.id}
                href={c.campaignId ? `/contributions/${c.campaignId}` : "/contributions"}
                className="flex items-center gap-4 p-3 rounded-xl transition-colors hover:bg-secondary group"
              >
                <div
                  className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0 transition-transform group-hover:scale-105"
                  style={{ background: "var(--card)", border: "1px solid var(--border-emphasis, var(--border))" }}
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
                    variant={c.status === "Successful" ? "success" : c.status === "Pending" ? "warning" : "destructive"}
                    className="text-[12px] font-bold uppercase tracking-wide mt-0.5"
                  >
                    {c.status}
                  </Badge>
                </div>
              </Link>
            ))}
            {contributions.isError && !contributions.data && <LoadError title="Payments couldn’t load" onRetry={() => void contributions.refetch()} />}
            {contributions.isLoading && <p className="text-sm text-muted-foreground py-5">Loading your payments…</p>}
          </CardContent>
        </Card>}

      </div>
    </div>
  );
}
