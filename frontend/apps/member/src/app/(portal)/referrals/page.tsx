"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { UserPlus, Copy, Check, Send, Loader2, Gift, Users, Clock } from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { getReferralInfo, sendReferralInvite, getMyReferrals } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";

const statusVariant: Record<string, "info" | "success" | "secondary"> = {
  Pending:       "info",
  Registered:    "success",
  MembershipPaid: "success",
};
const statusLabel: Record<string, string> = {
  Pending:       "Pending",
  Registered:    "Registered",
  MembershipPaid: "Activated",
};

export default function ReferralsPage() {
  const [email,  setEmail]  = useState("");
  const [copied, setCopied] = useState(false);
  const qc = useQueryClient();

  const { data: info, isLoading: loadingInfo } = useQuery({
    queryKey: ["m-referral-info"],
    queryFn:  getReferralInfo,
  });

  const { data: referrals, isLoading: loadingReferrals } = useQuery({
    queryKey: ["m-referrals-list"],
    queryFn:  getMyReferrals,
  });

  const inviteMut = useMutation({
    mutationFn: () => sendReferralInvite(email),
    onSuccess: () => {
      toast.success("Invitation sent.");
      setEmail("");
      qc.invalidateQueries({ queryKey: ["m-referral-info"] });
      qc.invalidateQueries({ queryKey: ["m-referrals-list"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function handleCopy() {
    if (!info?.referralCode) return;
    navigator.clipboard.writeText(info.referralCode);
    setCopied(true);
    toast.success("Referral code copied.");
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[900px] mx-auto space-y-6 sm:space-y-8">

      {/* ── Header ── */}
      <PageHeader
        eyebrow="Bring someone back into the circle"
        title="Refer a friend"
        description="Invite fellow alumni to join the community."
      />

      {/* ── Stats + code + invite ── */}
      {loadingInfo ? (
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          {Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : info && (
        <div className="space-y-4">

          {/* Stat tiles */}
          <div className="grid grid-cols-3 gap-3">
            {[
              { icon: Users,   value: info.totalReferrals,      label: "Total",     color: "var(--primary)"  },
              { icon: Check,   value: info.registeredReferrals, label: "Registered", color: "#059669"        },
              { icon: Clock,   value: info.pendingReferrals,    label: "Pending",    color: "#d97706"        },
            ].map(({ icon: Icon, value, label, color }) => (
              <div
                key={label}
                className="card p-4 flex flex-col gap-2"
              >
                <div
                  className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
                  style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
                >
                  <Icon size={14} style={{ color: "var(--primary)" }} />
                </div>
                <p
                  className="font-[family-name:var(--font-display)] font-bold tabular-nums leading-none"
                  style={{ fontSize: "1.75rem", color, letterSpacing: "-0.02em" }}
                >
                  {value}
                </p>
                <p className="text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>{label}</p>
              </div>
            ))}
          </div>

          {/* Referral code */}
          <div
            className="card p-5 space-y-3"
          >
            <div className="flex items-center gap-2">
              <Gift size={15} style={{ color: "var(--primary)" }} />
              <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
                Your referral code
              </p>
            </div>
            <div className="flex items-center gap-3">
              <div
                className="flex-1 flex items-center justify-center rounded-xl px-4 py-3 font-mono text-[18px] font-bold tracking-widest select-all"
                style={{
                  background:    "var(--secondary)",
                  border:        "1px solid var(--border)",
                  color:         "var(--foreground)",
                  letterSpacing: "0.15em",
                }}
              >
                {info.referralCode}
              </div>
              <Button
                variant="outline"
                onClick={handleCopy}
                className="shrink-0 gap-1.5 font-semibold text-[13px]"
                style={{ height: 46 }}
              >
                {copied
                  ? <><Check size={14} className="text-success" /> Copied</>
                  : <><Copy size={14} /> Copy</>}
              </Button>
            </div>
            {info.hasReferrerBadge && (
              <div
                className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full text-[12px] font-semibold"
                style={{ background: "rgba(245,158,11,0.1)", color: "#d97706", border: "1px solid rgba(245,158,11,0.2)" }}
              >
                🏆 Referrer badge earned
              </div>
            )}
          </div>

          {/* Send invite */}
          <div
            className="card p-5 space-y-3"
          >
            <div className="flex items-center gap-2">
              <Send size={14} style={{ color: "var(--primary)" }} />
              <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
                Send an invitation
              </p>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="invite-email" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                Email address
              </Label>
              <div className="flex gap-2">
                <Input
                  id="invite-email"
                  type="email"
                  placeholder="colleague@example.com"
                  value={email}
                  onChange={e => setEmail(e.target.value)}
                  onKeyDown={e => e.key === "Enter" && email.trim() && inviteMut.mutate()}
                  className="h-11 text-[14px] flex-1"
                />
                <Button
                  onClick={() => inviteMut.mutate()}
                  disabled={!email.trim() || inviteMut.isPending}
                  className="gap-2 font-semibold text-[13.5px] shrink-0"
                  style={{ height: 44 }}
                >
                  {inviteMut.isPending
                    ? <Loader2 size={15} className="animate-spin" />
                    : <><UserPlus size={15} /> Send invite</>}
                </Button>
              </div>
            </div>
          </div>

        </div>
      )}

      {/* ── Referral history ── */}
      <div className="space-y-3">
        <h2
          className="font-[family-name:var(--font-display)] font-bold"
          style={{ fontSize: "1rem", color: "var(--foreground)" }}
        >
          Referral history
        </h2>

        {loadingReferrals ? (
          <div className="space-y-2">
            {Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}
          </div>
        ) : !referrals?.length ? (
          <EmptyState
            icon={<UserPlus size={40} />}
            title="No referrals yet"
            description="Share your referral code or send an invitation above to get started."
          />
        ) : (
          <div className="space-y-2">
            {referrals.map(r => (
              <div
                key={r.id}
                className="card flex items-center justify-between gap-4 px-4 py-3.5"
              >
                <div className="min-w-0">
                  <p className="text-[14px] font-semibold truncate" style={{ color: "var(--foreground)" }}>
                    {r.referredMemberName ?? r.referredEmail}
                  </p>
                  <div className="flex flex-wrap items-center gap-3 mt-0.5">
                    {r.referredMemberName && (
                      <p className="text-[12.5px] truncate" style={{ color: "var(--muted-foreground)" }}>
                        {r.referredEmail}
                      </p>
                    )}
                    <p className="text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>
                      {formatDate(r.createdAt)}
                    </p>
                  </div>
                </div>
                <Badge
                  variant={statusVariant[r.status] ?? "secondary"}
                  className="text-[11px] font-semibold shrink-0"
                >
                  {statusLabel[r.status] ?? r.status}
                </Badge>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
