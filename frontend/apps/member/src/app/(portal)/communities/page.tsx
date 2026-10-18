"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { NotifyMeButton } from "@/components/member/notify-me-button";
import { useRouter } from "next/navigation";
import { Users, Clock, Crown } from "@alumni/ui";
import { toast } from "sonner";
import { Button } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { getCommunities, joinCommunity, type Community } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { PageHeader } from "@alumni/ui";

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

export default function CommunitiesPage() {
  const qc = useQueryClient();
  const router = useRouter();

  const { data: communities = [], isLoading } = useQuery({
    queryKey: ["m-communities"],
    queryFn: getCommunities,
  });

  const joinMut = useMutation({
    mutationFn: (id: string) => joinCommunity(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["m-communities"] }); toast.success("Join request sent, a community leader will review it"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  // Filter by what the administrators call each group (Chapter, Committee…) — only offered when there is more than one kind.
  const [kindFilter, setKindFilter] = useState<string | null>(null);
  const kinds = Array.from(new Set(communities.map((c) => c.kind).filter((k): k is string => !!k))).sort();
  const visible = kindFilter ? communities.filter((c) => c.kind === kindFilter) : communities;

  return (
    <div className="p-4 sm:p-6 lg:p-8 space-y-6 sm:space-y-8 max-w-[1400px] mx-auto">
      <PageHeader
        title="Groups"
        description="Find your people within the community. Request to join the ones relevant to you."
      />

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
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {visible.map((c) => (
            <div
              key={c.id}
              role="link"
              tabIndex={0}
              onClick={() => router.push(`/communities/${c.id}`)}
              onKeyDown={(e) => { if (e.key === "Enter") router.push(`/communities/${c.id}`); }}
              className="card overflow-hidden flex flex-col cursor-pointer transition-all hover:-translate-y-0.5 hover:shadow-[var(--card-shadow-hover)] group"
            >
              <div className="relative shrink-0 overflow-hidden" style={{ height: 120 }}>
                {c.coverImageUrl ? (
                  <img
                    src={c.coverImageUrl}
                    alt={c.name}
                    className="absolute inset-0 w-full h-full object-cover transition-transform duration-500 group-hover:scale-[1.03]"
                    loading="lazy"
                  />
                ) : (
                  <div
                    className="absolute inset-0 flex items-center justify-center"
                    style={{ background: "var(--secondary)" }}
                  >
                    <Users size={32} style={{ color: "var(--muted-foreground)", opacity: 0.2 }} />
                  </div>
                )}
              </div>
              <div className="p-5 flex flex-col gap-3 flex-1">
                <div className="min-w-0 space-y-2">
                  {c.kind && <p className="text-[11.5px] font-semibold uppercase tracking-wide text-muted-foreground">{c.kind}</p>}
                  <p className="font-[family-name:var(--font-display)] text-[18px] font-semibold leading-snug group-hover:text-primary transition-colors">
                    {c.name}
                  </p>
                  {c.description && <p className="text-[13px] text-muted-foreground line-clamp-2">{c.description}</p>}
                </div>
                <div className="flex items-center justify-between gap-3 pt-2 mt-auto border-t border-border">
                  <span className="flex items-center gap-1.5 text-[12.5px] text-muted-foreground pt-3">
                    <Users size={13} /> {c.memberCount} {c.memberCount === 1 ? "member" : "members"}
                  </span>
                  <div className="pt-3" onClick={(e) => e.stopPropagation()}>
                    <StatusAction community={c} onJoin={() => joinMut.mutate(c.id)} joining={joinMut.isPending && joinMut.variables === c.id} />
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
