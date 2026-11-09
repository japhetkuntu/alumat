"use client";

import { useQuery } from "@tanstack/react-query";
import { UserAvatar } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { getLeaders, getLeadershipOf, type LeadershipPost } from "@/lib/member-api";
import { IntroduceButton } from "@/components/member/introduce-button";

/**
 * The people who lead a year group or community, with a way to reach each one. Renders nothing at all when nobody has been
 * appointed, so a group without leaders carries no empty box.
 */
export function MeetYourLeaders({ scopeType, scopeId, noun }: { scopeType: LeadershipPost["scopeType"]; scopeId?: string; noun: string }) {
  const { user } = useAuth();
  const { data: leaders = [] } = useQuery({
    queryKey: ["leaders", scopeType, scopeId],
    queryFn: () => getLeaders(scopeType, scopeId!),
    enabled: !!scopeId && !!user,
    staleTime: 60_000,
  });
  if (leaders.length === 0) return null;

  return (
    <section aria-labelledby={`leaders-${scopeId}`} className="rounded-[18px] border border-border bg-background p-4 sm:p-5">
      <h2 id={`leaders-${scopeId}`} className="text-[15px] font-semibold">{leaders.length === 1 ? "Your leader" : "Meet your leaders"}</h2>
      <p className="mt-0.5 text-[13px] text-muted-foreground">{leaders.length === 1 ? `The person who looks after this ${noun}.` : `The people who look after this ${noun}.`}</p>
      <ul className="mt-4 grid gap-3 sm:grid-cols-2">
        {leaders.map((p) => (
          <li key={p.id} className="flex flex-col gap-3 rounded-[14px] bg-muted/40 p-3">
            <div className="flex items-center gap-3">
              <UserAvatar src={p.profilePictureUrl} name={p.memberName} size="lg" />
              <div className="min-w-0 flex-1">
                <p className="truncate text-[14.5px] font-semibold leading-tight">{p.memberName}</p>
                <p className="truncate text-[12.5px] text-muted-foreground">{p.title}</p>
              </div>
            </div>
            {user?.id !== p.memberId && (
              <IntroduceButton memberId={p.memberId} firstName={p.memberName.split(" ")[0] ?? p.memberName}
                person={{ name: p.memberName, photoUrl: p.profilePictureUrl, subtitle: p.title }} />
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}

/** The titles held by the people in view, keyed by member, for a small label under their name. One request for the whole list. */
export function useLeaderTitles(memberIds: string[]) {
  const ids = [...new Set(memberIds)].sort();
  const { data = [] } = useQuery({
    queryKey: ["leader-titles", ids.join(",")],
    queryFn: () => getLeadershipOf(ids.slice(0, 100)),
    enabled: ids.length > 0,
    staleTime: 60_000,
  });
  const byMember = new Map<string, string[]>();
  for (const p of data) byMember.set(p.memberId, [...(byMember.get(p.memberId) ?? []), p.title]);
  return byMember;
}
