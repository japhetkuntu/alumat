"use client";

import Link from "next/link";
import { Fragment, use, useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { format, formatDistanceToNowStrict, isSameDay } from "date-fns";
import {
  ArrowLeft, Ban, Check, Button, ConfirmModal, Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger,
  FormError, Info, Label, Lock, LogOut, MessageCircle, MoreHorizontal, Send, ShieldAlert, Textarea, Undo, UserAvatar, cn,
} from "@alumni/ui";
import {
  INTRO_MESSAGE_MAX, REPORT_REASONS, answerIntroduction, blockMember, endIntroduction, getIntroduction, reportIntroduction, sendIntroMessage,
  resumeIntroduction, unblockMember, withdrawIntroduction, type IntroCondition, type IntroDetail, type IntroMessage, type IntroPerson,
} from "@/lib/member-api";
import { useCohortPhrase, useNavTheme } from "@/components/member/member-layout";
import { IntroduceButton } from "@/components/member/introduce-button";
import { mergeLatest, mergeNewMessages, prependOlder, pollWhenOffline, useStreamConnected } from "@/lib/introduction-stream";
import { handleApiError } from "@/lib/api-client";

const GROUP_GAP_MS = 5 * 60_000;

/** Turns web addresses in a message into links that open safely in a new tab. Nothing else is interpreted. */
function Linked({ text }: { text: string }) {
  const parts = text.split(/(https?:\/\/[^\s<]+[^\s<.,;:!?)\]'"])/g);
  return <>{parts.map((p, i) => (i % 2 === 1
    ? <a key={i} href={p} target="_blank" rel="noopener noreferrer nofollow" className="underline underline-offset-2 [overflow-wrap:anywhere]">{p}</a>
    : <Fragment key={i}>{p}</Fragment>))}</>;
}

/** One row of the conversation menu: an icon tile, what it does in a few words, and what that means in one plain line. */
function MenuAction({ icon, title, text, onSelect, destructive }: { icon: React.ReactNode; title: string; text: string; onSelect: () => void; destructive?: boolean }) {
  return (
    <DropdownMenuItem destructive={destructive} onSelect={onSelect} className="items-start gap-3 rounded-[12px] px-2.5 py-2.5">
      <span className={cn("mt-px flex h-9 w-9 shrink-0 items-center justify-center rounded-[10px] [&>svg]:!h-4 [&>svg]:!w-4", destructive ? "bg-destructive/10 [&>svg]:!text-destructive" : "bg-muted [&>svg]:!text-foreground/70")}>{icon}</span>
      <span className="min-w-0">
        <span className="block text-[14px] font-semibold leading-tight">{title}</span>
        <span className="mt-1 block text-[12.5px] font-normal leading-snug text-muted-foreground">{text}</span>
      </span>
    </DropdownMenuItem>
  );
}

/** What takes the place of the message box when a conversation cannot take new messages: what happened, in plain words, and the way back where there is one. */
function ClosedNotice({ condition, endedByMe, other, firstName, onResume, resuming, onUnblock, unblocking }: {
  condition: IntroCondition; endedByMe: boolean; other: IntroPerson; firstName: string;
  onResume: () => void; resuming: boolean; onUnblock: () => void; unblocking: boolean;
}) {
  const tile = (icon: React.ReactNode, tone: "calm" | "warn" = "calm") => (
    <span className={cn("flex h-10 w-10 shrink-0 items-center justify-center rounded-[12px] [&>svg]:!h-[18px] [&>svg]:!w-[18px]", tone === "warn" ? "bg-destructive/10 [&>svg]:!text-destructive" : "bg-muted [&>svg]:!text-foreground/70")}>{icon}</span>
  );
  let body: React.ReactNode;
  if (condition === "ended" && endedByMe) body = (
    <>{tile(<LogOut />)}<div className="min-w-0 flex-1"><p className="text-[14.5px] font-semibold">You ended this conversation</p><p className="mt-0.5 text-[13px] text-muted-foreground">Your messages are kept. Pick it up again whenever you like.</p></div>
      <Button size="sm" onClick={onResume} isLoading={resuming} loadingText="Resuming…">Resume</Button></>
  );
  else if (condition === "ended") body = (
    <div className="w-full space-y-3">
      <div className="flex items-start gap-3">{tile(<LogOut />)}<div><p className="text-[14.5px] font-semibold">{firstName} ended this conversation</p><p className="mt-0.5 text-[13px] text-muted-foreground">You can still read it. If you&apos;d like to talk again, ask to reconnect and, if they accept, it reopens right here.</p></div></div>
      <IntroduceButton memberId={other.id} firstName={firstName} person={{ name: other.name, photoUrl: other.photoUrl, subtitle: other.headline }} />
    </div>
  );
  else if (condition === "blocked") body = (
    <>{tile(<Ban />, "warn")}<div className="min-w-0 flex-1"><p className="text-[14.5px] font-semibold">You blocked {firstName}</p><p className="mt-0.5 text-[13px] text-muted-foreground">You won&apos;t get messages from them or be able to send any. They haven&apos;t been told.</p></div>
      <Button size="sm" variant="outline" onClick={onUnblock} isLoading={unblocking} loadingText="Unblocking…">Unblock</Button></>
  );
  else body = (
    <>{tile(<Info />)}<div className="min-w-0 flex-1"><p className="text-[14.5px] font-semibold">You can&apos;t send messages here right now</p><p className="mt-0.5 text-[13px] text-muted-foreground">You can still read the conversation.</p></div></>
  );
  return (
    <div className="px-3 pb-3 pt-2 sm:px-4" style={{ paddingBottom: "max(0.75rem, env(safe-area-inset-bottom))" }}>
      <div className="flex flex-wrap items-center gap-3 rounded-[16px] border p-3.5 animate-in fade-in slide-in-from-bottom-1 duration-300" style={{ borderColor: "var(--border)", background: "color-mix(in oklch, var(--muted) 45%, transparent)" }}>{body}</div>
    </div>
  );
}

function ReportDialog({ id, open, onClose }: { id: string; open: boolean; onClose: () => void }) {
  const [reason, setReason] = useState("");
  const [details, setDetails] = useState("");
  const [error, setError] = useState<string | null>(null);
  const send = useMutation({
    mutationFn: () => reportIntroduction(id, reason, details.trim() || undefined),
    onSuccess: () => { toast.success("Thank you. Your report has been sent to the administrators."); setReason(""); setDetails(""); onClose(); },
    onError: (e) => setError(handleApiError(e)),
  });
  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) { setError(null); onClose(); } }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Report this conversation</DialogTitle>
          <DialogDescription>
            Your report, your name and the latest messages in this conversation go to the institution&apos;s administrators. They never read a conversation unless someone reports it.
          </DialogDescription>
        </DialogHeader>
        <fieldset className="mt-3 space-y-2">
          <legend className="mb-1 text-[13px] font-semibold">What is the problem?</legend>
          {REPORT_REASONS.map((r) => (
            <label key={r} className="flex cursor-pointer items-center gap-2.5 text-[14px]">
              <input type="radio" name="intro-report" checked={reason === r} onChange={() => setReason(r)} className="h-4 w-4 accent-primary" /> {r}
            </label>
          ))}
        </fieldset>
        <div className="mt-3 space-y-1.5">
          <Label htmlFor="intro-report-details">Anything else? <span className="font-normal text-muted-foreground">(optional)</span></Label>
          <Textarea id="intro-report-details" rows={3} maxLength={1000} value={details} onChange={(e) => setDetails(e.target.value)} className="resize-none text-[14px]" />
        </div>
        <FormError message={error} className="mt-2" />
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button disabled={!reason} isLoading={send.isPending} loadingText="Sending…" onClick={() => { setError(null); send.mutate(); }}>Send report</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

type Shown = IntroMessage & { sending?: boolean };

export default function ConversationPage({ params }: { params: Promise<{ id: string }> }) {
  const cohortPhrase = useCohortPhrase();
  const isCommunity = useNavTheme().data?.organizationType === "Community";
  const { id } = use(params);
  const router = useRouter();
  const qc = useQueryClient();
  const [text, setText] = useState("");
  const [sending, setSending] = useState<{ key: string; body: string; at: string; sid?: string }[]>([]);
  const [confirm, setConfirm] = useState<null | "end" | "block" | "withdraw">(null);
  const [reporting, setReporting] = useState(false);
  const [atBottom, setAtBottom] = useState(true);
  const [missed, setMissed] = useState(0);
  const scroller = useRef<HTMLDivElement>(null);
  const box = useRef<HTMLTextAreaElement>(null);
  const menuButton = useRef<HTMLButtonElement>(null);
  const dialogWasOpen = useRef(false);
  const seenCount = useRef(0);
  const first = useRef(true);
  const older = useRef<HTMLDivElement>(null);
  const anchor = useRef<{ height: number; top: number } | null>(null);
  const [loadingOlder, setLoadingOlder] = useState(false);

  const live = useStreamConnected();
  const { data, isLoading, isError } = useQuery<IntroDetail>({
    queryKey: ["intro", id],
    // A refresh brings in the newest page and keeps any older messages the reader has already scrolled back to.
    queryFn: async () => mergeLatest(qc.getQueryData<IntroDetail>(["intro", id]), await getIntroduction(id)),
    // The live connection pushes changes. Only if it is down does this ask every few seconds.
    refetchInterval: (q) => (q.state.data?.canSend ? pollWhenOffline(live, 5_000) : false),
    // A failed fetch (a dropped connection, a request that raced the server) is tried again before the page gives up; a real "not yours / not found" is not.
    retry: (n, e) => n < 3 && ![401, 403, 404].includes((e as { response?: { status?: number } })?.response?.status ?? 0),
    retryDelay: (n) => 400 * 2 ** n,
  });

  const refresh = useCallback(() => Promise.all([
    qc.invalidateQueries({ queryKey: ["intro", id] }), qc.invalidateQueries({ queryKey: ["intros"] }), qc.invalidateQueries({ queryKey: ["intro-summary"] }),
  ]), [qc, id]);

  const answer = useMutation({
    mutationFn: (accept: boolean) => answerIntroduction(id, accept),
    onSuccess: (res, accept) => {
      refresh();
      const name = data?.item.other.name.split(" ")[0] ?? "them";
      if (!accept) { toast.success("Declined. They won't be told."); router.push("/connections"); return; }
      // An earlier, ended conversation comes back instead of a new one starting: go to that one.
      const target = res?.data?.introductionId;
      toast.success(data?.item.reopensIntroductionId ? `Your conversation with ${name} is back.` : `You're now connected with ${name}.`);
      if (target && target !== id) router.replace(`/connections/${target}`);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const withdraw = useMutation({ mutationFn: () => withdrawIntroduction(id), onSuccess: () => { setConfirm(null); refresh(); toast.success("Request withdrawn."); router.push("/connections"); }, onError: (e) => toast.error(handleApiError(e)) });
  const end = useMutation({ mutationFn: () => endIntroduction(id), onSuccess: () => { setConfirm(null); refresh(); toast.success("Conversation ended."); }, onError: (e) => toast.error(handleApiError(e)) });
  const block = useMutation({
    mutationFn: () => blockMember(data!.item.other.id),
    // Stays in the conversation: it is now read-only, with an Unblock button right there.
    onSuccess: () => { setConfirm(null); refresh(); toast.success("Blocked. You can unblock any time from this conversation."); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const resume = useMutation({
    mutationFn: () => resumeIntroduction(id),
    onSuccess: () => { refresh(); toast.success("Conversation resumed."); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const unblock = useMutation({
    mutationFn: () => unblockMember(data!.item.other.id),
    onSuccess: () => { refresh(); toast.success("Unblocked. You can message each other again."); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  // When a confirmation or the report form closes, keyboard focus goes back to the menu button rather than being lost.
  useEffect(() => {
    const open = confirm !== null || reporting;
    if (dialogWasOpen.current && !open) window.setTimeout(() => menuButton.current?.focus(), 0);
    dialogWasOpen.current = open;
  }, [confirm, reporting]);

  // Opening a conversation marks it read on the server, so the menu badge is refreshed straight away.
  const opened = data?.item.id;
  useEffect(() => { if (opened) { qc.invalidateQueries({ queryKey: ["intro-summary"] }); qc.invalidateQueries({ queryKey: ["intros"] }); qc.invalidateQueries({ queryKey: ["notifications"] }); } }, [opened, qc]);

  const send = async (body: string) => {
    const key = `${Date.now()}-${Math.random()}`;
    setSending((s) => [...s, { key, body, at: new Date().toISOString() }]);
    setText("");
    if (box.current) box.current.style.height = "auto";
    try {
      const saved = await sendIntroMessage(id, body);
      // Put the saved message straight into the chat and let the grey "sending" copy step aside in the same moment, so it is never shown twice.
      qc.setQueryData<IntroDetail>(["intro", id], (old) => (old && !old.messages.some((m) => m.id === saved.id)
        ? { ...old, messages: [...old.messages, saved] } : old));
      setSending((s) => s.map((m) => (m.key === key ? { ...m, sid: saved.id } : m)));
      void mergeNewMessages(qc, id).catch(() => undefined);
      qc.invalidateQueries({ queryKey: ["intros"] }, { cancelRefetch: false });
      qc.invalidateQueries({ queryKey: ["intro-summary"] }, { cancelRefetch: false });
    } catch (e) {
      setText((t) => (t ? t : body));
      toast.error(handleApiError(e));
      setSending((s) => s.filter((m) => m.key !== key));
    }
  };

  // Once a message is really in the chat its grey copy is dropped (never before, so there is no moment where it is in neither place).
  const savedIds = data?.messages;
  useEffect(() => {
    if (!savedIds) return;
    const ids = new Set(savedIds.map((m) => m.id));
    setSending((s) => (s.some((m) => m.sid && ids.has(m.sid)) ? s.filter((m) => !(m.sid && ids.has(m.sid))) : s));
  }, [savedIds]);

  // A "sending" copy is shown only until its real message exists, whichever way that arrives (the reply to our own request, or the live stream).
  const real = data?.messages ?? [];
  const known = new Set(real.map((m) => m.id));
  const claimed = new Set<string>();
  const pending = sending.filter((s) => {
    if (s.sid && known.has(s.sid)) return false;
    const twin = real.find((m) => m.mine && m.body === s.body && !claimed.has(m.id) && new Date(m.createdAt).getTime() >= new Date(s.at).getTime() - 3000 && !sending.some((o) => o.sid === m.id));
    if (twin) { claimed.add(twin.id); return false; }
    return true;
  });
  const messages: Shown[] = [...real, ...pending.map((s) => ({ id: s.key, mine: true, body: s.body, createdAt: s.at, sending: true }))];
  const count = messages.length;
  const lastMine = count > 0 && messages[count - 1].mine;

  // Scrolling to the top fetches the page before the oldest message on screen, one page at a time, never the whole history.
  const hasOlder = !!data?.hasMoreBefore;
  const loadOlder = useCallback(async () => {
    const cur = qc.getQueryData<IntroDetail>(["intro", id]);
    const el = scroller.current;
    if (!cur || !cur.hasMoreBefore || !cur.messages.length || !el || anchor.current) return;
    anchor.current = { height: el.scrollHeight, top: el.scrollTop };
    setLoadingOlder(true);
    try {
      const page = await getIntroduction(id, undefined, cur.messages[0].createdAt);
      qc.setQueryData<IntroDetail>(["intro", id], (old) => (old ? prependOlder(old, page) : old));
    } catch { anchor.current = null; toast.error("Couldn't load earlier messages."); }
    finally { setLoadingOlder(false); }
  }, [qc, id]);

  useEffect(() => {
    const el = older.current;
    if (!el || !hasOlder || loadingOlder) return;
    const watch = new IntersectionObserver(([e]) => { if (e.isIntersecting) void loadOlder(); }, { root: scroller.current, rootMargin: "200px 0px 0px 0px" });
    watch.observe(el);
    return () => watch.disconnect();
  }, [hasOlder, loadingOlder, loadOlder, count]);

  const toBottom = useCallback((smooth: boolean) => {
    const el = scroller.current;
    if (el) el.scrollTo({ top: el.scrollHeight, behavior: smooth ? "smooth" : "auto" });
  }, []);

  // First paint lands at the latest message. After that, new messages follow along only if the reader is already at the bottom (or wrote them).
  useLayoutEffect(() => {
    if (count === seenCount.current) return;
    if (anchor.current && scroller.current) {
      // Older messages were added above: keep the reader looking at the same message instead of jumping to the top.
      const el = scroller.current;
      el.scrollTop = anchor.current.top + (el.scrollHeight - anchor.current.height);
      anchor.current = null;
      seenCount.current = count;
      return;
    }
    const added = count - seenCount.current;
    if (first.current && count > 0) { toBottom(false); first.current = false; }
    else if (atBottom || lastMine) toBottom(true);
    else setMissed((m) => m + added);
    seenCount.current = count;
  }, [count, atBottom, lastMine, toBottom]);

  if (isLoading) return <div className="flex-1 p-6"><div className="h-full min-h-48 animate-pulse rounded-[16px] bg-muted" /></div>;
  if (isError || !data) return (
    <div className="flex h-full flex-col items-center justify-center gap-4 p-6 text-center">
      <p className="text-[15px] font-semibold">This conversation isn&apos;t available.</p>
      <div className="flex gap-2">
        <Button onClick={() => void refresh()}>Try again</Button>
        <Link href="/connections"><Button variant="outline">Back to connections</Button></Link>
      </div>
    </div>
  );

  const { item, canSend, otherReadAt } = data;
  const other = item.other, firstName = other.name.split(" ")[0];
  const pendingForMe = item.status === "Pending" && !item.mine;
  const pendingMine = item.status === "Pending" && item.mine;
  const accepted = item.status === "Accepted" || item.status === "Ended";
  const condition: IntroCondition = item.condition ?? "open";
  const endedByMe = !!item.endedByMe;
  const starters = [`Hi ${firstName}, thanks for connecting!`, "Could we find 15 minutes for a call this week?", "I'd love your advice on something. Do you have a moment?"];

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="flex items-center gap-2.5 border-b bg-background/90 px-2 py-2.5 backdrop-blur sm:px-4" style={{ borderColor: "var(--border)", paddingTop: "max(0.625rem, env(safe-area-inset-top))" }}>
        <Link href="/connections" aria-label="Back to connections" className="rounded-[12px] p-2.5 hover:bg-muted lg:hidden"><ArrowLeft size={18} /></Link>
        <UserAvatar src={other.photoUrl} name={other.name} size="lg" />
        <div className="min-w-0 flex-1">
          <p className="truncate text-[15px] font-semibold leading-tight">{other.name}</p>
          <p className="truncate text-[12.5px] text-muted-foreground">{[other.headline, other.graduationYear && !isCommunity ? cohortPhrase.of(other.graduationYear) : null].filter(Boolean).join(" · ") || "Member"}</p>
        </div>
        <DropdownMenu modal={false}>
          <DropdownMenuTrigger asChild>
            <button ref={menuButton} type="button" aria-label={`Options for your conversation with ${firstName}`}
              className="grid h-10 w-10 shrink-0 place-items-center rounded-full text-muted-foreground outline-none transition-colors hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-primary data-[state=open]:bg-muted data-[state=open]:text-foreground">
              <MoreHorizontal size={18} />
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" sideOffset={6} className="w-[min(92vw,340px)] rounded-[16px] p-1.5">
            <div className="flex items-center gap-3 px-2.5 pb-2.5 pt-2">
              <UserAvatar src={other.photoUrl} name={other.name} size="default" />
              <div className="min-w-0">
                <p className="truncate text-[14px] font-semibold leading-tight">{other.name}</p>
                <p className="mt-0.5 truncate text-[12px] text-muted-foreground">{accepted ? "Connected" : "Asked"} {format(new Date(item.createdAt), "d MMMM yyyy")}</p>
              </div>
            </div>
            <DropdownMenuSeparator className="mx-1 my-1" />
            {pendingMine && <MenuAction icon={<Undo />} title="Withdraw request" text={`${firstName} won't see it. You can ask again later.`} onSelect={() => setConfirm("withdraw")} />}
            {condition === "ended" && endedByMe && <MenuAction icon={<MessageCircle />} title="Resume conversation" text="Pick up where you left off." onSelect={() => resume.mutate()} />}
            {item.status === "Accepted" && condition === "open" && <MenuAction icon={<LogOut />} title="End conversation" text="Stop messaging. You can both still read it, and you can resume it." onSelect={() => setConfirm("end")} />}
            {accepted && <MenuAction icon={<ShieldAlert />} title="Report" text="Send this conversation to the administrators." onSelect={() => setReporting(true)} />}
            <DropdownMenuSeparator className="mx-1 my-1" />
            {condition === "blocked"
              ? <MenuAction icon={<Undo />} title={`Unblock ${firstName}`} text="Messages can flow both ways again." onSelect={() => unblock.mutate()} />
              : <MenuAction destructive icon={<Ban />} title={`Block ${firstName}`} text="No messages either way, and they won't be told. You can undo it." onSelect={() => setConfirm("block")} />}
          </DropdownMenuContent>
        </DropdownMenu>
      </header>

      {pendingForMe && (
        <div className="flex flex-1 items-center justify-center overflow-y-auto p-5">
          <section className="w-full max-w-md space-y-5 rounded-[24px] border bg-background p-6 text-center shadow-sm animate-in fade-in zoom-in-95 duration-300" style={{ borderColor: "var(--border)" }}>
            <UserAvatar src={other.photoUrl} name={other.name} size="xl" className="mx-auto" />
            <div>
              <p className="text-[16px] font-semibold">{item.reopensIntroductionId ? `${other.name} would like to pick your conversation back up` : `${other.name} would like to be introduced to you`}</p>
              {other.headline && <p className="text-[13px] text-muted-foreground">{other.headline}</p>}
            </div>
            <blockquote className="whitespace-pre-wrap rounded-[16px] bg-muted/60 px-4 py-3.5 text-left text-[14.5px] leading-relaxed">
              <span className="mb-1 block text-[11.5px] font-semibold uppercase tracking-wide text-muted-foreground">Their note</span>“{item.reason}”
            </blockquote>
            <div className="flex flex-col gap-1.5">
              <Button className="h-12 w-full rounded-full text-[15px] font-semibold" isLoading={answer.isPending} loadingText="Accepting…" onClick={() => answer.mutate(true)}>{item.reopensIntroductionId ? "Accept and reopen" : "Accept and chat"}</Button>
              <button type="button" disabled={answer.isPending} onClick={() => answer.mutate(false)} className="w-full rounded-full py-2.5 text-[14px] font-semibold text-muted-foreground transition-colors hover:bg-muted hover:text-foreground disabled:opacity-50">Not now</button>
            </div>
            <p className="text-[12px] text-muted-foreground">{item.reopensIntroductionId ? "Accepting reopens your earlier chat with all its messages. They won't be told if you decline." : "Nothing is shared until you accept. They won't be told if you decline."}</p>
          </section>
        </div>
      )}

      {pendingMine && (
        <div className="flex flex-1 items-center justify-center p-5">
          <section className="w-full max-w-md space-y-3 rounded-[16px] border p-6 text-center" style={{ borderColor: "var(--border)" }}>
            <UserAvatar src={other.photoUrl} name={other.name} size="xl" className="mx-auto" />
            <div>
              <p className="text-[16px] font-semibold">Waiting for {firstName} to reply</p>
              <p className="mt-0.5 text-[12.5px] text-muted-foreground">Sent {Date.now() - new Date(item.createdAt).getTime() < 45_000 ? "just now" : `${formatDistanceToNowStrict(new Date(item.createdAt))} ago`}</p>
            </div>
            <blockquote className="whitespace-pre-wrap rounded-[12px] bg-muted/60 p-4 text-left text-[14px] leading-relaxed text-muted-foreground">Your note: “{item.reason}”</blockquote>
            <p className="text-[12.5px] text-muted-foreground">You&apos;ll be able to chat as soon as they accept, and we&apos;ll let you know.</p>
            <button type="button" className="text-[13px] font-semibold text-muted-foreground underline underline-offset-4 hover:text-foreground" onClick={() => setConfirm("withdraw")}>Withdraw request</button>
          </section>
        </div>
      )}

      {accepted && (
        <>
          <div ref={scroller} className="relative min-h-0 flex-1 overflow-y-auto px-3 sm:px-5" aria-live="polite"
            onScroll={(e) => { const el = e.currentTarget; const near = el.scrollHeight - el.scrollTop - el.clientHeight < 80; setAtBottom(near); if (near) setMissed(0); }}>
            <div className="flex min-h-full flex-col justify-end py-4">
              {hasOlder && (
                <div ref={older} className="flex h-10 items-center justify-center">
                  {loadingOlder ? <span className="h-4 w-4 animate-spin rounded-full border-2 border-muted-foreground/30 border-t-muted-foreground" role="status" aria-label="Loading earlier messages" />
                    : <button type="button" onClick={() => void loadOlder()} className="text-[12px] font-semibold text-muted-foreground hover:text-foreground">Load earlier messages</button>}
                </div>
              )}
              <div className="mx-auto mb-5 max-w-sm space-y-2 text-center">
                <p className="flex items-center justify-center gap-1.5 text-[12px] text-muted-foreground"><Lock size={12} className="shrink-0" /> Private between you two. Messages are encrypted when sent and when stored. Administrators can&apos;t read this unless someone reports it. Never send money or passwords.</p>
                {item.reason && !(messages.length === 0 && canSend) && <p className="rounded-[12px] bg-muted/60 px-3.5 py-2.5 text-[13px] italic leading-relaxed text-muted-foreground">{item.mine ? "Your note" : `${firstName}'s note`}: “{item.reason}”</p>}
              </div>

              {messages.length === 0 && canSend && (
                <div className="mb-6 flex flex-col items-center gap-3 text-center animate-in fade-in zoom-in-95 duration-500">
                  <div className="relative">
                    <UserAvatar src={other.photoUrl} name={other.name} size="xl" />
                    <span className="absolute -bottom-1 -right-1 flex h-7 w-7 items-center justify-center rounded-full border-2 border-background" style={{ background: "var(--primary)", color: "var(--primary-foreground)" }}><Check size={12} /></span>
                  </div>
                  <div>
                    <p className="text-[17px] font-semibold leading-tight">You&apos;re connected with {other.name}</p>
                    {other.headline && <p className="mt-0.5 text-[13px] text-muted-foreground">{other.headline}</p>}
                  </div>
                  {item.reason && <blockquote className="max-w-sm rounded-[12px] bg-muted/60 px-4 py-3 text-[13.5px] italic leading-relaxed text-muted-foreground">{item.mine ? "Your note" : `${firstName}'s note`}: “{item.reason}”</blockquote>}
                  <p className="text-[14px] font-medium">Say hello to start the conversation</p>
                  <div className="flex flex-wrap justify-center gap-2">
                    {starters.map((s) => (
                      <button key={s} type="button" onClick={() => { setText(s); box.current?.focus(); }} className="rounded-full border px-3.5 py-1.5 text-[13px] hover:bg-muted" style={{ borderColor: "var(--border)" }}>{s}</button>
                    ))}
                  </div>
                </div>
              )}

              {messages.map((m, i) => {
                const prev = messages[i - 1], next = messages[i + 1];
                const t = new Date(m.createdAt).getTime();
                const joinsPrev = !!prev && prev.mine === m.mine && isSameDay(new Date(prev.createdAt), new Date(m.createdAt)) && t - new Date(prev.createdAt).getTime() < GROUP_GAP_MS;
                const joinsNext = !!next && next.mine === m.mine && isSameDay(new Date(next.createdAt), new Date(m.createdAt)) && new Date(next.createdAt).getTime() - t < GROUP_GAP_MS;
                const newDay = !prev || !isSameDay(new Date(prev.createdAt), new Date(m.createdAt));
                const lastOwn = m.mine && !m.sending && !messages.slice(i + 1).some((x) => x.mine && !x.sending);
                const seen = lastOwn && otherReadAt && new Date(otherReadAt).getTime() >= t;
                return (
                  <div key={m.id}>
                    {newDay && <div className="my-5 flex justify-center"><span className="rounded-full bg-muted px-3 py-1 text-[11.5px] font-medium text-muted-foreground">{format(new Date(m.createdAt), "EEEE d MMMM")}</span></div>}
                    <div className={cn("flex", m.mine ? "justify-end" : "justify-start", joinsPrev ? "mt-0.5" : "mt-3")}>
                      <div className={cn("max-w-[82%] whitespace-pre-wrap break-words rounded-[18px] px-3.5 py-2 text-[14.5px] leading-snug sm:max-w-[70%]",
                        m.mine ? "text-white" : "bg-muted", m.mine ? (joinsNext ? "" : "rounded-br-md") : (joinsNext ? "" : "rounded-bl-md"), m.sending && "opacity-60")}
                        style={m.mine ? { background: "var(--primary)", color: "var(--primary-foreground)" } : undefined}>
                        <Linked text={m.body} />
                      </div>
                    </div>
                    {!joinsNext && (
                      <p className={cn("mt-1 px-1 text-[11px] text-muted-foreground", m.mine ? "text-right" : "text-left")}>
                        {m.sending ? "Sending…" : format(new Date(m.createdAt), "HH:mm")}{seen ? ` · Seen ${format(new Date(otherReadAt!), "HH:mm")}` : ""}
                      </p>
                    )}
                  </div>
                );
              })}
            </div>
          </div>

          {missed > 0 && !atBottom && (
            <div className="pointer-events-none relative">
              <button type="button" onClick={() => { toBottom(true); setMissed(0); }}
                className="pointer-events-auto absolute -top-12 left-1/2 -translate-x-1/2 rounded-full px-4 py-2 text-[13px] font-semibold text-white shadow-lg" style={{ background: "var(--primary)" }}>
                {missed} new {missed === 1 ? "message" : "messages"} ↓
              </button>
            </div>
          )}

          {canSend ? (
            <form className="px-3 pt-2 sm:px-4" style={{ paddingBottom: "max(0.75rem, env(safe-area-inset-bottom))" }}
              onSubmit={(e) => { e.preventDefault(); const body = text.trim(); if (body) void send(body); }}>
              <div className="flex items-end gap-1.5 rounded-[26px] border bg-background py-1.5 pl-4 pr-1.5 shadow-sm transition-[border-color,box-shadow] focus-within:border-primary/50 focus-within:shadow-[0_0_0_3px_color-mix(in_oklch,var(--primary)_14%,transparent)]" style={{ borderColor: "var(--border)" }}>
                <label htmlFor="msg" className="sr-only">Message</label>
                <Textarea ref={box} id="msg" rows={1} maxLength={INTRO_MESSAGE_MAX} value={text} placeholder={`Message ${firstName}`}
                  className="max-h-36 min-h-[40px] flex-1 resize-none rounded-none border-0 bg-transparent px-0 py-2 text-[15px] shadow-none outline-none ring-0 focus-visible:ring-0 focus-visible:ring-offset-0"
                  onChange={(e) => { setText(e.target.value); const el = e.currentTarget; el.style.height = "auto"; el.style.height = `${Math.min(el.scrollHeight, 144)}px`; }}
                  onKeyDown={(e) => { if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) { e.preventDefault(); const body = text.trim(); if (body) void send(body); } }} />
                <button type="submit" disabled={!text.trim()} aria-label="Send message"
                  className="grid h-10 w-10 shrink-0 place-items-center rounded-full text-white outline-none transition-[opacity,transform] hover:scale-105 focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 active:scale-95 disabled:scale-100 disabled:opacity-35"
                  style={{ background: "var(--primary)", color: "var(--primary-foreground)" }}>
                  <Send size={16} />
                </button>
              </div>
              <p className="mt-1.5 hidden px-4 text-[11.5px] text-muted-foreground sm:block">Enter to send · Shift + Enter for a new line</p>
            </form>
          ) : (
            <ClosedNotice condition={condition} endedByMe={endedByMe} other={other} firstName={firstName}
              onResume={() => resume.mutate()} resuming={resume.isPending} onUnblock={() => unblock.mutate()} unblocking={unblock.isPending} />
          )}
        </>
      )}

      <ConfirmModal open={confirm === "end"} title="End this conversation?" message="Neither of you will be able to send more messages. You can both still read what was said." confirmLabel="End conversation"
        isLoading={end.isPending} onConfirm={() => end.mutate()} onCancel={() => setConfirm(null)} />
      <ConfirmModal open={confirm === "withdraw"} title="Withdraw your request?" message={`${firstName} won't see it. You can ask again later.`} confirmLabel="Withdraw" variant="default"
        isLoading={withdraw.isPending} onConfirm={() => withdraw.mutate()} onCancel={() => setConfirm(null)} />
      <ConfirmModal open={confirm === "block"} title={`Block ${other.name}?`} message="You won't send or receive messages from each other, and they won't be told. You can still read this conversation, and you can unblock them any time from right here."
        confirmLabel="Block" isLoading={block.isPending} onConfirm={() => block.mutate()} onCancel={() => setConfirm(null)} />
      <ReportDialog id={id} open={reporting} onClose={() => setReporting(false)} />
    </div>
  );
}
