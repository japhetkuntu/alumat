"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button, Input, Label, LoadError, FormError, Skeleton, ExternalLink, Copy, Check, MessageCircle, Pencil } from "@alumni/ui";
import { shareMessages } from "@alumni/ui";
import { getCommunityInvite, updateCommunityChannels, type CommunityChannel } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";

/** The invitation link for one community: the leader's own code, the group, and where it was shared. */
function inviteLink(origin: string, code: string, communityId: string, src: "whatsapp" | "link" | "qr") {
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

/** Connect, change or disconnect the community's WhatsApp group. */
function GroupConnect({ communityId, channels }: { communityId: string; channels: CommunityChannel[] }) {
  const qc = useQueryClient();
  const current = channels.find((c) => c.type === "WhatsApp");
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(current?.displayName ?? "");
  const [url, setUrl] = useState(current?.inviteUrl ?? "");
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: (next: { displayName: string; inviteUrl: string }[]) =>
      updateCommunityChannels(communityId, next.map((c) => ({ type: "WhatsApp", displayName: c.displayName, inviteUrl: c.inviteUrl || undefined }))),
    onSuccess: (_d, next) => {
      qc.invalidateQueries({ queryKey: ["community-invite", communityId] });
      qc.invalidateQueries({ queryKey: ["community", communityId] });
      setEditing(false); setError(null);
      toast.success(next.length ? "WhatsApp group connected" : "WhatsApp group disconnected");
    },
    onError: (e) => setError(handleApiError(e)),
  });

  if (current && !editing) {
    return (
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">WhatsApp group</p>
          <p className="mt-0.5 truncate text-[14.5px] font-semibold">{current.displayName}</p>
        </div>
        <div className="flex items-center gap-2">
          {current.inviteUrl && (
            <a href={current.inviteUrl} target="_blank" rel="noopener noreferrer" className="inline-flex h-9 items-center gap-1.5 border border-border px-3 text-[13px] font-semibold hover:bg-muted">
              Open group <ExternalLink size={12} />
            </a>
          )}
          <Button size="sm" variant="ghost" onClick={() => { setName(current.displayName); setUrl(current.inviteUrl ?? ""); setEditing(true); }}><Pencil size={13} />Edit</Button>
          <Button size="sm" variant="ghost" onClick={() => save.mutate([])} isLoading={save.isPending}>Disconnect</Button>
        </div>
      </div>
    );
  }

  if (!editing) {
    return (
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-[14.5px] font-semibold">Connect your WhatsApp group</p>
          <p className="mt-0.5 max-w-[48ch] text-[13px] text-muted-foreground">Members of this community will see a link to the group. You keep chatting there; this just points people to it.</p>
        </div>
        <Button size="sm" variant="outline" onClick={() => setEditing(true)}>Connect group</Button>
      </div>
    );
  }

  return (
    <form
      className="space-y-3"
      onSubmit={(e) => { e.preventDefault(); setError(null); save.mutate([{ displayName: name, inviteUrl: url }]); }}
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-1.5"><Label htmlFor="grp-name">Group name</Label><Input id="grp-name" value={name} maxLength={100} onChange={(e) => setName(e.target.value)} required /></div>
        <div className="space-y-1.5"><Label htmlFor="grp-url">Invite link <span className="font-normal text-muted-foreground">(optional)</span></Label><Input id="grp-url" type="url" inputMode="url" placeholder="https://chat.whatsapp.com/…" value={url} onChange={(e) => setUrl(e.target.value)} /></div>
      </div>
      <p className="text-[12px] text-muted-foreground">In WhatsApp, open the group, then Group info, then &ldquo;Invite via link&rdquo; and copy it. Only people already in this community see it here.</p>
      <FormError message={error} />
      <div className="flex gap-2">
        <Button type="submit" size="sm" isLoading={save.isPending} loadingText="Saving">Save</Button>
        <Button type="button" size="sm" variant="ghost" onClick={() => { setEditing(false); setError(null); }}>Cancel</Button>
      </div>
    </form>
  );
}

/**
 * For a community leader: an invitation they can drop into their existing group, how it is going, and the group link.
 * Anyone who joins through it lands in this community, with no hunting for it afterwards.
 */
export function CommunityInvitePanel({ communityId, communityName, description }: { communityId: string; communityName: string; description?: string | null }) {
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["community-invite", communityId],
    queryFn: () => getCommunityInvite(communityId),
    staleTime: 30_000,
  });
  const [copied, setCopied] = useState(false);

  if (isLoading) return <PanelSkeleton />;
  if (isError || !data) return <div className="card p-2"><LoadError title="We couldn't load your invitation" description="Check your connection, then try again." onRetry={() => refetch()} /></div>;

  const origin = typeof window !== "undefined" ? window.location.origin : "";
  const waText = `${shareMessages.community({ name: communityName, description })}\n${inviteLink(origin, data.referralCode, communityId, "whatsapp")}`;
  const copyLink = inviteLink(origin, data.referralCode, communityId, "link");

  const copy = async () => {
    try { await navigator.clipboard.writeText(copyLink); setCopied(true); window.setTimeout(() => setCopied(false), 2000); }
    catch { toast.error("Couldn't copy. Select the link and copy it manually."); }
  };

  return (
    <section className="card space-y-6 p-5 sm:p-6" aria-labelledby="invite-heading">
      <div>
        <h2 id="invite-heading" className="text-[17px] font-bold">Bring your group in</h2>
        <p className="mt-1 max-w-[56ch] text-[13.5px] leading-relaxed text-muted-foreground">
          Share this invitation in your group chat. People who join through it land straight in {communityName}, already approved.
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
          <p className="mt-1">Send it to your group below. As people join, you will see the numbers move here.</p>
        </div>
      )}

      <div className="space-y-3">
        <Label htmlFor="invite-link">Your invitation link</Label>
        <Input id="invite-link" readOnly value={copyLink} onFocus={(e) => e.currentTarget.select()} className="font-mono text-[12.5px]" />
        <div className="flex flex-wrap gap-2">
          <a
            href={`https://wa.me/?text=${encodeURIComponent(waText)}`}
            target="_blank" rel="noopener noreferrer"
            className="inline-flex min-h-11 flex-1 items-center justify-center gap-2 bg-primary px-5 text-[14px] font-semibold text-primary-foreground hover:opacity-90 sm:flex-none"
          >
            <MessageCircle size={16} /> Share to WhatsApp
          </a>
          <Button variant="outline" className="min-h-11 flex-1 sm:flex-none" onClick={copy}>
            {copied ? <><Check size={14} />Copied</> : <><Copy size={14} />Copy link</>}
          </Button>
        </div>
      </div>

      <div className="border-t border-border/60 pt-5">
        <GroupConnect key={data.channels.map((c) => c.displayName + c.inviteUrl).join("|")} communityId={communityId} channels={data.channels} />
      </div>
    </section>
  );
}

/** What every approved member sees when the community's WhatsApp group is connected. */
export function CommunityGroupLink({ channels }: { channels?: CommunityChannel[] | null }) {
  const group = channels?.find((c) => c.type === "WhatsApp" && c.inviteUrl);
  if (!group) return null;
  return (
    <a
      href={group.inviteUrl!} target="_blank" rel="noopener noreferrer"
      className="flex items-center justify-between gap-3 border border-border px-4 py-3.5 transition-colors hover:bg-muted/50"
    >
      <span className="flex min-w-0 items-center gap-3">
        <MessageCircle size={18} className="shrink-0 text-primary" />
        <span className="min-w-0">
          <span className="block text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">Our WhatsApp group</span>
          <span className="block truncate text-[14.5px] font-semibold">{group.displayName}</span>
        </span>
      </span>
      <span className="flex shrink-0 items-center gap-1.5 text-[13px] font-semibold text-primary">Open <ExternalLink size={12} /></span>
    </a>
  );
}
