"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ArrowLeft, ArrowRight, Check, Clock, Lock, Vote } from "@alumni/ui";
import { Badge, Button, CardSkeleton, EmptyState, LoadError, Progress, ReadMore, cn } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";
import {
  castBallot, getMemberPoll, getMemberPollResults,
  type BallotChoiceBody, type MemberPollAspirant, type MemberPollDetail, type MemberPollPosition, type MemberPollResults,
} from "@/lib/member-api";
import { fmtWhen, timeLeft } from "@/lib/poll-format";

type Answer = { aspirantId?: string; choice: BallotChoiceBody["choice"] };
type Answers = Record<string, Answer>;

const initials = (name: string) => name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join("");

/** An aspirant's face: their photo, or their initials on a quiet tile when they have none. */
function Face({ a, size }: { a: { name: string; photoUrl?: string | null }; size: number }) {
  return a.photoUrl ? (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={a.photoUrl} alt="" width={size} height={size} loading="lazy" decoding="async" className="shrink-0 rounded-full object-cover" style={{ width: size, height: size }} />
  ) : (
    <span aria-hidden className="flex shrink-0 items-center justify-center rounded-full bg-secondary font-[family-name:var(--font-display)] font-semibold text-muted-foreground" style={{ width: size, height: size, fontSize: size / 3 }}>{initials(a.name)}</span>
  );
}

/* ── The ballot ─────────────────────────────────────────────────────────── */

function AspirantChoice({ a, selected, onSelect }: { a: MemberPollAspirant; selected: boolean; onSelect: () => void }) {
  return (
    <div className={cn("card relative flex gap-4 p-4 transition-colors sm:p-5", selected ? "border-primary ring-2 ring-primary/25" : "hover:border-primary/40")}>
      <Face a={a} size={84} />
      <div className="min-w-0 flex-1 space-y-1.5">
        <p className="font-[family-name:var(--font-display)] text-[18px] font-semibold leading-snug">{a.name}</p>
        {a.details && <ReadMore lines={3} className="text-[13.5px] leading-relaxed text-muted-foreground">{a.details}</ReadMore>}
        <button
          type="button" role="radio" aria-checked={selected} onClick={onSelect}
          className={cn("mt-1 inline-flex h-10 items-center gap-2 border px-4 text-[13.5px] font-semibold transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-ring",
            selected ? "border-primary bg-primary text-primary-foreground" : "border-border hover:border-primary hover:text-primary")}
        >
          {selected && <Check size={13} />} {selected ? "Selected" : `Choose ${a.name.split(" ")[0]}`}
        </button>
      </div>
    </div>
  );
}

function PositionStep({ pos, answer, onAnswer }: { pos: MemberPollPosition; answer?: Answer; onAnswer: (a: Answer) => void }) {
  const abstained = answer?.choice === "Abstain";
  if (pos.isYesNo) {
    const a = pos.aspirants[0]!;
    return (
      <div className="space-y-4">
        <div className="card flex gap-4 p-4 sm:p-5">
          <Face a={a} size={84} />
          <div className="min-w-0 space-y-1.5">
            <p className="text-[12px] font-semibold uppercase tracking-wide text-muted-foreground">The only aspirant</p>
            <p className="font-[family-name:var(--font-display)] text-[18px] font-semibold leading-snug">{a.name}</p>
            {a.details && <ReadMore lines={3} className="text-[13.5px] leading-relaxed text-muted-foreground">{a.details}</ReadMore>}
          </div>
        </div>
        <p className="text-[14.5px] font-medium">Should {a.name} be {pos.title.toLowerCase().startsWith("the ") ? "" : "your "}{pos.title}?</p>
        <div className="grid grid-cols-3 gap-2.5" role="radiogroup" aria-label={`Your vote on ${pos.title}`}>
          {([["Yes", "Yes"], ["No", "No"], ["Abstain", "Abstain"]] as const).map(([choice, label]) => {
            const on = answer?.choice === choice;
            return (
              <button key={choice} type="button" role="radio" aria-checked={on} onClick={() => onAnswer({ aspirantId: a.id, choice })}
                className={cn("flex h-16 flex-col items-center justify-center gap-1 border text-[15px] font-semibold transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                  on ? (choice === "Abstain" ? "border-foreground/60 bg-muted" : "border-primary bg-primary text-primary-foreground") : "border-border hover:border-primary/60")}>
                {on && <Check size={13} />}{label}
              </button>
            );
          })}
        </div>
        <p className="text-[12.5px] text-muted-foreground">Abstain means you are choosing not to vote on this one. It still counts as an answer.</p>
      </div>
    );
  }
  return (
    <div className="space-y-3">
      <div role="radiogroup" aria-label={`Your choice for ${pos.title}`} className="space-y-3">
        {pos.aspirants.map((a) => (
          <AspirantChoice key={a.id} a={a} selected={answer?.choice === "Candidate" && answer.aspirantId === a.id} onSelect={() => onAnswer({ aspirantId: a.id, choice: "Candidate" })} />
        ))}
      </div>
      <button type="button" role="radio" aria-checked={abstained} onClick={() => onAnswer({ choice: "Abstain" })}
        className={cn("flex w-full items-center justify-between border border-dashed px-4 py-3 text-left text-[13.5px] transition-colors", abstained ? "border-foreground/60 bg-muted font-semibold" : "border-border text-muted-foreground hover:border-primary/60")}>
        <span>Abstain: I do not want to vote for this position</span>{abstained && <Check size={14} />}
      </button>
    </div>
  );
}

function describe(pos: MemberPollPosition, ans?: Answer): { text: string; face?: MemberPollAspirant } {
  if (!ans) return { text: "Not answered" };
  if (ans.choice === "Abstain") return { text: "Abstain" };
  const a = pos.aspirants.find((x) => x.id === ans.aspirantId);
  if (ans.choice === "Yes" || ans.choice === "No") return { text: `${ans.choice} for ${a?.name ?? ""}`.trim(), face: a };
  return { text: a?.name ?? "", face: a };
}

function Ballot({ poll, userId, onDone }: { poll: MemberPollDetail; userId: string; onDone: () => void }) {
  const total = poll.positions.length;
  const key = `poll-draft:${poll.id}:${userId}`;
  // 0 is the welcome, 1..total the positions, total+1 the review.
  const [step, setStep] = useState(0);
  const [answers, setAnswers] = useState<Answers>(() => { try { return JSON.parse(sessionStorage.getItem(key) ?? "{}"); } catch { return {}; } });
  const [sure, setSure] = useState(false);
  useEffect(() => { try { sessionStorage.setItem(key, JSON.stringify(answers)); } catch { /* the choices simply are not kept across a refresh */ } }, [answers, key]);
  useEffect(() => { window.scrollTo({ top: 0, behavior: "smooth" }); }, [step]);
  // While the member reads one position, fetch the next one's photos, so turning the page shows faces and not empty circles.
  useEffect(() => {
    const next = poll.positions[step]; // step is 1-based on positions, so index `step` is the following one
    next?.aspirants.forEach((a) => { if (a.photoUrl) { const img = new Image(); img.decoding = "async"; img.src = a.photoUrl; } });
  }, [step, poll.positions]);

  const submit = useMutation({
    mutationFn: () => castBallot(poll.id, poll.positions.map((p) => ({ positionId: p.id, aspirantId: answers[p.id]?.aspirantId ?? null, choice: answers[p.id]!.choice }))),
    onSuccess: () => { try { sessionStorage.removeItem(key); } catch { /* nothing to clear */ } onDone(); },
    onError: (e) => {
      toast.error(handleApiError(e));
      const status = (e as { response?: { status?: number } })?.response?.status;
      // A refusal (the ballot changed, the poll closed, you already voted) means the answers no longer match the poll: start again from what it
      // is now. A dropped connection or a server hiccup says nothing about the ballot, so the member keeps their answers and just presses submit again.
      if (status && status < 500) {
        setAnswers({}); setSure(false); setStep(0);
        try { sessionStorage.removeItem(key); } catch { /* nothing to clear */ }
        onDone();
      }
    },
  });

  const answered = poll.positions.filter((p) => answers[p.id]).length;
  const review = step === total + 1;
  const pos = step >= 1 && step <= total ? poll.positions[step - 1] : null;

  return (
    <div className="space-y-6">
      {step > 0 && (
        <div aria-label={`Step ${Math.min(step, total)} of ${total}`} className="space-y-2">
          <div className="flex gap-1.5" aria-hidden>
            {poll.positions.map((p, i) => (
              <span key={p.id} className={cn("h-1.5 flex-1 transition-colors", step === i + 1 ? "bg-primary" : answers[p.id] ? "bg-primary/45" : "bg-border")} />
            ))}
            <span className={cn("h-1.5 flex-1", review ? "bg-primary" : "bg-border")} />
          </div>
          <p className="text-[12.5px] font-semibold text-muted-foreground">{review ? "Check your ballot" : `Position ${step} of ${total}`}</p>
        </div>
      )}

      {step === 0 && (
        <div className="space-y-5">
          <div className="space-y-3">
            <h2 className="font-[family-name:var(--font-display)] text-[22px] font-semibold leading-tight">Before you vote</h2>
            <ul className="space-y-2.5 text-[14.5px] leading-relaxed">
              <li className="flex gap-3"><Lock size={16} className="mt-1 shrink-0 text-primary" /><span><strong>Your vote is secret.</strong> It is stored with no link to your name. Nobody can see what you chose, not even the administrators.</span></li>
              <li className="flex gap-3"><Check size={16} className="mt-1 shrink-0 text-primary" /><span><strong>You vote once, and it is final.</strong> You will see everything again before you submit.</span></li>
              <li className="flex gap-3"><Clock size={16} className="mt-1 shrink-0 text-primary" /><span>{total} {total === 1 ? "position" : "positions"}, about {Math.max(1, Math.round(total * 0.5))} {Math.max(1, Math.round(total * 0.5)) === 1 ? "minute" : "minutes"} in all.</span></li>
            </ul>
          </div>
          <Button size="lg" className="w-full sm:w-auto" onClick={() => setStep(1)}>Start voting <ArrowRight size={15} className="ml-2" /></Button>
        </div>
      )}

      {pos && (
        <div className="space-y-5">
          <div>
            <h2 className="font-[family-name:var(--font-display)] text-[24px] font-semibold leading-tight">{pos.title}</h2>
            {pos.description && <p className="mt-1 text-[14px] text-muted-foreground">{pos.description}</p>}
          </div>
          <PositionStep pos={pos} answer={answers[pos.id]} onAnswer={(a) => setAnswers((cur) => ({ ...cur, [pos.id]: a }))} />
          <div className="flex items-center justify-between gap-3 pt-2">
            <Button variant="outline" onClick={() => setStep(step - 1)}><ArrowLeft size={14} className="mr-2" /> Back</Button>
            <Button disabled={!answers[pos.id]} onClick={() => setStep(step + 1)}>{step === total ? "Review my ballot" : "Next"} <ArrowRight size={14} className="ml-2" /></Button>
          </div>
        </div>
      )}

      {review && (
        <div className="space-y-5">
          <div>
            <h2 className="font-[family-name:var(--font-display)] text-[24px] font-semibold leading-tight">Is this what you meant?</h2>
            <p className="mt-1 text-[14px] text-muted-foreground">Look over each answer. Tap Change to fix one. After you submit, it cannot be undone.</p>
          </div>
          <ul className="divide-y divide-border border border-border">
            {poll.positions.map((p, i) => {
              const d = describe(p, answers[p.id]);
              return (
                <li key={p.id} className="flex items-center gap-3 p-4">
                  <div className="min-w-0 flex-1">
                    <p className="text-[12px] font-semibold uppercase tracking-wide text-muted-foreground">{p.title}</p>
                    <div className="mt-1 flex items-center gap-2.5">{d.face && <Face a={d.face} size={32} />}<p className={cn("text-[15px] font-semibold", !answers[p.id] && "text-destructive")}>{d.text}</p></div>
                  </div>
                  <button type="button" onClick={() => setStep(i + 1)} className="shrink-0 text-[13px] font-semibold text-primary underline underline-offset-4">Change</button>
                </li>
              );
            })}
          </ul>
          <label className="flex cursor-pointer items-start gap-3 text-[14px] leading-snug">
            <input type="checkbox" className="mt-0.5 size-5 shrink-0 accent-[var(--primary)]" checked={sure} onChange={(e) => setSure(e.target.checked)} />
            <span>I have checked my answers. I understand that my vote is final and cannot be changed.</span>
          </label>
          <div className="flex items-center justify-between gap-3">
            <Button variant="outline" onClick={() => setStep(total)}><ArrowLeft size={14} className="mr-2" /> Back</Button>
            <Button size="lg" disabled={!sure || answered < total} isLoading={submit.isPending} loadingText="Submitting" onClick={() => submit.mutate()}>Submit my vote</Button>
          </div>
          {answered < total && <p className="text-[12.5px] text-destructive">Some positions have no answer yet. Tap Change on them.</p>}
        </div>
      )}
    </div>
  );
}

/* ── After voting, results, previews ────────────────────────────────────── */

function Receipt({ poll }: { poll: MemberPollDetail }) {
  return (
    <div className="space-y-4 border border-primary/30 bg-primary/5 p-5 sm:p-6">
      <div className="flex items-center gap-3">
        <span className="flex size-10 items-center justify-center rounded-full bg-primary text-primary-foreground"><Check size={18} /></span>
        <div>
          <p className="font-[family-name:var(--font-display)] text-[20px] font-semibold leading-tight">Your vote is in</p>
          {poll.votedAt && <p className="text-[13px] text-muted-foreground">Recorded {fmtWhen(poll.votedAt)}</p>}
        </div>
      </div>
      <p className="text-[14px] leading-relaxed text-muted-foreground">Thank you for taking part. What you chose is not stored against your name, so nobody can look it up, and it cannot be changed.</p>
    </div>
  );
}

function Results({ results }: { results: MemberPollResults }) {
  const pct = results.eligible > 0 ? Math.round((results.voted / results.eligible) * 100) : 0;
  return (
    <section className="space-y-4" aria-labelledby="results-h">
      <h2 id="results-h" className="font-[family-name:var(--font-display)] text-[22px] font-semibold">{results.state === "Closed" ? "Results" : "Results so far"}</h2>
      <div className="card space-y-2 p-4 sm:p-5">
        <p className="text-[15px] font-semibold tabular-nums">{results.voted} of {results.eligible} members voted <span className="font-normal text-muted-foreground">({pct}%)</span></p>
        <Progress value={pct} tone="accent" aria-label={`${pct}% turnout`} />
      </div>
      {results.positions.map((p) => (
        <div key={p.positionId} className="card space-y-3 p-4 sm:p-5">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h3 className="text-[16px] font-semibold">{p.title}</h3>
            <span className="text-[12.5px] text-muted-foreground tabular-nums">{p.totalVotes} {p.totalVotes === 1 ? "vote" : "votes"}{p.abstained > 0 && `, ${p.abstained} abstained`}</span>
          </div>
          {p.isYesNo ? (
            <>
              <p className="text-[13.5px] text-muted-foreground">Vote on {p.options[0]?.name}</p>
              {(["Yes", "No"] as const).map((l) => {
                const n = l === "Yes" ? p.yes : p.no; const decided = p.yes + p.no;
                return <Bar key={l} label={l} votes={n} percent={decided ? Math.round((n / decided) * 100) : 0} lead={l === "Yes" ? p.yes > p.no : p.no > p.yes} />;
              })}
              <p className="text-[13.5px] font-semibold">{p.tied ? "Tied. Neither side leads." : p.passed == null ? "No votes yet." : p.passed ? "Passed: more Yes than No." : "Did not pass: more No than Yes."}</p>
            </>
          ) : (
            <>
              {p.options.map((o) => <Bar key={o.aspirantId} label={o.name} face={{ name: o.name, photoUrl: o.photoUrl }} votes={o.votes} percent={o.percent} lead={o.leads} />)}
              {p.tied && <p className="text-[13.5px] font-semibold">Tied at the top. Nobody is declared the winner.</p>}
            </>
          )}
        </div>
      ))}
    </section>
  );
}

function Bar({ label, face, votes, percent, lead }: { label: string; face?: { name: string; photoUrl?: string | null }; votes: number; percent: number; lead: boolean }) {
  return (
    <div className="flex items-center gap-3">
      {face && <Face a={face} size={36} />}
      <div className="min-w-0 flex-1">
        <div className="mb-1 flex items-baseline justify-between gap-3 text-[14px]">
          <span className={cn("truncate", lead ? "font-semibold" : "text-muted-foreground")}>{label}{lead && <Check size={12} className="ml-1.5 inline text-primary" />}</span>
          <span className="shrink-0 tabular-nums text-muted-foreground">{votes} · {percent}%</span>
        </div>
        <Progress value={percent} tone={lead ? "primary" : "accent"} className="h-2" aria-label={`${label}: ${percent}%`} />
      </div>
    </div>
  );
}

function Preview({ poll }: { poll: MemberPollDetail }) {
  return (
    <section className="space-y-4" aria-labelledby="meet-h">
      <h2 id="meet-h" className="font-[family-name:var(--font-display)] text-[22px] font-semibold">Meet the aspirants</h2>
      {poll.positions.map((p) => (
        <div key={p.id} className="space-y-2.5">
          <h3 className="text-[15px] font-semibold">{p.title}</h3>
          <ul className="grid gap-3 sm:grid-cols-2">
            {p.aspirants.map((a) => (
              <li key={a.id} className="card flex gap-3 p-4"><Face a={a} size={56} /><div className="min-w-0"><p className="text-[15px] font-semibold">{a.name}</p>{a.details && <ReadMore lines={3} className="text-[13px] text-muted-foreground">{a.details}</ReadMore>}</div></li>
            ))}
          </ul>
        </div>
      ))}
    </section>
  );
}

/* ── The page ───────────────────────────────────────────────────────────── */

export default function PollPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const qc = useQueryClient();
  const poll = useQuery({ queryKey: ["m-poll", id], queryFn: () => getMemberPoll(id), refetchInterval: 30_000, retry: false });
  const showResults = !!poll.data?.resultsVisible;
  const results = useQuery({ queryKey: ["m-poll-results", id], queryFn: () => getMemberPollResults(id), enabled: showResults, refetchInterval: poll.data?.state === "Open" ? 15_000 : false });
  const [justVoted, setJustVoted] = useState(false);
  const refresh = () => { setJustVoted(true); qc.invalidateQueries({ queryKey: ["m-poll", id] }); qc.invalidateQueries({ queryKey: ["m-polls"] }); qc.invalidateQueries({ queryKey: ["m-poll-results", id] }); };
  const p = poll.data;
  const body = useMemo(() => p, [p]);

  if (poll.isLoading) return <div className="mx-auto max-w-[760px] space-y-4 p-4 sm:p-6 lg:p-8"><CardSkeleton /><CardSkeleton /></div>;
  if (poll.isError || !body) {
    return <div className="mx-auto max-w-[760px] p-4 sm:p-6 lg:p-8"><EmptyState icon={<Vote size={40} />} title="We could not find that poll" description="It may have closed and been put away, or it is not open to you." action={<Link href="/polls"><Button variant="outline">All polls</Button></Link>} /></div>;
  }

  return (
    <div className="mx-auto max-w-[760px] space-y-6 p-4 pb-32 sm:p-6 sm:pb-16 lg:p-8">
      <Link href="/polls" className="inline-flex items-center gap-1.5 text-[13.5px] font-semibold text-muted-foreground hover:underline"><ArrowLeft size={15} /> All polls</Link>

      <header className="space-y-2">
        <div className="flex flex-wrap items-center gap-2">
          {body.state === "Open" && <Badge variant="success">Voting open</Badge>}
          {body.state === "Scheduled" && <Badge variant="info">Opens {fmtWhen(body.opensAt)}</Badge>}
          {body.state === "Closed" && <Badge variant="neutral">Voting closed</Badge>}
          <span className="text-[12.5px] text-muted-foreground">{body.audience}</span>
        </div>
        <h1 className="m-0 font-[family-name:var(--font-display)] text-[26px] font-bold leading-tight sm:text-[32px]">{body.title}</h1>
        {body.description && <p className="max-w-[62ch] whitespace-pre-line text-[14.5px] leading-relaxed text-muted-foreground">{body.description}</p>}
        {body.state === "Open" && <p className="flex items-center gap-1.5 text-[13px] text-muted-foreground"><Clock size={13} /> {timeLeft(body.closesAt)}</p>}
      </header>

      {body.hasVoted && <Receipt poll={body} />}

      {body.canVote && user?.id && <Ballot key={body.id} poll={body} userId={user.id} onDone={refresh} />}

      {body.state === "Scheduled" && <Preview poll={body} />}

      {body.state === "Open" && body.hasVoted && !showResults && (
        <p className="border border-border bg-muted/40 px-4 py-3 text-[13.5px] text-muted-foreground">The results will be shared once voting closes{justVoted ? ", and you will be told" : ""}.</p>
      )}
      {body.state === "Closed" && !showResults && (
        <div className="space-y-3">
          <p className="border border-border bg-muted/40 px-4 py-3 text-[13.5px] text-muted-foreground">Voting has closed. The results have not been announced yet, and you will be told when they are.</p>
          {!body.hasVoted && <p className="text-[13.5px] text-muted-foreground">You did not vote in this one.</p>}
        </div>
      )}

      {showResults && results.data && <Results results={results.data} />}
      {showResults && results.isError && <LoadError onRetry={() => results.refetch()} />}
    </div>
  );
}
