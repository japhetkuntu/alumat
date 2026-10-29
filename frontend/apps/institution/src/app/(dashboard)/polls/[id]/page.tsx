"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  ArrowLeft, Badge, Button, Card, CardContent, CardSkeleton, ConfirmModal, LoadError, Progress, UserAvatar, Check, Download, cn,
} from "@alumni/ui";
import {
  archivePoll, deletePoll, getPoll, getPollResults, publishPoll, publishPollResults, unpublishPoll,
  type PollDetail, type PollResults,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { PollStateBadge, RESULTS_MODE_LABEL, audienceText, fmtWhen } from "@/components/institution/poll-ui";
import { useCohortPhrase } from "@/components/institution/institution-layout";

function csv(r: PollResults): string {
  const esc = (v: string | number) => `"${String(v).replace(/"/g, '""')}"`;
  const rows: (string | number)[][] = [["Position", "Aspirant", "Votes", "Share of votes"]];
  for (const p of r.positions) {
    if (p.isYesNo) {
      rows.push([p.title, `${p.options[0]?.name ?? ""} (Yes)`, p.yes, ""], [p.title, `${p.options[0]?.name ?? ""} (No)`, p.no, ""], [p.title, "Abstained", p.abstained, ""]);
    } else {
      for (const o of p.options) rows.push([p.title, o.name, o.votes, `${o.percent}%`]);
      rows.push([p.title, "Abstained", p.abstained, ""]);
    }
  }
  rows.push([], ["Turnout", `${r.voted} of ${r.eligible} eligible members voted`]);
  return rows.map((row) => row.map(esc).join(",")).join("\n");
}

function download(name: string, text: string) {
  const url = URL.createObjectURL(new Blob([text], { type: "text/csv;charset=utf-8" }));
  const a = Object.assign(document.createElement("a"), { href: url, download: name });
  document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);
}

function ResultsPanel({ poll, results, onPublish, publishing }: { poll: PollDetail; results: PollResults; onPublish: () => void; publishing: boolean }) {
  const pct = results.eligible > 0 ? Math.round((results.voted / results.eligible) * 100) : 0;
  return (
    <section className="space-y-4" aria-labelledby="results-heading">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <h2 id="results-heading" className="text-[17px] font-bold">Turnout and results</h2>
        {results.figuresAvailable && (
          <Button variant="outline" size="sm" onClick={() => download(`${poll.title.replace(/[^a-z0-9]+/gi, "-").toLowerCase()}-results.csv`, csv(results))}><Download size={13} className="mr-1.5" /> Export CSV</Button>
        )}
      </div>

      <Card><CardContent className="space-y-2 p-4 sm:p-5">
        <div className="flex items-baseline justify-between"><p className="text-[22px] font-bold tabular-nums">{results.voted} <span className="text-[14px] font-medium text-muted-foreground">of {results.eligible} members voted</span></p><span className="text-[14px] font-semibold tabular-nums">{pct}%</span></div>
        <Progress value={pct} tone="accent" aria-label={`${pct}% turnout`} />
      </CardContent></Card>

      {!results.figuresAvailable ? (
        <p className="border border-border bg-muted/40 px-4 py-3 text-[13.5px] text-muted-foreground">
          The figures are kept back while voting is open, so nobody, including you, can watch the vote as it happens. They appear here once it closes.
        </p>
      ) : (
        <>
          {results.resultsMode === "Manual" && (
            <div className={cn("flex flex-wrap items-center justify-between gap-3 border px-4 py-3", results.shownToMembers ? "border-primary/30 bg-primary/5" : "border-border")}>
              <p className="text-[13.5px]">
                {results.shownToMembers ? `Published to members on ${fmtWhen(results.resultsPublishedAt!)}.` : "Members cannot see these yet. Check them, then publish when you are ready."}
              </p>
              {results.canPublish && <Button onClick={onPublish} isLoading={publishing}>Publish results to members</Button>}
            </div>
          )}
          {results.resultsMode !== "Manual" && <p className="text-[13px] text-muted-foreground">{results.shownToMembers ? "Members can see these results." : "Members will see these results once voting closes."}</p>}

          {results.positions.map((p) => (
            <Card key={p.positionId}><CardContent className="space-y-3 p-4 sm:p-5">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h3 className="text-[15px] font-semibold">{p.title}</h3>
                <span className="text-[12.5px] text-muted-foreground tabular-nums">{p.totalVotes} {p.totalVotes === 1 ? "vote" : "votes"}{p.abstained > 0 && `, ${p.abstained} abstained`}</span>
              </div>
              {p.isYesNo ? (
                <div className="space-y-2">
                  <p className="text-[13.5px] text-muted-foreground">Vote on {p.options[0]?.name}</p>
                  {[["Yes", p.yes], ["No", p.no]].map(([label, n]) => {
                    const decided = p.yes + p.no;
                    const share = decided ? Math.round(((n as number) / decided) * 100) : 0;
                    return <Bar key={label as string} label={label as string} votes={n as number} percent={share} lead={(label === "Yes" ? p.yes > p.no : p.no > p.yes)} />;
                  })}
                  <p className="text-[13px] font-semibold">{p.tied ? "Tied. Neither side leads." : p.passed === null || p.passed === undefined ? "No votes yet." : p.passed ? "Passed: more Yes than No." : "Did not pass: No leads."}</p>
                </div>
              ) : (
                <div className="space-y-2.5">
                  {p.options.map((o) => <Bar key={o.aspirantId} label={o.name} photo={o.photoUrl} votes={o.votes} percent={o.percent} lead={o.leads} />)}
                  {p.tied && <p className="text-[13px] font-semibold">Tied at the top. Nobody is declared the winner.</p>}
                </div>
              )}
            </CardContent></Card>
          ))}
        </>
      )}
    </section>
  );
}

function Bar({ label, photo, votes, percent, lead }: { label: string; photo?: string | null; votes: number; percent: number; lead: boolean }) {
  return (
    <div className="flex items-center gap-3">
      {photo !== undefined && <UserAvatar name={label} src={photo || undefined} size="sm" />}
      <div className="min-w-0 flex-1">
        <div className="mb-1 flex items-baseline justify-between gap-3 text-[13.5px]">
          <span className={cn("truncate", lead ? "font-semibold" : "text-muted-foreground")}>{label}{lead && <Check size={12} className="ml-1.5 inline text-primary" />}</span>
          <span className="shrink-0 tabular-nums text-muted-foreground">{votes} · {percent}%</span>
        </div>
        <Progress value={percent} tone={lead ? "primary" : "accent"} className="h-1.5" aria-label={`${label}: ${percent}%`} />
      </div>
    </div>
  );
}

export default function PollOverviewPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const qc = useQueryClient();
  const { word } = useCohortPhrase();
  const [confirm, setConfirm] = useState<null | "publish" | "unpublish" | "delete" | "results">(null);

  const poll = useQuery({ queryKey: ["poll", id], queryFn: () => getPoll(id), refetchInterval: (q) => (q.state.data?.state === "Open" ? 20_000 : false) });
  const results = useQuery({ queryKey: ["poll-results", id], queryFn: () => getPollResults(id), enabled: poll.data?.status === "Published", refetchInterval: poll.data?.state === "Open" ? 20_000 : false });

  const refresh = () => { qc.invalidateQueries({ queryKey: ["poll", id] }); qc.invalidateQueries({ queryKey: ["poll-results", id] }); qc.invalidateQueries({ queryKey: ["polls"] }); };
  const fail = (e: unknown) => { setConfirm(null); toast.error(handleApiError(e)); };
  const publish = useMutation({ mutationFn: () => publishPoll(id), onSuccess: () => { refresh(); setConfirm(null); toast.success("Published. Members in the audience have been told."); }, onError: fail });
  const unpublish = useMutation({ mutationFn: () => unpublishPoll(id), onSuccess: () => { refresh(); setConfirm(null); toast.success("Back to a draft."); }, onError: fail });
  const remove = useMutation({ mutationFn: () => deletePoll(id), onSuccess: () => { qc.invalidateQueries({ queryKey: ["polls"] }); toast.success("Poll deleted."); router.push("/polls"); }, onError: fail });
  const archive = useMutation({ mutationFn: (a: boolean) => archivePoll(id, a), onSuccess: (_d, a) => { refresh(); toast.success(a ? "Archived." : "Restored."); }, onError: fail });
  const publishResults = useMutation({ mutationFn: () => publishPollResults(id), onSuccess: () => { refresh(); setConfirm(null); toast.success("Results published to members."); }, onError: fail });

  if (poll.isLoading) return <div className="mx-auto max-w-[900px] space-y-4 p-4 sm:p-[26px]"><CardSkeleton /><CardSkeleton /></div>;
  if (poll.isError || !poll.data) return <div className="mx-auto max-w-[900px] p-4 sm:p-[26px]"><LoadError onRetry={() => poll.refetch()} /></div>;
  const p = poll.data;
  const pct = p.eligible > 0 ? Math.round((p.voted / p.eligible) * 100) : 0;

  return (
    <div className="mx-auto max-w-[900px] space-y-6 p-4 sm:p-[26px]">
      <Link href="/polls" className="inline-flex items-center gap-1.5 text-[13.5px] font-semibold text-muted-foreground hover:underline"><ArrowLeft size={15} /> All polls</Link>

      <header className="space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          <PollStateBadge state={p.state} />
          {p.isArchived && <Badge variant="neutral">Archived</Badge>}
        </div>
        <h1 className="m-0 text-[22px] font-bold leading-tight sm:text-[28px]">{p.title}</h1>
        {p.description && <p className="max-w-[70ch] whitespace-pre-line text-[14.5px] leading-relaxed text-muted-foreground">{p.description}</p>}
        <div className="flex flex-wrap gap-2 pt-1">
          {p.status === "Draft" && <Button onClick={() => setConfirm("publish")}>Publish to members</Button>}
          {p.state !== "Closed" && <Link href={`/polls/${p.id}/edit`}><Button variant="outline">{p.locked ? "Edit wording and closing time" : "Edit"}</Button></Link>}
          {p.status === "Published" && !p.locked && p.state !== "Closed" && <Button variant="outline" onClick={() => setConfirm("unpublish")}>Back to draft</Button>}
          {p.state === "Closed" && !p.isArchived && <Button variant="outline" onClick={() => archive.mutate(true)} isLoading={archive.isPending}>Archive</Button>}
          {p.isArchived && <Button variant="outline" onClick={() => archive.mutate(false)} isLoading={archive.isPending}>Restore</Button>}
          {p.canDelete && <Button variant="ghost" className="text-destructive hover:text-destructive" onClick={() => setConfirm("delete")}>Delete</Button>}
        </div>
      </header>

      <Card><CardContent className="grid gap-5 p-4 sm:grid-cols-3 sm:p-5">
        <Fact label="Voting opens" value={fmtWhen(p.opensAt)} />
        <Fact label="Voting closes" value={fmtWhen(p.closesAt)} />
        <Fact label="Who can vote" value={audienceText(p, word)} sub={`${p.eligible} eligible ${p.eligible === 1 ? "member" : "members"}`} />
        <Fact label="Results" value={RESULTS_MODE_LABEL[p.resultsMode]?.title ?? p.resultsMode} sub={p.status === "Draft" ? undefined : `${p.voted} of ${p.eligible} voted (${pct}%)`} />
      </CardContent></Card>

      {p.status === "Published" && results.data && (
        <ResultsPanel poll={p} results={results.data} onPublish={() => setConfirm("results")} publishing={publishResults.isPending} />
      )}

      <section className="space-y-3" aria-labelledby="ballot-heading">
        <h2 id="ballot-heading" className="text-[17px] font-bold">The ballot</h2>
        {p.positions.length === 0 ? <p className="text-[13.5px] text-muted-foreground">No positions yet. Edit the poll to add them.</p> : p.positions.map((pos) => (
          <Card key={pos.id}><CardContent className="space-y-3 p-4 sm:p-5">
            <div>
              <h3 className="text-[15px] font-semibold">{pos.title}</h3>
              {pos.description && <p className="text-[13px] text-muted-foreground">{pos.description}</p>}
              <p className="mt-1 text-[12px] font-semibold uppercase tracking-wide text-muted-foreground">{pos.isYesNo ? "Yes / No / Abstain" : `Choose one of ${pos.aspirants.length}`}</p>
            </div>
            <ul className="grid gap-3 sm:grid-cols-2">
              {pos.aspirants.map((a) => (
                <li key={a.id} className="flex items-start gap-3 border border-border p-3">
                  <UserAvatar name={a.name} src={a.photoUrl || undefined} size="lg" />
                  <div className="min-w-0"><p className="text-[14px] font-semibold">{a.name}</p>{a.details && <p className="mt-0.5 line-clamp-3 text-[13px] text-muted-foreground">{a.details}</p>}</div>
                </li>
              ))}
            </ul>
          </CardContent></Card>
        ))}
      </section>

      <ConfirmModal open={confirm === "publish"} title="Publish this poll?" message={`Members in the audience will be told. It opens ${fmtWhen(p.opensAt)} and closes ${fmtWhen(p.closesAt)}. Once anyone votes, the ballot and audience are locked.`} confirmLabel="Publish" isLoading={publish.isPending} onConfirm={() => publish.mutate()} onCancel={() => setConfirm(null)} />
      <ConfirmModal open={confirm === "unpublish"} title="Take it back to a draft?" message="Members will no longer see it. This is only possible while nobody has voted." confirmLabel="Back to draft" isLoading={unpublish.isPending} onConfirm={() => unpublish.mutate()} onCancel={() => setConfirm(null)} />
      <ConfirmModal open={confirm === "delete"} title="Delete this poll?" message="It will be removed for good. Nobody has voted in it." confirmLabel="Delete" variant="destructive" isLoading={remove.isPending} onConfirm={() => remove.mutate()} onCancel={() => setConfirm(null)} />
      <ConfirmModal open={confirm === "results"} title="Publish the results?" message="Every eligible member will see the figures and be told. This cannot be undone." confirmLabel="Publish results" isLoading={publishResults.isPending} onConfirm={() => publishResults.mutate()} onCancel={() => setConfirm(null)} />
    </div>
  );
}

function Fact({ label, value, sub }: { label: string; value: string; sub?: string }) {
  return (
    <div>
      <p className="text-[11.5px] font-semibold uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="mt-1 text-[14.5px] font-semibold leading-snug">{value}</p>
      {sub && <p className="mt-0.5 text-[12.5px] text-muted-foreground">{sub}</p>}
    </div>
  );
}
