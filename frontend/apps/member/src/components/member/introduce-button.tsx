"use client";

import Link from "next/link";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { formatDistanceToNowStrict } from "date-fns";
import { Button, Check, Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, FormError, Label, MessageCircle, Textarea, UserAvatar } from "@alumni/ui";
import {
  INTRO_REASON_MAX, INTRO_REASON_MIN, getIntroState, requestIntroduction, resumeIntroduction, unblockMember, withdrawIntroduction,
} from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import { useFeatures } from "@/components/member/member-layout";

/** "just now", "3 minutes ago": never "0 seconds ago". */
const ago = (iso: string) => (Date.now() - new Date(iso).getTime() < 45_000 ? "just now" : `${formatDistanceToNowStrict(new Date(iso))} ago`);

export interface IntroducePerson { name: string; photoUrl?: string | null; subtitle?: string | null }

const RECONNECT_STARTERS = [
  "I'd love to pick our conversation back up.",
  "Something came up that I'd like to talk to you about.",
  "I'd like to say thank you and keep in touch.",
];

const STARTERS = [
  "We're from the same programme and I'd love to connect.",
  "I'm exploring a similar path and would value your advice.",
  "I came across your profile and would like to say hello.",
];

/**
 * Asks another member for an introduction, or shows where that stands: waiting for a reply (with a way to withdraw), a conversation
 * to open or resume, or a block to undo. Nothing is shared and no message can be sent until the other member accepts, so the form asks
 * for one thing: a short, honest reason.
 */
export function IntroduceButton({ memberId, firstName, person }: { memberId: string; firstName: string; person?: IntroducePerson }) {
  const features = useFeatures();
  const { user } = useAuth();
  const qc = useQueryClient();
  const on = features.enabled("Introductions") && !!user && user.id !== memberId;
  const [open, setOpen] = useState(false);
  const [sent, setSent] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);

  const { data: state } = useQuery({ queryKey: ["intro-state", memberId], queryFn: () => getIntroState(memberId), enabled: on, staleTime: 30_000, retry: false });
  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["intro-state", memberId] });
    qc.invalidateQueries({ queryKey: ["intros"] });
    qc.invalidateQueries({ queryKey: ["intro-summary"] });
    qc.invalidateQueries({ queryKey: ["intro-any"] });
  };

  const send = useMutation({
    mutationFn: () => requestIntroduction(memberId, reason.trim()),
    // The status behind the sheet ("Ask" turns into "Request sent") is refreshed once the sheet has closed, so the page under it does not
    // jump while the sheet is still on screen.
    onSuccess: () => { setSent(true); setError(null); refreshAfterClose.current = true; },
    onError: (e) => setError(handleApiError(e)),
  });
  const withdraw = useMutation({
    mutationFn: () => withdrawIntroduction(state!.introductionId!),
    onSuccess: () => { toast.success("Request withdrawn."); refresh(); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const resume = useMutation({
    mutationFn: () => resumeIntroduction(state!.introductionId!),
    onSuccess: () => { toast.success(`Conversation with ${firstName} resumed.`); refresh(); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const unblock = useMutation({
    mutationFn: () => unblockMember(memberId),
    onSuccess: () => { toast.success(`${firstName} unblocked.`); refresh(); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  // When they ended an earlier conversation, a yes brings that same chat back with all its messages instead of starting a new one.
  // Decided when the sheet opens and held while it is open: sending changes the status behind it ("reconnectable" becomes "pending"),
  // and the sheet's wording must not change under the person's eyes.
  const reconnectAtOpen = useRef(false);
  const reconnect = open ? reconnectAtOpen.current : state?.state === "reconnectable";
  const starters = reconnect ? RECONNECT_STARTERS : STARTERS;
  const length = reason.trim().length;
  const addStarter = (s: string) => setReason((r) => (r.trim() ? `${r.trim()} ${s}` : s).slice(0, INTRO_REASON_MAX));
  // The sheet keeps showing "Request sent" while it slides away. Clearing the form at the moment of closing would flash the empty form
  // (and change the sheet's height) during that exit, so it is cleared the next time the sheet is opened instead.
  const refreshAfterClose = useRef(false);
  const close = (v: boolean) => {
    setOpen(v);
    if (!v) {
      setError(null);
      if (refreshAfterClose.current) { refreshAfterClose.current = false; window.setTimeout(refresh, 320); }
    }
  };
  const openSheet = () => { reconnectAtOpen.current = state?.state === "reconnectable"; if (sent) { setSent(false); setReason(""); } setOpen(true); };


  const dialog = (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent>
          {sent ? (
            <div className="flex flex-col items-center gap-4 px-2 py-4 text-center animate-in fade-in zoom-in-95 duration-300">
              <span className="flex h-14 w-14 items-center justify-center rounded-full text-white" style={{ background: "var(--primary)" }}><Check size={24} /></span>
              <div className="space-y-1.5">
              <DialogTitle className="text-[19px]">Request sent to {firstName}</DialogTitle>
              <DialogDescription>{reconnect
                ? "They'll see your note and can accept or decline. If they accept, your earlier conversation reopens right where you left off, and we'll let you know."
                : "They'll see your note and can accept or decline. When they accept, a private chat opens here and we'll let you know."}</DialogDescription>
              </div>
              <div className="flex w-full flex-col gap-2 sm:flex-row sm:justify-center">
                <Button className="h-12 rounded-full px-7 text-[15px] font-semibold sm:h-11" onClick={() => close(false)}>Done</Button>
                <Link href="/connections?tab=requests" onClick={() => { if (refreshAfterClose.current) { refreshAfterClose.current = false; refresh(); } }}><Button variant="outline" className="h-12 w-full rounded-full text-[15px] font-semibold text-foreground sm:h-11">See my requests</Button></Link>
              </div>
            </div>
          ) : (
            <>
            <DialogHeader>
              <DialogTitle>{reconnect ? "Ask to reconnect" : "Ask for an introduction"}</DialogTitle>
              <DialogDescription>{reconnect
                ? `${firstName} ended your conversation. If they accept, it reopens right where you left off, with all your messages. Your note is stored encrypted.`
                : `${firstName} reads your note and chooses whether to accept. Nothing is shared until they do, and your note is stored encrypted.`}</DialogDescription>
              </DialogHeader>
              <div className="mt-3 flex items-center gap-3 rounded-[12px] border p-3" style={{ borderColor: "var(--border)" }}>
                <UserAvatar src={person?.photoUrl} name={person?.name ?? firstName} size="lg" />
                <div className="min-w-0">
                  <p className="truncate text-[14.5px] font-semibold leading-tight">{person?.name ?? firstName}</p>
                  {person?.subtitle && <p className="mt-0.5 truncate text-[12.5px] text-muted-foreground">{person.subtitle}</p>}
                </div>
              </div>
              <div className="mt-4 space-y-2">
                <Label htmlFor="intro-reason">{reconnect ? "Why would you like to reconnect?" : "Why would you like to connect?"}</Label>
                <Textarea id="intro-reason" rows={4} maxLength={INTRO_REASON_MAX} value={reason} onChange={(e) => setReason(e.target.value)} autoFocus
                  placeholder="Same programme, 2014, and I'm moving into data work. I'd value your advice." className="resize-none rounded-[12px] text-[14px]" />
                <div className="flex items-center justify-between gap-3 text-[12px]" style={{ color: "var(--muted-foreground)" }}>
                  <span>{length > 0 && length < INTRO_REASON_MIN ? `A little more, please (${INTRO_REASON_MIN - length} to go)` : "Be specific and kind. People say yes to a clear reason."}</span>
                  <span className="tabular-nums">{length}/{INTRO_REASON_MAX}</span>
                </div>
                <div className="flex flex-wrap gap-1.5 pt-1" aria-label="Ways to start">
                  {starters.map((s) => (
                    <button key={s} type="button" onClick={() => addStarter(s)} className="rounded-full border px-3 py-1 text-[12.5px] transition-colors hover:bg-muted" style={{ borderColor: "var(--border)" }}>{s}</button>
                  ))}
                </div>
              </div>
              <FormError message={error} className="mt-2" />
              <div className="mt-4 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                <button type="button" onClick={() => close(false)} className="rounded-full px-5 py-2.5 text-[14px] font-semibold text-muted-foreground transition-colors hover:bg-muted hover:text-foreground">Cancel</button>
                <Button className="h-12 rounded-full px-7 text-[15px] font-semibold sm:h-11" onClick={() => { setError(null); send.mutate(); }} disabled={length < INTRO_REASON_MIN} isLoading={send.isPending} loadingText="Sending…">Send request</Button>
              </div>
            </>
          )}
        </DialogContent>
    </Dialog>
  );

  if (!on || !state || state.state === "self" || state.state === "unavailable") return null;

  const note = (children: React.ReactNode) => (
    <div className="rounded-[12px] border px-4 py-3 text-[13px] leading-relaxed" style={{ borderColor: "var(--border)", background: "color-mix(in oklch, var(--muted) 55%, transparent)" }}>{children}</div>
  );

  if (state.state === "connected" && state.introductionId)
    return <Link href={`/connections/${state.introductionId}`}><Button className="w-full gap-2 text-[13.5px] font-semibold" style={{ height: 42 }}><MessageCircle size={15} /> Open your chat with {firstName}</Button></Link>;
  if (state.state === "incoming")
    return <Link href={`/connections/${state.introductionId}`}><Button className="w-full gap-2 text-[13.5px] font-semibold" style={{ height: 42 }}><MessageCircle size={15} /> {firstName} would like to connect: reply</Button></Link>;

  if (state.state === "resumable")
    return note(
      <div className="space-y-2.5">
        <p className="font-semibold text-foreground">You ended your conversation with {firstName}.</p>
        <p className="text-muted-foreground">Your messages are kept. You can pick it up again whenever you like.</p>
        <Button className="w-full" size="sm" onClick={() => resume.mutate()} isLoading={resume.isPending} loadingText="Resuming…">Resume conversation</Button>
      </div>,
    );

  if (state.state === "blocked")
    return note(
      <div className="space-y-2.5">
        <p className="font-semibold text-foreground">You blocked {firstName}.</p>
        <p className="text-muted-foreground">You won&apos;t get messages from each other. They haven&apos;t been told.</p>
        <Button className="w-full" size="sm" variant="outline" onClick={() => unblock.mutate()} isLoading={unblock.isPending} loadingText="Unblocking…">Unblock {firstName}</Button>
      </div>,
    );

  if (state.state === "pending")
    return (<>{note(
      <div className="space-y-1.5">
        <p className="flex items-center gap-2 font-semibold text-foreground"><Check size={14} className="text-primary" /> Request sent{state.askedAt ? ` ${ago(state.askedAt)}` : ""}</p>
        <p className="text-muted-foreground">{firstName} will see your note and can accept or decline. You&apos;ll be told when they accept.</p>
        <button type="button" className="text-[12.5px] font-semibold text-muted-foreground underline underline-offset-4 hover:text-foreground disabled:opacity-50"
          disabled={withdraw.isPending} onClick={() => withdraw.mutate()}>{withdraw.isPending ? "Withdrawing…" : "Withdraw request"}</button>
      </div>,
    )}{dialog}</>);

  return (
    <>
      <Button className="w-full gap-2 text-[13.5px] font-semibold" style={{ height: 42 }} onClick={openSheet}>
        <MessageCircle size={15} /> {reconnect ? `Ask ${firstName} to reopen your conversation` : `Ask ${firstName} for an introduction`}
      </Button>
      {dialog}
    </>
  );
}
