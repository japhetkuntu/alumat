"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import {
  ArrowLeft, Users, Lock, Plus, MessageSquare, Clock, Crown, Check, X, UserMinus,
  Calendar, MapPin, FileText, ExternalLink, HandCoins, Heart, Send,
} from "lucide-react";
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
import { formatDate, formatCurrency, getInitials } from "@alumni/ui";
import { cn } from "@alumni/ui";
import {
  getCommunity, joinCommunity, leaveCommunity, getCommunityMembers,
  getCommunityJoinRequests, approveJoinRequest, rejectJoinRequest, removeCommunityMember,
  getForumThreads, createThread,
  getEvents, rsvpEvent, getMyRsvps,
  getResources,
  getClassNotes, createClassNote, toggleClassNoteLike,
  getMyCampaigns,
} from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";

function safeDate(value: string | null | undefined): string {
  if (!value) return "";
  const d = new Date(value);
  if (isNaN(d.getTime())) return "";
  return formatDate(value);
}

const TABS = ["Discussion", "Events", "Resources", "Wall", "Fundraisers", "Members", "Requests"] as const;

const TAB_CAPTIONS: Partial<Record<(typeof TABS)[number], string>> = {
  Discussion: "Threaded Q&A and longer conversations",
  Wall: "Quick updates and photos, like a group feed",
};

export default function CommunityDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const qc = useQueryClient();
  const { user } = useAuth();
  const [tab, setTab] = useState<(typeof TABS)[number]>("Discussion");
  const [showNewThread, setShowNewThread] = useState(false);
  const [form, setForm] = useState({ title: "", content: "" });
  const [newNote, setNewNote] = useState("");
  const [confirmLeave, setConfirmLeave] = useState(false);

  const { data: community, isLoading } = useQuery({
    queryKey: ["m-community", id],
    queryFn: () => getCommunity(id),
  });

  const isApproved = community?.myStatus === "Approved";
  const isLeader = community?.myRole === "Leader";

  const { data: threads = [], isLoading: threadsLoading } = useQuery({
    queryKey: ["m-community-threads", id],
    queryFn: async () => (await getForumThreads(1, 50, undefined, undefined, undefined, id)).results,
    enabled: isApproved,
  });

  const { data: members = [] } = useQuery({
    queryKey: ["m-community-members", id],
    queryFn: () => getCommunityMembers(id),
    enabled: isApproved,
  });

  const { data: joinRequests = [] } = useQuery({
    queryKey: ["m-community-requests", id],
    queryFn: () => getCommunityJoinRequests(id),
    enabled: isLeader,
  });

  const { data: events = [], isLoading: eventsLoading } = useQuery({
    queryKey: ["m-community-events", id],
    queryFn: async () => (await getEvents(1, 50, undefined, id)).results,
    enabled: isApproved && tab === "Events",
  });

  const { data: myRsvps = [] } = useQuery({
    queryKey: ["m-rsvps"],
    queryFn: () => getMyRsvps(),
    enabled: isApproved && tab === "Events",
  });
  const rsvpSet = new Set(myRsvps.map((r) => r.eventId));

  const { data: resources = [], isLoading: resourcesLoading } = useQuery({
    queryKey: ["m-community-resources", id],
    queryFn: async () => (await getResources(1, 50, undefined, undefined, undefined, undefined, undefined, id)).results,
    enabled: isApproved && tab === "Resources",
  });

  const { data: notes = [], isLoading: notesLoading } = useQuery({
    queryKey: ["m-community-notes", id],
    queryFn: async () => (await getClassNotes(1, 50, id)).results,
    enabled: isApproved && tab === "Wall",
  });

  const { data: campaigns = [], isLoading: campaignsLoading } = useQuery({
    queryKey: ["m-community-campaigns", id],
    queryFn: async () => (await getMyCampaigns(1, 50, id)).results,
    enabled: isApproved && tab === "Fundraisers",
  });

  const rsvpMut = useMutation({
    mutationFn: (eventId: string) => rsvpEvent(eventId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-rsvps"] });
      qc.invalidateQueries({ queryKey: ["m-community-events", id] });
      toast.success("You're in!");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const postNoteMut = useMutation({
    mutationFn: () => createClassNote({ content: newNote, communityId: id }),
    onSuccess: () => {
      setNewNote("");
      qc.invalidateQueries({ queryKey: ["m-community-notes", id] });
      toast.success("Posted");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const likeNoteMut = useMutation({
    mutationFn: (noteId: string) => toggleClassNoteLike(noteId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["m-community-notes", id] }),
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
      qc.invalidateQueries({ queryKey: ["m-community-threads", id] });
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
      <div className="p-4 sm:p-6 lg:p-8 max-w-[1200px] mx-auto space-y-6 animate-pulse">
        <div className="h-44 rounded-3xl bg-secondary" />
        <CardSkeleton />
      </div>
    );
  }

  if (!community) {
    return (
      <div className="p-6 lg:p-10 max-w-[1200px] mx-auto">
        <EmptyState icon={<Users size={40} />} title="Community not found" description="This community may have been removed." />
      </div>
    );
  }

  const previewMembers = members.slice(0, 6);

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1200px] mx-auto pb-20 space-y-6 sm:space-y-8">
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
                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[12px] font-bold bg-white/15">
                  <Crown size={12} /> You lead this community
                </span>
              )}
              {community.myStatus === "Pending" && (
                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[12px] font-bold bg-white/15">
                  <Clock size={12} /> Request pending
                </span>
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
              {previewMembers.length > 0 && (
                <div className="flex items-center -space-x-2.5">
                  {previewMembers.map((m) => (
                    <div key={m.memberId} className="ring-2 ring-[color-mix(in_oklch,var(--primary)_85%,black)] rounded-full">
                      <UserAvatar name={m.name} size="sm" />
                    </div>
                  ))}
                </div>
              )}
              <span className="flex items-center gap-1.5 text-[13.5px] font-medium text-white/80">
                <Users size={14} /> {community.memberCount} {community.memberCount === 1 ? "member" : "members"}
              </span>
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
          description="Join this community to see its discussions and members."
        />
      ) : (
        <>
          <div className="flex gap-1.5 overflow-x-auto pb-1 -mx-1 px-1">
            {TABS.filter((t) => t !== "Requests" || isLeader).map((t: (typeof TABS)[number]) => (
              <button
                key={t}
                onClick={() => setTab(t)}
                className={cn(
                  "shrink-0 px-4 py-2.5 rounded-full text-[13.5px] font-semibold whitespace-nowrap transition-all duration-200",
                  tab === t
                    ? "bg-primary text-primary-foreground shadow-sm"
                    : "text-muted-foreground hover:text-foreground hover:bg-secondary"
                )}
              >
                {t}{t === "Requests" && joinRequests.length > 0 ? ` (${joinRequests.length})` : ""}
              </button>
            ))}
          </div>

          {TAB_CAPTIONS[tab] && (
            <p className="text-[12.5px] -mt-3 sm:-mt-5" style={{ color: "var(--muted-foreground)" }}>
              {TAB_CAPTIONS[tab]}
            </p>
          )}

          {tab === "Discussion" && (
            <div className="max-w-[720px] mx-auto space-y-4">
              <div className="flex justify-end">
                <Button size="sm" onClick={() => setShowNewThread((v) => !v)} className="gap-2">
                  <Plus size={14} /> New thread
                </Button>
              </div>

              {showNewThread && (
                <div className="card p-4 space-y-3">
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
                <div className="space-y-3">{Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}</div>
              ) : threads.length === 0 ? (
                <EmptyState icon={<MessageSquare size={36} />} title="No threads yet" description="Be the first to start a discussion in this community." />
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
            </div>
          )}

          {tab === "Events" && (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
              {eventsLoading ? (
                Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)
              ) : events.length === 0 ? (
                <div className="md:col-span-2 xl:col-span-3">
                  <EmptyState icon={<Calendar size={36} />} title="No events yet" description="Events scoped to this community will show up here." />
                </div>
              ) : (
                events.map((e) => {
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
                        <span className="flex items-center gap-1.5 text-[12px] text-muted-foreground"><Users size={11} /> {e.rsvpCount} attending</span>
                      </div>
                      <div className="mt-auto pt-3">
                        {(e.status === "Upcoming" || e.status === "Ongoing") && !hasRsvp && (
                          <Button size="sm" className="w-full" onClick={() => rsvpMut.mutate(e.id)} isLoading={rsvpMut.isPending} loadingText="RSVPing">RSVP</Button>
                        )}
                        {hasRsvp && <Badge variant="success" className="gap-1"><Check size={11} /> You're attending</Badge>}
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          )}

          {tab === "Resources" && (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
              {resourcesLoading ? (
                Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)
              ) : resources.length === 0 ? (
                <div className="md:col-span-2 xl:col-span-3">
                  <EmptyState icon={<FileText size={36} />} title="No resources yet" description="Resources shared with this community will show up here." />
                </div>
              ) : (
                resources.map((r) => (
                  <Link key={r.id} href={`/resources/${r.id}`} className="block group">
                    <div className="card p-4 flex items-start justify-between gap-3 transition-all hover:-translate-y-0.5 hover:shadow-sm h-full">
                      <div className="min-w-0">
                        <p className="text-[14px] font-semibold group-hover:text-primary transition-colors truncate">{r.title}</p>
                        <p className="text-[12px] text-muted-foreground mt-1">{r.category}{r.type ? ` · ${r.type}` : ""}</p>
                      </div>
                      <ExternalLink size={14} className="shrink-0 text-muted-foreground mt-1" />
                    </div>
                  </Link>
                ))
              )}
            </div>
          )}

          {tab === "Wall" && (
            <div className="max-w-[720px] mx-auto space-y-4">
              <div className="card p-4 space-y-3">
                <div className="flex gap-3">
                  <UserAvatar name={user?.name ?? "M"} size="sm" />
                  <Textarea
                    rows={3}
                    placeholder="Share an update with this community..."
                    value={newNote}
                    onChange={(e) => setNewNote(e.target.value)}
                    className="resize-none"
                  />
                </div>
                <div className="flex justify-end">
                  <Button size="sm" className="gap-1.5" disabled={!newNote.trim()} onClick={() => postNoteMut.mutate()} isLoading={postNoteMut.isPending}>
                    <Send size={13} /> Post
                  </Button>
                </div>
              </div>

              {notesLoading ? (
                <div className="space-y-3">{Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}</div>
              ) : notes.length === 0 ? (
                <EmptyState icon={<MessageSquare size={36} />} title="No posts yet" description="Be the first to post to this community's wall." />
              ) : (
                <div className="space-y-2">
                  {notes.map((n) => (
                    <div key={n.id} className="card p-4">
                      <div className="flex items-center gap-2">
                        <UserAvatar name={n.authorName ?? "Member"} size="sm" />
                        <span className="text-[13.5px] font-semibold">{n.authorName ?? "Member"}</span>
                        {safeDate(n.createdAt) && <span className="text-[11.5px] text-muted-foreground">{safeDate(n.createdAt)}</span>}
                      </div>
                      <p className="text-[13.5px] mt-2 whitespace-pre-wrap">{n.content}</p>
                      <button
                        onClick={() => likeNoteMut.mutate(n.id)}
                        className={cn(
                          "flex items-center gap-1.5 text-[12px] font-semibold rounded-full px-2 py-1 mt-2 transition-colors",
                          n.isLikedByMe ? "text-rose-500" : "text-muted-foreground hover:text-rose-500"
                        )}
                      >
                        <Heart size={12} className={cn(n.isLikedByMe && "fill-current")} />
                        {n.likeCount > 0 && n.likeCount} {n.isLikedByMe ? "Liked" : "Like"}
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {tab === "Fundraisers" && (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
              {campaignsLoading ? (
                Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)
              ) : campaigns.length === 0 ? (
                <div className="md:col-span-2 xl:col-span-3">
                  <EmptyState icon={<HandCoins size={36} />} title="No fundraisers yet" description="Fundraisers for this community will show up here." />
                </div>
              ) : (
                campaigns.map((c) => {
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
                })
              )}
            </div>
          )}

          {tab === "Members" && (
            <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-3">
              {members.length === 0 ? (
                <div className="sm:col-span-2 xl:col-span-3">
                  <EmptyState icon={<Users size={36} />} title="No members yet" />
                </div>
              ) : members.map((m) => (
                <div key={m.memberId} className="flex items-center justify-between gap-3 card px-4 py-3">
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
          )}

          {tab === "Requests" && isLeader && (
            <div className="max-w-[720px] mx-auto space-y-2">
              {joinRequests.length === 0 ? (
                <EmptyState icon={<Users size={36} />} title="No pending requests" />
              ) : joinRequests.map((r) => (
                <div key={r.membershipId} className="flex items-center justify-between gap-3 card px-4 py-3">
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
          )}
        </>
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
