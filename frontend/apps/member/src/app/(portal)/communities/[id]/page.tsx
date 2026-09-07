"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import {
  ArrowLeft, Users, Lock, Plus, MessageSquare, Clock, Crown, Check, X, UserMinus,
  Calendar, MapPin, FileText, ExternalLink, HandCoins, ArrowRight, Bell,
} from "@alumni/ui";
import { toast } from "sonner";
import { Button } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@alumni/ui";
import { formatDate, formatCurrency } from "@alumni/ui";
import {
  getCommunity, joinCommunity, leaveCommunity, getCommunityMembers,
  getCommunityJoinRequests, approveJoinRequest, rejectJoinRequest, removeCommunityMember,
  getForumThreads, createThread,
  getEvents, rsvpEvent, getMyRsvps,
  getResources,
  getMyCampaigns,
} from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";

function safeDate(value: string | null | undefined): string {
  if (!value) return "";
  const d = new Date(value);
  if (isNaN(d.getTime())) return "";
  return formatDate(value);
}

/** A summary section's header — a title and a "see everything" link out to the relevant global feed, pre-filtered to this community. */
function SectionHeader({ title, seeAllHref }: { title: string; seeAllHref: string }) {
  return (
    <div className="flex items-center justify-between mb-3">
      <h2 className="text-[15px] font-bold" style={{ color: "var(--foreground)" }}>{title}</h2>
      <Link href={seeAllHref} className="flex items-center gap-1 text-[12.5px] font-semibold text-primary hover:underline">
        See all <ArrowRight size={12} />
      </Link>
    </div>
  );
}

export default function CommunityDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const qc = useQueryClient();
  const [showNewThread, setShowNewThread] = useState(false);
  const [form, setForm] = useState({ title: "", content: "" });
  const [confirmLeave, setConfirmLeave] = useState(false);
  const [membersOpen, setMembersOpen] = useState(false);
  const [requestsOpen, setRequestsOpen] = useState(false);

  const { data: community, isLoading } = useQuery({
    queryKey: ["m-community", id],
    queryFn: () => getCommunity(id),
  });

  const isApproved = community?.myStatus === "Approved";
  const isLeader = community?.myRole === "Leader";

  // Small previews only — the full lists live on the global feeds, filtered
  // to this community via `?communityId=`, so nothing here duplicates a page.
  const { data: threads = [], isLoading: threadsLoading } = useQuery({
    queryKey: ["m-community-threads-preview", id],
    queryFn: async () => (await getForumThreads(1, 3, undefined, undefined, undefined, id)).results,
    enabled: isApproved,
  });

  const { data: events = [], isLoading: eventsLoading } = useQuery({
    queryKey: ["m-community-events-preview", id],
    queryFn: async () => (await getEvents(1, 2, undefined, id)).results,
    enabled: isApproved,
  });

  const { data: myRsvps = [] } = useQuery({
    queryKey: ["m-rsvps"],
    queryFn: () => getMyRsvps(),
    enabled: isApproved,
  });
  const rsvpSet = new Set(myRsvps.map((r) => r.eventId));

  const { data: resources = [], isLoading: resourcesLoading } = useQuery({
    queryKey: ["m-community-resources-preview", id],
    queryFn: async () => (await getResources(1, 3, undefined, undefined, undefined, undefined, undefined, id)).results,
    enabled: isApproved,
  });

  const { data: campaigns = [], isLoading: campaignsLoading } = useQuery({
    queryKey: ["m-community-campaigns-preview", id],
    queryFn: async () => (await getMyCampaigns(1, 2, id)).results,
    enabled: isApproved,
  });

  const { data: members = [] } = useQuery({
    queryKey: ["m-community-members", id],
    queryFn: () => getCommunityMembers(id),
    enabled: isApproved, // fetched eagerly — the hero's avatar stack needs it before the drawer even opens
  });

  const { data: joinRequests = [] } = useQuery({
    queryKey: ["m-community-requests", id],
    queryFn: () => getCommunityJoinRequests(id),
    enabled: isLeader,
  });

  const rsvpMut = useMutation({
    mutationFn: (eventId: string) => rsvpEvent(eventId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-rsvps"] });
      qc.invalidateQueries({ queryKey: ["m-community-events-preview", id] });
      toast.success("You're in!");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const joinMut = useMutation({
    mutationFn: () => joinCommunity(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["m-community", id] }); toast.success("Join request sent"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const leaveMut = useMutation({
    mutationFn: () => leaveCommunity(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["m-community", id] }); toast.success("You left the community"); router.push("/communities"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const createThreadMut = useMutation({
    mutationFn: () => createThread({ title: form.title, content: form.content, communityId: id }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-community-threads-preview", id] });
      setShowNewThread(false);
      setForm({ title: "", content: "" });
      toast.success("Thread created");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const decideMut = useMutation({
    mutationFn: ({ membershipId, approve }: { membershipId: string; approve: boolean }) =>
      approve ? approveJoinRequest(id, membershipId) : rejectJoinRequest(id, membershipId),
    onSuccess: (_d, vars) => {
      qc.invalidateQueries({ queryKey: ["m-community-requests", id] });
      qc.invalidateQueries({ queryKey: ["m-community-members", id] });
      qc.invalidateQueries({ queryKey: ["m-community", id] });
      toast.success(vars.approve ? "Request approved" : "Request rejected");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const removeMut = useMutation({
    mutationFn: (memberId: string) => removeCommunityMember(id, memberId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-community-members", id] });
      toast.success("Member removed");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (isLoading) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 animate-pulse">
        <div className="h-44 rounded-3xl bg-secondary" />
        <CardSkeleton />
      </div>
    );
  }

  if (!community) {
    return (
      <div className="p-6 lg:p-10 max-w-[1400px] mx-auto">
        <EmptyState icon={<Users size={40} />} title="Community not found" description="This community may have been removed." />
      </div>
    );
  }

  const previewMembers = members.slice(0, 6);

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto pb-20 space-y-6 sm:space-y-8">
      <Link href="/communities" className="flex items-center gap-1.5 text-[13.5px] font-semibold hover:underline w-fit" style={{ color: "var(--muted-foreground)" }}>
        <ArrowLeft size={15} /> Back to communities
      </Link>

      {/* ── Hero ── */}
      <div
        className="relative overflow-hidden rounded-3xl p-6 sm:p-10"
        style={{
          background: "linear-gradient(135deg, var(--primary) 0%, color-mix(in oklch, var(--primary) 70%, black) 100%)",
          color: "white",
        }}
      >
        <div className="absolute -right-20 -top-20 w-72 h-72 rounded-full border border-white/10" />
        <div className="absolute -right-10 -top-10 w-44 h-44 rounded-full border border-white/10" />

        <div className="relative flex flex-col sm:flex-row sm:items-start sm:justify-between gap-6">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2.5 mb-3">
              {isLeader && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1 text-[12px] font-bold bg-white/15">
                  <Crown size={12} /> You lead this community
                </span>
              )}
              {community.myStatus === "Pending" && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1 text-[12px] font-bold bg-white/15">
                  <Clock size={12} /> Request pending
                </span>
              )}
              {isLeader && joinRequests.length > 0 && (
                <button
                  onClick={() => setRequestsOpen(true)}
                  className="inline-flex items-center gap-1.5 px-3 py-1 text-[12px] font-bold bg-white/15 hover:bg-white/25 transition-colors"
                >
                  <Bell size={12} /> {joinRequests.length} join {joinRequests.length === 1 ? "request" : "requests"}
                </button>
              )}
            </div>
            <h1
              className="font-[family-name:var(--font-display)] font-bold leading-tight"
              style={{ fontSize: "clamp(1.75rem, 3.5vw, 2.75rem)" }}
            >
              {community.name}
            </h1>
            {community.description && (
              <p className="mt-3 max-w-[60ch] text-[15px] leading-relaxed text-white/80">
                {community.description}
              </p>
            )}

            <div className="flex flex-wrap items-center gap-4 mt-6">
              <button onClick={() => setMembersOpen(true)} className="flex items-center gap-3 hover:opacity-90 transition-opacity">
                {previewMembers.length > 0 && (
                  <div className="flex items-center -space-x-2.5">
                    {previewMembers.map((m) => (
                      <div key={m.memberId} className="ring-2 ring-[color-mix(in_oklch,var(--primary)_85%,black)] rounded-full">
                        <UserAvatar name={m.name} size="sm" />
                      </div>
                    ))}
                  </div>
                )}
                <span className="flex items-center gap-1.5 text-[13.5px] font-medium text-white/80 underline decoration-white/30 underline-offset-2">
                  <Users size={14} /> {community.memberCount} {community.memberCount === 1 ? "member" : "members"}
                </span>
              </button>
            </div>
          </div>

          <div className="shrink-0">
            {!community.myStatus && (
              <Button
                onClick={() => joinMut.mutate()}
                isLoading={joinMut.isPending}
                loadingText="Requesting"
                className="bg-white font-bold hover:bg-white/90"
                style={{ color: "var(--primary)", height: 44 }}
              >
                Request to join
              </Button>
            )}
            {isApproved && !isLeader && (
              <Button
                variant="outline"
                onClick={() => setConfirmLeave(true)}
                isLoading={leaveMut.isPending}
                loadingText="Leaving"
                className="bg-transparent border-white/40 text-white hover:bg-white/10 hover:text-white hover:border-white/60 font-semibold"
                style={{ height: 44 }}
              >
                Leave community
              </Button>
            )}
          </div>
        </div>
      </div>

      {!isApproved ? (
        <EmptyState
          icon={<Lock size={40} />}
          title="Members only"
          description="Join this community to see what's happening here."
        />
      ) : (
        <div className="space-y-8">
          {/* ── Fundraisers ── */}
          <section>
            <SectionHeader title="Fundraisers" seeAllHref={`/contributions?communityId=${id}`} />
            {campaignsLoading ? (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">{Array.from({ length: 2 }).map((_, i) => <CardSkeleton key={i} />)}</div>
            ) : campaigns.length === 0 ? (
              <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>No fundraisers here right now.</p>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {campaigns.map((c) => {
                  const pct = c.targetAmount > 0 ? Math.min(100, Math.round((c.collectedAmount / c.targetAmount) * 100)) : 0;
                  return (
                    <Link key={c.id} href={`/contributions/${c.id}`} className="block group">
                      <div className="card p-4 transition-all hover:-translate-y-0.5 hover:shadow-sm h-full">
                        <div className="flex items-start justify-between gap-3">
                          <p className="text-[14px] font-semibold group-hover:text-primary transition-colors">{c.title}</p>
                          <Badge variant="info" className="shrink-0 text-[10px]">{c.status}</Badge>
                        </div>
                        <div className="mt-2 h-1.5 rounded-full overflow-hidden" style={{ background: "var(--secondary)" }}>
                          <div className="h-full rounded-full" style={{ width: `${pct}%`, background: "var(--primary)" }} />
                        </div>
                        <p className="text-[12px] text-muted-foreground mt-1.5">
                          {formatCurrency(c.collectedAmount)} raised of {formatCurrency(c.targetAmount)} · {pct}%
                        </p>
                      </div>
                    </Link>
                  );
                })}
              </div>
            )}
          </section>

          {/* ── Events ── */}
          <section>
            <SectionHeader title="Upcoming events" seeAllHref={`/events?communityId=${id}`} />
            {eventsLoading ? (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">{Array.from({ length: 2 }).map((_, i) => <CardSkeleton key={i} />)}</div>
            ) : events.length === 0 ? (
              <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>No events scheduled here right now.</p>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {events.map((e) => {
                  const hasRsvp = rsvpSet.has(e.id);
                  return (
                    <div key={e.id} className="card p-4 flex flex-col">
                      <div className="flex items-start justify-between gap-3">
                        <Link href={`/events/${e.id}`} className="min-w-0">
                          <p className="text-[14px] font-semibold hover:text-primary transition-colors truncate">{e.title}</p>
                        </Link>
                        <Badge variant="info" className="shrink-0 text-[10px]">{e.status}</Badge>
                      </div>
                      <div className="flex flex-col gap-1.5 mt-2.5">
                        {safeDate(e.startDate) && <span className="flex items-center gap-1.5 text-[12px] text-muted-foreground"><Clock size={11} /> {safeDate(e.startDate)}</span>}
                        <span className="flex items-center gap-1.5 text-[12px] text-muted-foreground"><MapPin size={11} /> {e.venue}</span>
                      </div>
                      <div className="mt-auto pt-3">
                        {(e.status === "Upcoming" || e.status === "Ongoing") && !hasRsvp && (
                          <Button size="sm" className="w-full" onClick={() => rsvpMut.mutate(e.id)} isLoading={rsvpMut.isPending} loadingText="RSVPing">RSVP</Button>
                        )}
                        {hasRsvp && <Badge variant="success" className="gap-1"><Check size={11} /> You're attending</Badge>}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </section>

          {/* ── Discussion ── */}
          <section>
            <div className="flex items-center justify-between mb-3">
              <SectionHeader title="Discussion" seeAllHref={`/forum?communityId=${id}`} />
              <Button size="sm" variant="outline" onClick={() => setShowNewThread((v) => !v)} className="gap-1.5 -mt-3">
                <Plus size={13} /> New thread
              </Button>
            </div>

            {showNewThread && (
              <div className="card p-4 space-y-3 mb-3">
                <div className="space-y-1.5">
                  <Label>Title</Label>
                  <Input value={form.title} onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))} placeholder="What's on your mind?" />
                </div>
                <div className="space-y-1.5">
                  <Label>Message</Label>
                  <Textarea rows={3} value={form.content} onChange={(e) => setForm((f) => ({ ...f, content: e.target.value }))} />
                </div>
                <div className="flex gap-2">
                  <Button size="sm" onClick={() => createThreadMut.mutate()} isLoading={createThreadMut.isPending} disabled={!form.title || !form.content}>Post</Button>
                  <Button size="sm" variant="outline" onClick={() => setShowNewThread(false)}>Cancel</Button>
                </div>
              </div>
            )}

            {threadsLoading ? (
              <div className="space-y-3">{Array.from({ length: 2 }).map((_, i) => <CardSkeleton key={i} />)}</div>
            ) : threads.length === 0 ? (
              <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>No discussion yet — be the first to post.</p>
            ) : (
              <div className="space-y-2">
                {threads.map((t) => (
                  <Link key={t.id} href={`/forum/${t.id}`} className="block group">
                    <div className="card p-4 transition-all hover:-translate-y-0.5 hover:shadow-sm">
                      <p className="text-[14px] font-semibold group-hover:text-primary transition-colors">{t.title}</p>
                      <div className="flex items-center gap-4 mt-2">
                        {safeDate(t.createdAt) && <span className="flex items-center gap-1 text-[12px] text-muted-foreground"><Clock size={11} /> {safeDate(t.createdAt)}</span>}
                        <span className="flex items-center gap-1 text-[12px] text-muted-foreground"><MessageSquare size={11} /> {t.replyCount ?? 0} {(t.replyCount ?? 0) === 1 ? "reply" : "replies"}</span>
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </section>

          {/* ── Resources ── */}
          <section>
            <SectionHeader title="Resources" seeAllHref={`/resources?communityId=${id}`} />
            {resourcesLoading ? (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">{Array.from({ length: 2 }).map((_, i) => <CardSkeleton key={i} />)}</div>
            ) : resources.length === 0 ? (
              <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>No resources shared here yet.</p>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {resources.map((r) => (
                  <Link key={r.id} href={`/resources/${r.id}`} className="block group">
                    <div className="card p-4 flex items-start justify-between gap-3 transition-all hover:-translate-y-0.5 hover:shadow-sm h-full">
                      <div className="min-w-0">
                        <p className="text-[14px] font-semibold group-hover:text-primary transition-colors truncate">{r.title}</p>
                        <p className="text-[12px] text-muted-foreground mt-1">{r.category}{r.type ? ` · ${r.type}` : ""}</p>
                      </div>
                      <ExternalLink size={14} className="shrink-0 text-muted-foreground mt-1" />
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </section>
        </div>
      )}

      {/* ── Members drawer ── */}
      <Dialog open={membersOpen} onOpenChange={setMembersOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>{community.memberCount} {community.memberCount === 1 ? "member" : "members"}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2 max-h-[60vh] overflow-y-auto -mx-1 px-1">
            {members.length === 0 ? (
              <EmptyState icon={<Users size={32} />} title="No members yet" />
            ) : members.map((m) => (
              <div key={m.memberId} className="flex items-center justify-between gap-3 card px-3 py-2.5">
                <div className="flex items-center gap-3 min-w-0">
                  <UserAvatar name={m.name} size="sm" />
                  <div className="min-w-0">
                    <p className="text-[13.5px] font-semibold truncate">{m.name}</p>
                    {m.role === "Leader" && <Badge variant="info" className="text-[10px] gap-1 mt-0.5"><Crown size={9} /> Leader</Badge>}
                  </div>
                </div>
                {isLeader && m.role !== "Leader" && (
                  <Button size="sm" variant="ghost" className="gap-1.5 text-destructive shrink-0" onClick={() => removeMut.mutate(m.memberId)} isLoading={removeMut.isPending}>
                    <UserMinus size={13} /> Remove
                  </Button>
                )}
              </div>
            ))}
          </div>
        </DialogContent>
      </Dialog>

      {/* ── Join requests drawer (leader only) ── */}
      {isLeader && (
        <Dialog open={requestsOpen} onOpenChange={setRequestsOpen}>
          <DialogContent className="max-w-md">
            <DialogHeader>
              <DialogTitle>Join requests</DialogTitle>
            </DialogHeader>
            <div className="space-y-2 max-h-[60vh] overflow-y-auto -mx-1 px-1">
              {joinRequests.length === 0 ? (
                <EmptyState icon={<Users size={32} />} title="No pending requests" />
              ) : joinRequests.map((r) => (
                <div key={r.membershipId} className="flex items-center justify-between gap-3 card px-3 py-2.5">
                  <div className="flex items-center gap-3 min-w-0">
                    <UserAvatar name={r.memberName} size="sm" />
                    <div className="min-w-0">
                      <p className="text-[13.5px] font-semibold truncate">{r.memberName}</p>
                      <p className="text-[11.5px] text-muted-foreground">{safeDate(r.requestedAt)}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 shrink-0">
                    <Button size="sm" variant="outline" className="gap-1.5" onClick={() => decideMut.mutate({ membershipId: r.membershipId, approve: true })} isLoading={decideMut.isPending}>
                      <Check size={13} /> Approve
                    </Button>
                    <Button size="sm" variant="ghost" className="gap-1.5 text-destructive" onClick={() => decideMut.mutate({ membershipId: r.membershipId, approve: false })} isLoading={decideMut.isPending}>
                      <X size={13} /> Reject
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          </DialogContent>
        </Dialog>
      )}

      <ConfirmModal
        open={confirmLeave}
        title="Leave this community?"
        message="You'll lose access to its discussions, wall, resources, and fundraisers unless a leader approves your request to rejoin."
        confirmLabel="Leave community"
        variant="destructive"
        isLoading={leaveMut.isPending}
        onConfirm={() => { leaveMut.mutate(); setConfirmLeave(false); }}
        onCancel={() => setConfirmLeave(false)}
      />
    </div>
  );
}
