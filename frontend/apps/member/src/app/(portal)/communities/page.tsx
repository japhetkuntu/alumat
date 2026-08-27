"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { Users, Clock, Crown } from "lucide-react";
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
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["m-communities"] }); toast.success("Join request sent — a community leader will review it"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <div className="p-4 sm:p-6 lg:p-8 space-y-6 sm:space-y-8 max-w-[1400px] mx-auto">
      <PageHeader
        title="Communities"
        description="Find your people beyond the classroom — request to join the ones relevant to you."
      />

      {isLoading ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : communities.length === 0 ? (
        <EmptyState icon={<Users size={40} />} title="No communities yet" description="Your institution hasn't created any communities yet." />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {communities.map((c) => (
            <div
              key={c.id}
              role="link"
              tabIndex={0}
              onClick={() => router.push(`/communities/${c.id}`)}
              onKeyDown={(e) => { if (e.key === "Enter") router.push(`/communities/${c.id}`); }}
              className="card p-5 flex flex-col gap-3 cursor-pointer transition-all hover:-translate-y-0.5 hover:shadow-[var(--card-shadow-hover)] group"
            >
              <div className="min-w-0 space-y-2">
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
                  <StatusAction community={c} onJoin={() => joinMut.mutate(c.id)} joining={joinMut.isPending} />
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
