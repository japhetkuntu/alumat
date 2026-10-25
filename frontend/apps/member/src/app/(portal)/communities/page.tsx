"use client";

import { useState } from "react";
import { richTextToPlain } from "@alumni/ui";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { NotifyMeButton } from "@/components/member/notify-me-button";
import { useRouter } from "next/navigation";
import { Users, Clock, Crown, Search } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { toast } from "sonner";
import { Button } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { getCommunities, joinCommunity, type Community } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { PageHeader } from "@alumni/ui";

/** A steady colour per group, so a group without a cover photo still has a face of its own instead of a grey box. */
function placeholderHue(name: string) {
  let h = 0;
  for (const ch of name) h = (h * 31 + ch.charCodeAt(0)) % 360;
  return h;
}

function initialsOf(name: string) {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join("");
}

/** Where the member stands with a group: led first, then joined, then waiting, then not yet. */
function standing(c: Community) {
  if (c.myRole === "Leader") return 0;
  if (c.myStatus === "Approved") return 1;
  if (c.myStatus === "Pending") return 2;
  return 3;
}

function StatusAction({ community, onJoin, joining }: { community: Community; onJoin: () => void; joining: boolean }) {
  if (community.myRole === "Leader") {
    return <Badge variant="info" className="gap-1"><Crown size={11} /> You lead this</Badge>;
  }
  if (community.myStatus === "Approved") {
    return <Badge variant="success">Member</Badge>;
  }
  if (community.myStatus === "Pending") {
    return <Badge variant="warning" className="gap-1"><Clock size={11} /> Request pending</Badge>;
  }
  return (
    <Button size="sm" onClick={onJoin} isLoading={joining} loadingText="Requesting">
      Request to join
    </Button>
  );
}

function GroupSection({ title, groups, onOpen, onJoin, joiningId }: { title?: string; groups: Community[]; onOpen: (id: string) => void; onJoin: (id: string) => void; joiningId?: string }) {
  return (
    <section className="space-y-3">
      {title && <h2 className="text-[15px] font-bold">{title}</h2>}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {groups.map((c) => (
          <div
            key={c.id}
            role="link"
            tabIndex={0}
            onClick={() => onOpen(c.id)}
            onKeyDown={(e) => { if (e.key === "Enter") onOpen(c.id); }}
            className="card group flex cursor-pointer flex-col overflow-hidden transition-all hover:-translate-y-0.5 hover:shadow-[var(--card-shadow-hover)]"
          >
            <div className="relative shrink-0 overflow-hidden" style={{ height: 104 }}>
              {c.coverImageUrl ? (
                <img src={c.coverImageUrl} alt="" className="absolute inset-0 h-full w-full object-cover transition-transform duration-500 group-hover:scale-[1.03]" loading="lazy" />
              ) : (
                <div
                  className="absolute inset-0 flex items-center justify-center font-[family-name:var(--font-display)] text-[34px] font-bold tracking-wide"
                  style={{ background: `oklch(0.93 0.04 ${placeholderHue(c.name)})`, color: `oklch(0.42 0.09 ${placeholderHue(c.name)})` }}
                  aria-hidden="true"
                >
                  {initialsOf(c.name)}
                </div>
              )}
            </div>
            <div className="flex flex-1 flex-col gap-3 p-5">
              <div className="min-w-0 space-y-1.5">
                {c.kind && <p className="text-[11.5px] font-semibold uppercase tracking-wide text-muted-foreground">{c.kind}</p>}
                <p className="font-[family-name:var(--font-display)] text-[18px] font-semibold leading-snug transition-colors group-hover:text-primary">{c.name}</p>
                {c.description && <p className="line-clamp-2 text-[13px] text-muted-foreground">{richTextToPlain(c.description)}</p>}
              </div>
              <div className="mt-auto flex flex-wrap items-center justify-between gap-x-3 gap-y-2 border-t border-border pt-3">
                <span className="flex items-center gap-1.5 whitespace-nowrap text-[12.5px] text-muted-foreground">
                  <Users size={13} /> {c.memberCount} {c.memberCount === 1 ? "member" : "members"}
                </span>
                <div onClick={(e) => e.stopPropagation()}>
                  <StatusAction community={c} onJoin={() => onJoin(c.id)} joining={joiningId === c.id} />
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

export default function CommunitiesPage() {
  const qc = useQueryClient();
  const router = useRouter();

  const { data: communities = [], isLoading } = useQuery({
    queryKey: ["m-communities"],
    queryFn: getCommunities,
  });

  const joinMut = useMutation({
    mutationFn: (id: string) => joinCommunity(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["m-communities"] }); toast.success("Join request sent. A group leader will review it"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  // Filter by what the administrators call each group (Chapter, Committee…) — only offered when there is more than one kind.
  const [kindFilter, setKindFilter] = useState<string | null>(null);
  const kinds = Array.from(new Set(communities.map((c) => c.kind).filter((k): k is string => !!k))).sort();
  const [query, setQuery] = useState("");
  const q = query.trim().toLowerCase();
  const visible = communities
    .filter((c) => !kindFilter || c.kind === kindFilter)
    .filter((c) => !q || c.name.toLowerCase().includes(q) || richTextToPlain(c.description ?? "").toLowerCase().includes(q))
    .sort((a, b) => standing(a) - standing(b) || a.name.localeCompare(b.name));
  const mine = visible.filter((c) => standing(c) < 3);
  const others = visible.filter((c) => standing(c) === 3);

  return (
    <div className="p-4 sm:p-6 lg:p-8 space-y-6 sm:space-y-8 max-w-[1400px] mx-auto">
      <PageHeader
        title="Groups"
        description="Find your people within the community. Request to join the ones relevant to you."
      />

      {communities.length > 6 && (
        <div className="relative max-w-sm">
          <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
          <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search groups" aria-label="Search groups" className="pl-9" />
        </div>
      )}

      {kinds.length > 1 && (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Filter by kind of group">
          {[null, ...kinds].map((k) => (
            <button
              key={k ?? "all"}
              type="button"
              aria-pressed={kindFilter === k}
              onClick={() => setKindFilter(k)}
              className={`rounded-full border px-3.5 py-1.5 text-[13px] font-medium transition-colors ${kindFilter === k ? "border-primary bg-primary text-primary-foreground" : "border-border text-muted-foreground hover:text-foreground"}`}
            >
              {k ?? "All"}
            </button>
          ))}
        </div>
      )}

      {isLoading ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : communities.length === 0 ? (
        <EmptyState icon={<Users size={40} />} title="Groups are smaller circles inside your community" description="A group can be a chapter, a committee or a team, each with its own news, events and discussions. None have been created yet. When it does, you can ask to join from this page." action={<NotifyMeButton />} />
      ) : (
        <div className="space-y-8">
          {visible.length === 0 && (
            <p className="text-[14px] text-muted-foreground">No group matches that. Try another word.</p>
          )}
          {mine.length > 0 && <GroupSection title="Your groups" groups={mine} onOpen={(id) => router.push(`/communities/${id}`)} onJoin={(id) => joinMut.mutate(id)} joiningId={joinMut.isPending ? joinMut.variables : undefined} />}
          {others.length > 0 && <GroupSection title={mine.length > 0 ? "More groups to join" : undefined} groups={others} onOpen={(id) => router.push(`/communities/${id}`)} onJoin={(id) => joinMut.mutate(id)} joiningId={joinMut.isPending ? joinMut.variables : undefined} />}
        </div>
      )}
    </div>
  );
}
