"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { UserPlus, Copy, Check, Send, Loader2, Gift } from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { formatDate } from "@/lib/utils";
import { getReferralInfo, sendReferralInvite, getMyReferrals } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";

export default function ReferralsPage() {
  const [email, setEmail] = useState("");
  const [copied, setCopied] = useState(false);
  const qc = useQueryClient();

  const { data: info, isLoading: loadingInfo } = useQuery({
    queryKey: ["m-referral-info"],
    queryFn: getReferralInfo,
  });

  const { data: referrals, isLoading: loadingReferrals } = useQuery({
    queryKey: ["m-referrals-list"],
    queryFn: getMyReferrals,
  });

  const inviteMut = useMutation({
    mutationFn: () => sendReferralInvite(email),
    onSuccess: () => {
      toast.success("Invitation sent!");
      setEmail("");
      qc.invalidateQueries({ queryKey: ["m-referral-info"] });
      qc.invalidateQueries({ queryKey: ["m-referrals-list"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const handleCopy = () => {
    if (info?.referralCode) {
      navigator.clipboard.writeText(info.referralCode);
      setCopied(true);
      toast.success("Referral code copied!");
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const statusVariant: Record<string, "info" | "success" | "secondary"> = {
    Pending: "info",
    Registered: "success",
    MembershipPaid: "success",
  };

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-6 sm:space-y-8 lg:space-y-10 selection:bg-primary/20">
      <header className="space-y-2 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <h1 className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tight text-foreground">
          Refer a Friend
        </h1>
        <p className="text-muted-foreground text-sm sm:text-base lg:text-lg font-medium leading-relaxed max-w-2xl">
          Invite fellow UMaT alumni to join the community. Earn recognition and badges for successful referrals!
        </p>
      </header>

      {loadingInfo ? (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : info && (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <Card>
              <CardContent className="p-4 text-center">
                <p className="text-3xl font-bold text-primary">{info.totalReferrals}</p>
                <p className="text-sm text-muted-foreground">Total Referrals</p>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-4 text-center">
                <p className="text-3xl font-bold text-green-600">{info.registeredReferrals}</p>
                <p className="text-sm text-muted-foreground">Registered</p>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-4 text-center">
                <p className="text-3xl font-bold text-amber-500">{info.pendingReferrals}</p>
                <p className="text-sm text-muted-foreground">Pending</p>
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-base flex items-center gap-2">
                <Gift className="w-5 h-5 text-primary" />
                Your Referral Code
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex items-center gap-2">
                <code className="flex-1 px-4 py-2.5 bg-muted rounded-lg font-mono text-lg tracking-wider text-center">
                  {info.referralCode}
                </code>
                <Button variant="outline" size="icon" onClick={handleCopy}>
                  {copied ? <Check className="w-4 h-4 text-green-500" /> : <Copy className="w-4 h-4" />}
                </Button>
              </div>
              {info.hasReferrerBadge && (
                <Badge variant="success" className="gap-1">
                  🏆 Referrer Badge Earned
                </Badge>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base flex items-center gap-2">
                <Send className="w-5 h-5 text-primary" />
                Send Invitation
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex gap-2">
                <Input
                  type="email"
                  placeholder="Enter email address"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  onKeyDown={(e) => e.key === "Enter" && email.trim() && inviteMut.mutate()}
                />
                <Button onClick={() => inviteMut.mutate()} disabled={!email.trim() || inviteMut.isPending}>
                  {inviteMut.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <UserPlus className="w-4 h-4" />}
                </Button>
              </div>
            </CardContent>
          </Card>
        </>
      )}

      <div className="space-y-4">
        <h2 className="text-lg font-bold">Referral History</h2>
        {loadingReferrals ? (
          <div className="space-y-3">
            {Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}
          </div>
        ) : !referrals?.length ? (
          <EmptyState icon={<UserPlus size={48} />} title="No referrals yet" description="Start inviting fellow alumni to grow the community!" />
        ) : (
          <div className="space-y-2">
            {referrals.map((r) => (
              <Card key={r.id}>
                <CardContent className="flex items-center justify-between p-4">
                  <div>
                    <p className="font-medium text-sm">{r.referredEmail}</p>
                    {r.referredMemberName && <p className="text-xs text-muted-foreground">{r.referredMemberName}</p>}
                    <p className="text-xs text-muted-foreground">{formatDate(r.createdAt)}</p>
                  </div>
                  <Badge variant={statusVariant[r.status] ?? "secondary"}>{r.status}</Badge>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
