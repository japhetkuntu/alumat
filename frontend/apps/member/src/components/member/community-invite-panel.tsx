"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button, Input, Label, LoadError, Skeleton, Copy, Check } from "@alumni/ui";
import { getCommunityInvite } from "@/lib/member-api";

/** The invitation link for one community: the leader's own code, the group, and where it was shared. */
function inviteLink(origin: string, code: string, communityId: string, src: "link" | "qr") {
  const params = new URLSearchParams({ ref: code, community: communityId, src });
  return `${origin}/register?${params.toString()}`;
}

function Figure({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <dd className="text-[22px] font-bold tabular-nums leading-none">{value.toLocaleString("en-GH")}</dd>
      <dt className="mt-1.5 text-[12px] text-muted-foreground leading-snug">{label}</dt>
    </div>
  );
}

function PanelSkeleton() {
  return (
    <div className="card p-5 sm:p-6 space-y-5" aria-busy="true" aria-label="Loading invitation details">
      <div className="space-y-2"><Skeleton className="h-5 w-48" /><Skeleton className="h-3.5 w-72 max-w-full" /></div>
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">{Array.from({ length: 4 }).map((_, i) => <div key={i} className="space-y-2"><Skeleton className="h-6 w-10" /><Skeleton className="h-3 w-20" /></div>)}</div>
      <Skeleton className="h-11 w-full" />
    </div>
  );
}

/**
 * For a community leader: an invitation link they can send to their people, and how it is going.
 * Anyone who joins through it lands in this community, with no hunting for it afterwards.
 */
export function CommunityInvitePanel({ communityId, communityName }: { communityId: string; communityName: string }) {
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["community-invite", communityId],
    queryFn: () => getCommunityInvite(communityId),
    staleTime: 30_000,
  });
  const [copied, setCopied] = useState(false);

  if (isLoading) return <PanelSkeleton />;
  if (isError || !data) return <div className="card p-2"><LoadError title="We couldn't load your invitation" description="Check your connection, then try again." onRetry={() => refetch()} /></div>;

  const origin = typeof window !== "undefined" ? window.location.origin : "";
  const copyLink = inviteLink(origin, data.referralCode, communityId, "link");

  const copy = async () => {
    try { await navigator.clipboard.writeText(copyLink); setCopied(true); window.setTimeout(() => setCopied(false), 2000); }
    catch { toast.error("Couldn't copy. Select the link and copy it manually."); }
  };

  return (
    <section className="card space-y-6 p-5 sm:p-6" aria-labelledby="invite-heading">
      <div>
        <h2 id="invite-heading" className="text-[17px] font-bold">Invite people to join</h2>
        <p className="mt-1 max-w-[56ch] text-[13.5px] leading-relaxed text-muted-foreground">
          Send this invitation to the people you want in the group. People who join through it land straight in {communityName}, already approved.
        </p>
      </div>

      <dl className="grid grid-cols-2 gap-x-6 gap-y-5 border-y border-border/60 py-5 sm:grid-cols-4">
        <Figure label="Members" value={data.approvedMembers} />
        <Figure label="Waiting for approval" value={data.pendingRequests} />
        <Figure label="Joined by invitation" value={data.joinedViaInvites} />
        <Figure label="In the last 30 days" value={data.joinedLast30Days} />
      </dl>

      {data.joinedViaInvites === 0 && (
        <div className="border border-dashed border-border px-4 py-4 text-[13.5px] text-muted-foreground">
          <p className="font-semibold text-foreground">No one has joined through your invitation yet.</p>
          <p className="mt-1">Send the link below to your people. As people join, you will see the numbers move here.</p>
        </div>
      )}

      <div className="space-y-3">
        <Label htmlFor="invite-link">Your invitation link</Label>
        <Input id="invite-link" readOnly value={copyLink} onFocus={(e) => e.currentTarget.select()} className="font-mono text-[12.5px]" />
        <div className="flex flex-wrap gap-2">
          <Button className="min-h-11 flex-1 sm:flex-none" onClick={copy}>
            {copied ? <><Check size={14} />Copied</> : <><Copy size={14} />Copy link</>}
          </Button>
        </div>
      </div>
    </section>
  );
}
