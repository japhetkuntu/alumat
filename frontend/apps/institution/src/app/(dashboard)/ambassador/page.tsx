"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import Link from "next/link";
import { EmptyState, Figure, InsightSection, LoadError } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { WrongWorkspace } from "@/components/institution/wrong-workspace";
import { getAmbassadorWorkspace, updateMyTask } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";

export default function AmbassadorPage() {
  const { user } = useAuth();
  const isAmbassador = user?.role === "ScopedAdmin";
  const qc = useQueryClient();
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["ambassador-workspace"], queryFn: () => getAmbassadorWorkspace(30), staleTime: 60_000, enabled: isAmbassador });
  const task = useMutation({
    mutationFn: (v: { id: string; action: "complete" | "snooze" }) => updateMyTask(v.id, v.action),
    onSuccess: (_, v) => { qc.invalidateQueries({ queryKey: ["ambassador-workspace"] }); toast.success(v.action === "complete" ? "Marked as done" : "Postponed for 3 days"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (user && !isAmbassador) return <WrongWorkspace title="This workspace is for year-group ambassadors" description="As a full administrator you see every year group and ambassador on the Community health page." href="/engagement" label="Open Community health" />;
  if (isError) return <div className="p-4 sm:p-[26px] max-w-[1000px] mx-auto"><LoadError title="Your year groups couldn’t load" onRetry={() => void refetch()} /></div>;

  const cohorts = data?.cohorts.cohorts ?? [];
  const total = cohorts.reduce((n, c) => n + c.activeMembers, 0);
  const joined = cohorts.reduce((n, c) => n + c.newMembers, 0);

  return (
    <div className="p-4 sm:p-[26px] max-w-[1000px] mx-auto space-y-4">
      <div>
        <h1 className="text-[20px] sm:text-[25px] font-bold m-0">My year groups</h1>
        <p className="text-muted-foreground text-[13px] mt-1.5 max-w-[60ch]">
          {data ? `You look after ${data.yearGroups.length === 0 ? "no year groups yet" : `the class of ${data.yearGroups.join(", ")}`}. Welcome people, nudge profiles to completion and keep your class connected.` : "Your year groups at a glance."}
        </p>
      </div>

      {data && data.yearGroups.length === 0 ? (
        <EmptyState title="No year groups assigned" description="An administrator can assign you to the classes you look after from the Institution Admins page." />
      ) : (
        <>
          <InsightSection question="Your year groups, last 30 days" loading={isLoading}
            answer={data && <><Figure>{total.toLocaleString()}</Figure> members, <Figure>{joined.toLocaleString()}</Figure> {joined === 1 ? "has" : "have"} joined in the last 30 days.</>}>
            {/* Phone: one compact card per class, so no column is hidden off the side. */}
            <ul className="space-y-3 sm:hidden">
              {cohorts.map((c) => (
                <li key={c.year} className="border border-border p-3">
                  <p className="text-[15px] font-bold tabular-nums">Class of {c.year}</p>
                  <dl className="mt-2 grid grid-cols-2 gap-x-4 gap-y-1.5 text-[13px]">
                    <div><dt className="text-muted-foreground">Members</dt><dd className="font-semibold tabular-nums">{c.activeMembers}</dd></div>
                    <div><dt className="text-muted-foreground">New this period</dt><dd className="font-semibold tabular-nums">{c.newMembers}</dd></div>
                    <div><dt className="text-muted-foreground">Waiting approval</dt><dd className="font-semibold tabular-nums">{c.pendingMembers}</dd></div>
                    <div><dt className="text-muted-foreground">Profile incomplete</dt><dd className="font-semibold tabular-nums">{c.incompleteProfiles}</dd></div>
                    <div><dt className="text-muted-foreground">Took part</dt><dd className="font-semibold tabular-nums">{c.activeMembers === 0 ? "n/a" : `${Math.round(100 * c.participants / c.activeMembers)}%`}</dd></div>
                  </dl>
                </li>
              ))}
            </ul>
            <div className="hidden overflow-x-auto sm:block">
              <table className="w-full min-w-[520px] text-[13.5px]">
                <thead><tr className="text-left text-[12px] text-muted-foreground">
                  <th className="py-2 pr-3 font-semibold">Class</th><th className="py-2 pr-3 text-right font-semibold">Members</th><th className="py-2 pr-3 text-right font-semibold">New</th>
                  <th className="py-2 pr-3 text-right font-semibold">Waiting approval</th><th className="py-2 pr-3 text-right font-semibold">Profile incomplete</th><th className="py-2 text-right font-semibold">Took part</th>
                </tr></thead>
                <tbody className="divide-y divide-border">
                  {cohorts.map((c) => (
                    <tr key={c.year}>
                      <td className="py-2.5 pr-3 font-semibold tabular-nums">{c.year}</td>
                      <td className="py-2.5 pr-3 text-right tabular-nums">{c.activeMembers}</td>
                      <td className="py-2.5 pr-3 text-right tabular-nums">{c.newMembers}</td>
                      <td className="py-2.5 pr-3 text-right tabular-nums">{c.pendingMembers}</td>
                      <td className="py-2.5 pr-3 text-right tabular-nums">{c.incompleteProfiles}</td>
                      <td className="py-2.5 text-right tabular-nums">{c.activeMembers === 0 ? "n/a" : `${Math.round(100 * c.participants / c.activeMembers)}%`}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </InsightSection>

          <InsightSection question="Tasks for you" loading={isLoading}
            answer={data && (data.tasks.length === 0 ? <>Nothing has been delegated to you.</> : <><Figure>{data.tasks.length}</Figure> {data.tasks.length === 1 ? "task" : "tasks"} from your administrator.</>)}>
            <ul className="divide-y divide-border">
              {data?.tasks.map((t) => (
                <li key={t.id} className="py-3 first:pt-0">
                  <p className="text-[15px] font-semibold">{t.title}</p>
                  <p className="mt-1 text-[13.5px] text-muted-foreground">{t.explanation}</p>
                  <div className="mt-1 flex flex-wrap gap-x-4 text-[13px]">
                    <Link href={t.actionUrl} className="inline-block py-2 font-semibold text-primary underline underline-offset-4">{t.actionLabel}</Link>
                    <button type="button" disabled={task.isPending} onClick={() => task.mutate({ id: t.id, action: "complete" })} className="py-2 text-[13px] font-semibold hover:underline underline-offset-4">Mark done</button>
                    <button type="button" disabled={task.isPending} onClick={() => task.mutate({ id: t.id, action: "snooze" })} className="py-2 text-[13px] text-muted-foreground hover:underline underline-offset-4">Later</button>
                  </div>
                </li>
              ))}
            </ul>
          </InsightSection>

          <InsightSection question="Newest members to welcome" loading={isLoading}
            answer={data && (data.recentlyJoined.length === 0 ? <>Nobody new in your year groups yet.</> : <>The {data.recentlyJoined.length} most recent. Names only; contact details stay private.</>)}>
            <ul className="divide-y divide-border">
              {data?.recentlyJoined.map((m, i) => (
                <li key={i} className="flex flex-wrap items-baseline justify-between gap-x-4 py-2.5 text-[14px]">
                  <span><span className="font-semibold">{m.name}</span> <span className="text-muted-foreground">class of {m.year}</span></span>
                  <span className="text-[13px] text-muted-foreground">
                    Joined {new Date(m.joinedAt).toLocaleDateString("en-GB", { day: "numeric", month: "short" })}{m.profileIncomplete ? " · profile not filled in" : ""}
                  </span>
                </li>
              ))}
            </ul>
          </InsightSection>
        </>
      )}
    </div>
  );
}
