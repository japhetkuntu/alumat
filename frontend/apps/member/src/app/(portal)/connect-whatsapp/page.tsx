"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button, Card, CardContent, CardHeader, CardTitle, ConfirmModal } from "@alumni/ui";
import {
  approveWhatsAppNumber, refuseWhatsAppNumber, disconnectWhatsApp, getWhatsAppGroups, getNotificationPreferences, getWhatsAppStatus, startWhatsAppLink, updateNotificationPreferences,
  type WhatsAppLinkChallenge,
} from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";

const STATUS_KEY = ["m-whatsapp-status"];

/**
 * Connect this account to the member's WhatsApp. The portal sign-in proves who they are; the code, sent from WhatsApp, proves
 * the number is theirs. Nothing is verified by opening this page or tapping a link.
 */
export default function ConnectWhatsAppPage() {
  const qc = useQueryClient();
  const [challenge, setChallenge] = useState<WhatsAppLinkChallenge | null>(null);
  const [secondsLeft, setSecondsLeft] = useState(0);
  const [confirmDisconnect, setConfirmDisconnect] = useState(false);

  const status = useQuery({
    queryKey: STATUS_KEY,
    queryFn: getWhatsAppStatus,
    // While a code is waiting to be sent, check every few seconds so the page notices the moment it is used.
    refetchInterval: (q) => ((challenge || q.state.data?.pendingNumber) && !q.state.data?.connected ? 3000 : false),
  });
  const prefs = useQuery({ queryKey: ["m-notif-prefs"], queryFn: getNotificationPreferences });
  const groups = useQuery({ queryKey: ["m-whatsapp-groups"], queryFn: getWhatsAppGroups, staleTime: 5 * 60_000, retry: false });

  const start = useMutation({
    mutationFn: startWhatsAppLink,
    onSuccess: (c) => { setChallenge(c); setSecondsLeft(c.expiresInSeconds); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const settle = {
    onSuccess: () => { setChallenge(null); qc.invalidateQueries({ queryKey: STATUS_KEY }); },
    onError: (e: unknown) => toast.error(handleApiError(e)),
  };
  const approve = useMutation({ mutationFn: approveWhatsAppNumber, ...settle, onSuccess: () => { settle.onSuccess(); toast.success("WhatsApp connected"); } });
  const refuse = useMutation({ mutationFn: refuseWhatsAppNumber, ...settle });

  const disconnect = useMutation({
    mutationFn: disconnectWhatsApp,
    onSuccess: () => {
      setChallenge(null);
      setConfirmDisconnect(false);
      qc.invalidateQueries({ queryKey: STATUS_KEY });
      qc.invalidateQueries({ queryKey: ["m-notif-prefs"] });
      toast.success("WhatsApp disconnected");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const consent = useMutation({
    mutationFn: (on: boolean) => {
      const p = prefs.data!;
      return updateNotificationPreferences({
        membershipReminders: p.membershipReminders, campaignAlerts: p.campaignAlerts, eventReminders: p.eventReminders,
        jobAlerts: p.jobAlerts, classNoteAlerts: p.classNoteAlerts, spotlightAlerts: p.spotlightAlerts,
        smsAlerts: p.smsAlerts, whatsAppAlerts: on, digestFrequency: p.digestFrequency, engagementMessages: p.engagementMessages ?? true,
      });
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["m-notif-prefs"] }); qc.invalidateQueries({ queryKey: STATUS_KEY }); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  // Countdown for the code.
  useEffect(() => {
    if (!challenge || secondsLeft <= 0) return;
    const t = setTimeout(() => setSecondsLeft((s) => s - 1), 1000);
    return () => clearTimeout(t);
  }, [challenge, secondsLeft]);

  const s = status.data;
  const connected = !!s?.connected;
  const expired = !!challenge && secondsLeft <= 0 && !connected;
  const mm = String(Math.floor(secondsLeft / 60)).padStart(2, "0");
  const ss = String(secondsLeft % 60).padStart(2, "0");

  return (
    <div className="mx-auto max-w-[720px] space-y-6 p-4 sm:p-6 lg:p-8">
      <header>
        <Link href="/profile" className="text-[13px] font-semibold text-muted-foreground underline underline-offset-4">Back to profile</Link>
        <h1 className="mt-3 text-[26px] font-bold leading-tight">Connect WhatsApp</h1>
        <p className="mt-1.5 text-[14px] text-muted-foreground">
          Ask the AlumUnion assistant about your dues, events and opportunities from WhatsApp, and get alerts there instead of by SMS.
        </p>
      </header>

      {status.isLoading && <div className="skeleton h-40 w-full" aria-busy="true" />}

      {status.isError && (
        <Card><CardContent className="p-5 text-[14px]">We couldn&apos;t load your WhatsApp settings. Please refresh and try again.</CardContent></Card>
      )}

      {s && !s.available && !connected && (
        <Card>
          <CardContent className="space-y-1.5 p-5">
            <p className="text-[15px] font-semibold">The WhatsApp assistant isn&apos;t switched on yet</p>
            <p className="text-[14px] text-muted-foreground">Everything in your portal works as normal. Check back soon.</p>
          </CardContent>
        </Card>
      )}

      {s && connected && (
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-base">WhatsApp is connected</CardTitle></CardHeader>
          <CardContent className="space-y-5">
            <p className="text-[14px]">
              Number: <span className="font-semibold tabular-nums">{s.maskedNumber}</span>
            </p>

            <label className="flex cursor-pointer items-start gap-3">
              <input
                type="checkbox"
                className="mt-1 size-5 accent-[var(--primary)]"
                checked={s.notificationsEnabled}
                disabled={consent.isPending || !prefs.data}
                onChange={(e) => consent.mutate(e.target.checked)}
              />
              <span>
                <span className="block text-[14.5px] font-semibold">Send my notifications on WhatsApp</span>
                <span className="block text-[13px] text-muted-foreground">Reminders and alerts arrive here first, and by SMS only if WhatsApp can&apos;t deliver them. You can turn this off any time.</span>
              </span>
            </label>

            <div className="flex flex-wrap gap-3">
              {s.assistantNumber && (
                <a href={`https://wa.me/${s.assistantNumber}?text=menu`} target="_blank" rel="noopener noreferrer">
                  <Button>Open the assistant</Button>
                </a>
              )}
              <Button variant="outline" onClick={() => setConfirmDisconnect(true)}>Disconnect</Button>
            </div>
            <p className="text-[12.5px] text-muted-foreground">You can also disconnect by sending <span className="font-semibold">unlink</span> to the assistant.</p>
          </CardContent>
        </Card>
      )}

      {s && !connected && s.pendingNumber && (
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-base">Is this your WhatsApp number?</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <p className="text-[14px]">
              Your code was sent from <span className="font-semibold tabular-nums">{s.pendingNumber}</span>, which is different from the
              number on your account. Only connect it if it is yours.
            </p>
            <div className="flex flex-wrap gap-3">
              <Button onClick={() => approve.mutate()} isLoading={approve.isPending} loadingText="Connecting">Yes, connect it</Button>
              <Button variant="outline" onClick={() => refuse.mutate()} isLoading={refuse.isPending}>No, this isn&apos;t mine</Button>
            </div>
          </CardContent>
        </Card>
      )}

      {s && s.available && !connected && !s.pendingNumber && (
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-base">Connect in three steps</CardTitle></CardHeader>
          <CardContent className="space-y-5">
            <ol className="space-y-2 text-[14px]">
              <li><span className="font-semibold">1.</span> Get your one-time code below.</li>
              <li><span className="font-semibold">2.</span> Open WhatsApp from the button. The message is already typed.</li>
              <li><span className="font-semibold">3.</span> Send it from the WhatsApp account you want to connect.</li>
            </ol>

            {!challenge && (
              <Button onClick={() => start.mutate()} isLoading={start.isPending} loadingText="Getting your code">Get my code</Button>
            )}

            {challenge && !expired && (
              <div className="space-y-4">
                <div className="border border-border bg-muted/40 p-4">
                  <p className="text-[12.5px] font-semibold text-muted-foreground">Your code</p>
                  <p className="mt-1 text-[28px] font-bold tracking-[0.2em] tabular-nums" aria-label={`Your code is ${challenge.code.split("").join(" ")}`}>{challenge.code}</p>
                  <p className="mt-1 text-[12.5px] text-muted-foreground">Works once. Expires in {mm}:{ss}.</p>
                </div>
                {challenge.deepLink ? (
                  <a href={challenge.deepLink} target="_blank" rel="noopener noreferrer" className="inline-block">
                    <Button>Open WhatsApp</Button>
                  </a>
                ) : (
                  <p className="text-[13.5px] text-muted-foreground">
                    Send <span className="font-semibold">LINK {challenge.code}</span> to the AlumUnion Assistant on WhatsApp.
                  </p>
                )}
                <p className="text-[13px] text-muted-foreground">This page updates by itself as soon as WhatsApp confirms.</p>
              </div>
            )}

            {expired && (
              <div className="space-y-3">
                <p className="text-[14px]">That code has expired.</p>
                <Button onClick={() => start.mutate()} isLoading={start.isPending}>Get a new code</Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {connected && (
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-base">What you can send the assistant</CardTitle></CardHeader>
          <CardContent>
            <ul className="space-y-1.5 text-[14px]">
              <li><span className="font-semibold">menu</span>: dues, events, jobs and updates</li>
              <li><span className="font-semibold">rsvp 1</span>: book a place at an event you were just shown</li>
              <li><span className="font-semibold">suggest</span>: send a job or opportunity for review</li>
              <li><span className="font-semibold">post update</span>: post to your year group&apos;s wall</li>
              <li><span className="font-semibold">stop</span>: stop alerts on WhatsApp (<span className="font-semibold">alerts on</span> turns them back on)</li>
            </ul>
          </CardContent>
        </Card>
      )}

      {(groups.data?.length ?? 0) > 0 && (
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-base">Community WhatsApp groups</CardTitle></CardHeader>
          <CardContent className="space-y-3">
            {groups.data!.map((g) => (
              <div key={g.inviteLink} className="flex flex-wrap items-center justify-between gap-3">
                <div className="min-w-0">
                  <p className="truncate text-[14.5px] font-semibold">{g.name}</p>
                  {g.communityName && <p className="text-[12.5px] text-muted-foreground">{g.communityName}</p>}
                </div>
                <a href={g.inviteLink} target="_blank" rel="noopener noreferrer"><Button variant="outline">Join group</Button></a>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      <p className="text-[12.5px] leading-relaxed text-muted-foreground">
        We use your number only to recognise you in WhatsApp. The assistant never asks for your password or payment details,
        and a link or a forwarded code can&apos;t connect anyone else&apos;s WhatsApp to your account.
      </p>

      <ConfirmModal
        open={confirmDisconnect}
        title="Disconnect WhatsApp"
        message="The assistant will stop recognising this number and you'll stop getting notifications on WhatsApp. You can connect again any time."
        confirmLabel="Disconnect"
        variant="destructive"
        isLoading={disconnect.isPending}
        onConfirm={() => disconnect.mutate()}
        onCancel={() => setConfirmDisconnect(false)}
      />
    </div>
  );
}
