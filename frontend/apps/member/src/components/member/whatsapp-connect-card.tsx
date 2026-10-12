"use client";

import Link from "next/link";
import { useCallback, useSyncExternalStore } from "react";
import { useQuery } from "@tanstack/react-query";
import { MessageCircle } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { getWhatsAppStatus, type WhatsAppStatus } from "@/lib/member-api";

/** One shared read of the member's WhatsApp state, for the sidebar, the home card and the checklist. */
export function useWhatsAppStatus(enabled = true) {
  return useQuery<WhatsAppStatus>({ queryKey: ["m-whatsapp-status"], queryFn: getWhatsAppStatus, enabled, staleTime: 5 * 60_000, retry: false });
}

const SNOOZE_DAYS = 14;
const key = (userId: string) => `member-wa-nudge:${userId}`;

function useSnoozed(userId: string | undefined) {
  const subscribe = useCallback((notify: () => void) => {
    window.addEventListener("storage", notify);
    window.addEventListener("member-wa-nudge", notify);
    return () => { window.removeEventListener("storage", notify); window.removeEventListener("member-wa-nudge", notify); };
  }, []);
  // Hidden until the browser says otherwise: a card that flashes and disappears is worse than one that arrives a moment late.
  return useSyncExternalStore(subscribe, () => {
    if (!userId) return true;
    try {
      const at = Number(localStorage.getItem(key(userId)) ?? 0);
      return at > 0 && Date.now() - at < SNOOZE_DAYS * 86_400_000;
    } catch { return false; }
  }, () => true);
}

/**
 * A plain, dismissible prompt on the member's home page, shown only while there is something to do: the assistant is on for
 * this community and this member has not connected. "Not now" hides it for two weeks; connecting removes it for good.
 */
export function WhatsAppConnectCard() {
  const { user } = useAuth();
  const status = useWhatsAppStatus();
  const snoozed = useSnoozed(user?.id);

  const s = status.data;
  if (!s || !s.available || s.connected || snoozed) return null;

  const snooze = () => {
    if (!user?.id) return;
    try { localStorage.setItem(key(user.id), String(Date.now())); } catch { /* ignore */ }
    window.dispatchEvent(new Event("member-wa-nudge"));
  };

  return (
    <section aria-labelledby="wa-connect-heading" className="flex flex-wrap items-center gap-x-5 gap-y-3 border border-border bg-card p-4 sm:p-5">
      <span className="flex size-10 shrink-0 items-center justify-center bg-primary/10 text-primary" aria-hidden><MessageCircle size={20} /></span>
      <div className="min-w-0 flex-1 basis-60">
        <h2 id="wa-connect-heading" className="text-[15px] font-semibold leading-snug">Get your alerts and answers on WhatsApp</h2>
        <p className="mt-0.5 text-[13.5px] text-muted-foreground">Ask about your dues, events and jobs in a chat, and get reminders there instead of by SMS. It takes under a minute.</p>
      </div>
      <div className="flex shrink-0 items-center gap-4">
        <Link href="/connect-whatsapp" className="inline-flex h-10 items-center bg-primary px-4 text-[14px] font-semibold text-primary-foreground">Connect WhatsApp</Link>
        <button type="button" onClick={snooze} className="py-2 text-[13px] font-semibold text-muted-foreground underline underline-offset-4">Not now</button>
      </div>
    </section>
  );
}
