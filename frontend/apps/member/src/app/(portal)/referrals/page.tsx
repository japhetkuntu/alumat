"use client";

import { useEffect, useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Check, Copy, MessageSquare, Share2, Trophy, Medal, Award, Send, Loader2,
} from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { LoadError } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { getReferralInfo, getMyReferrals, getReferralLeaderboard, sendReferralInvite } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";

const RANK_STYLE = [
  { bg: "rgba(234,179,8,0.12)",  border: "rgba(234,179,8,0.35)",  text: "#b45309" },
  { bg: "rgba(148,163,184,0.12)", border: "rgba(148,163,184,0.35)", text: "#64748b" },
  { bg: "rgba(180,83,9,0.10)",   border: "rgba(180,83,9,0.3)",    text: "#92400e" },
];

function RankBadge({ index }: { index: number }) {
  if (index < 3) {
    const s = RANK_STYLE[index];
    const Icon = index === 0 ? Trophy : Medal;
    return (
      <div className="w-9 h-9 rounded-full flex items-center justify-center shrink-0" style={{ background: s.bg, border: `1.5px solid ${s.border}` }}>
        <Icon size={16} style={{ color: s.text }} />
      </div>
    );
  }
  return (
    <div className="w-9 h-9 rounded-full flex items-center justify-center shrink-0 text-[13px] font-bold tabular-nums" style={{ background: "var(--secondary)", color: "var(--muted-foreground)", border: "1px solid var(--border)" }}>
      {index + 1}
    </div>
  );
}

export default function ReferralsPage() {
  const { user } = useAuth();
  const [copied, setCopied] = useState(false);
  const [inviteEmail, setInviteEmail] = useState("");

  const { data: info, isLoading, isError, refetch } = useQuery({
    queryKey: ["referral-info"],
    queryFn: getReferralInfo,
  });

  const { data: myReferrals } = useQuery({
    queryKey: ["referral-list"],
    queryFn: getMyReferrals,
  });

  const { data: leaderboard, isLoading: leaderboardLoading } = useQuery({
    queryKey: ["referral-leaderboard"],
    queryFn: getReferralLeaderboard,
  });

  const [link, setLink] = useState("");
  useEffect(() => {
    if (info?.referralCode && typeof window !== "undefined") {
      setLink(`${window.location.origin}/register?ref=${info.referralCode}`);
    }
  }, [info?.referralCode]);

  const shareMessage = `Join me on our alumni community — ${link}`;

  const inviteMut = useMutation({
    mutationFn: () => sendReferralInvite(inviteEmail.trim()),
    onSuccess: () => {
      toast.success("Invitation sent!");
      setInviteEmail("");
      refetch();
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const copyLink = async () => {
    try {
      await navigator.clipboard.writeText(link);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard access can fail silently (permissions, non-secure context) — the link is
      // still visible and selectable, so this never blocks copying it by hand.
    }
  };

  if (isLoading) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 max-w-[1000px] mx-auto space-y-6">
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }
  if (isError || !info) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 max-w-[1000px] mx-auto">
        <LoadError onRetry={refetch} />
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1000px] mx-auto space-y-6 sm:space-y-8">
      <PageHeader
        eyebrow="Grow the community"
        title="Refer & earn"
        description="Every classmate you bring in earns you points — the leaderboard shows who's brought in the most."
      />

      {/* Stats + points */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {[
          { label: "Points", value: info.points },
          { label: "Your rank", value: info.rank ? `#${info.rank}` : "—" },
          { label: "Registered", value: info.registeredReferrals },
          { label: "Became members", value: info.membershipPaidReferrals },
        ].map((s) => (
          <div key={s.label} className="card p-4 text-center">
            <p className="text-[22px] font-bold" style={{ color: "var(--primary)" }}>{s.value}</p>
            <p className="text-[12px] text-muted-foreground mt-0.5">{s.label}</p>
          </div>
        ))}
      </div>

      {/* Share */}
      <div className="card p-5">
        <div className="flex items-start gap-2.5 mb-3">
          <div className="w-8 h-8 rounded-lg bg-primary/10 flex items-center justify-center shrink-0">
            <Share2 size={16} className="text-primary" />
          </div>
          <div>
            <p className="text-[14px] font-semibold">Your invite link</p>
            <p className="text-[12px] text-muted-foreground mt-0.5">
              10 points once they register, another 15 once they become a paying member.
              {info.hasReferrerBadge && " You've already earned the Referrer badge."}
            </p>
          </div>
        </div>
        <div className="flex flex-col sm:flex-row gap-2">
          <Input readOnly value={link} className="text-[13px] font-mono" onFocus={(e) => e.target.select()} />
          <div className="flex gap-2 shrink-0">
            <Button
              size="sm"
              className="font-semibold text-[12.5px] gap-1.5"
              onClick={() => window.open(`https://wa.me/?text=${encodeURIComponent(shareMessage)}`, "_blank", "noopener,noreferrer")}
            >
              <MessageSquare size={13} />WhatsApp
            </Button>
            <Button variant="outline" size="sm" onClick={copyLink} className="font-semibold text-[12.5px] gap-1.5">
              {copied ? <Check size={13} /> : <Copy size={13} />}
              {copied ? "Copied" : "Copy"}
            </Button>
          </div>
        </div>

        {/* Invite by email — tracked immediately, doesn't wait for them to click the link */}
        <div className="mt-4 pt-4 border-t border-border">
          <p className="text-[12.5px] font-semibold mb-2">Or invite by email</p>
          <div className="flex flex-col sm:flex-row gap-2">
            <Input
              type="email"
              placeholder="classmate@example.com"
              value={inviteEmail}
              onChange={(e) => setInviteEmail(e.target.value)}
              className="text-[13px]"
            />
            <Button
              size="sm"
              variant="outline"
              disabled={!inviteEmail.trim() || inviteMut.isPending}
              onClick={() => inviteMut.mutate()}
              className="font-semibold text-[12.5px] gap-1.5 shrink-0"
            >
              {inviteMut.isPending ? <Loader2 size={13} className="animate-spin" /> : <Send size={13} />}
              Send invite
            </Button>
          </div>
        </div>
      </div>

      {/* My referrals */}
      {myReferrals && myReferrals.length > 0 && (
        <div className="card overflow-hidden">
          <div className="px-5 py-3.5 border-b border-border">
            <p className="text-[14px] font-semibold">Your referrals</p>
          </div>
          <div className="divide-y divide-border">
            {myReferrals.map((r) => (
              <div key={r.id} className="flex items-center justify-between gap-3 px-5 py-3">
                <div className="min-w-0">
                  <p className="text-[13px] font-medium truncate">{r.referredMemberName || r.referredEmail}</p>
                  <p className="text-[12px] text-muted-foreground">{new Date(r.createdAt).toLocaleDateString()}</p>
                </div>
                <Badge variant={r.status === "MembershipPaid" ? "success" : r.status === "Registered" ? "default" : "outline"}>
                  {r.status === "MembershipPaid" ? "Paying member" : r.status === "Registered" ? "Registered" : "Invited"}
                </Badge>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Leaderboard */}
      <div className="card overflow-hidden">
        <div className="px-5 py-3.5 border-b border-border flex items-center gap-2">
          <Award size={16} className="text-primary" />
          <p className="text-[14px] font-semibold">Top referrers</p>
        </div>
        {leaderboardLoading ? (
          <div className="p-5 space-y-3">
            {Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-12 rounded skeleton" />)}
          </div>
        ) : !leaderboard || leaderboard.length === 0 ? (
          <EmptyState
            icon={<Trophy size={32} />}
            title="No referrals yet"
            description="Be the first — share your link above and you'll top this list."
          />
        ) : (
          <div className="divide-y divide-border">
            {leaderboard.map((entry, i) => (
              <div
                key={entry.memberId}
                className={cn("flex items-center gap-3 px-5 py-3", entry.memberId === user?.id && "bg-primary/5")}
              >
                <RankBadge index={i} />
                <UserAvatar src={entry.profilePictureUrl} name={entry.name} size="sm" />
                <div className="flex-1 min-w-0">
                  <p className="text-[13px] font-semibold truncate">
                    {entry.name}{entry.memberId === user?.id && <span className="text-muted-foreground font-normal"> (you)</span>}
                  </p>
                  <p className="text-[12px] text-muted-foreground">
                    {entry.totalReferrals} referral{entry.totalReferrals === 1 ? "" : "s"}
                    {entry.membershipPaidReferrals > 0 && ` · ${entry.membershipPaidReferrals} paying`}
                  </p>
                </div>
                <p className="text-[15px] font-bold tabular-nums" style={{ color: "var(--primary)" }}>{entry.points}</p>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
