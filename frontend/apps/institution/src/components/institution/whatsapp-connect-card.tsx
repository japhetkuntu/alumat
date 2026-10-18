"use client";

import Link from "next/link";
import { useCallback, useSyncExternalStore } from "react";
import { useQuery } from "@tanstack/react-query";
import { MessageCircle } from "@alumni/ui";
import { getWhatsAppOverview } from "@/lib/institution-api";
import { useAuth } from "@/hooks/use-auth";
import { useWhatsApp } from "@/hooks/use-institution-features";

const SNOOZE_DAYS = 14;
const KEY = "institution-wa-nudge";

function useSnoozed() {
  const subscribe = useCallback((notify: () => void) => {
    window.addEventListener("storage", notify);
    window.addEventListener("institution-wa-nudge", notify);
    return () => { window.removeEventListener("storage", notify); window.removeEventListener("institution-wa-nudge", notify); };
  }, []);
  return useSyncExternalStore(subscribe, () => {
    try {
      const at = Number(localStorage.getItem(KEY) ?? 0);
      return at > 0 && Date.now() - at < SNOOZE_DAYS * 86_400_000;
    } catch { return false; }
  }, () => true);
}

/**
 * Full administrators only, and only while there is something to do: WhatsApp is on for the platform and this institution, and no
 * group is connected yet. Quiet on any error (a switched-off feature answers 403): a dashboard is never blocked by a card.
 */
export function WhatsAppConnectCard() {
  const { user } = useAuth();
  const { on: whatsAppOn } = useWhatsApp();
  const enabled = user?.role === "SuperAdmin" && whatsAppOn;   // nothing about WhatsApp until the platform has it on and the institution wants it
  const snoozed = useSnoozed();
  const { data } = useQuery({ queryKey: ["whatsapp-overview"], queryFn: getWhatsAppOverview, enabled, staleTime: 60_000, retry: false });

  if (!enabled || !data || !data.platformEnabled || data.groups.length > 0 || snoozed) return null;

  const snooze = () => {
    try { localStorage.setItem(KEY, String(Date.now())); } catch { /* ignore */ }
    window.dispatchEvent(new Event("institution-wa-nudge"));
  };

  return (
    <section aria-labelledby="wa-setup-heading" className="mb-3.5 flex flex-wrap items-center gap-x-5 gap-y-3 border border-border bg-card p-4 sm:p-5">
      <span className="flex size-10 shrink-0 items-center justify-center bg-primary/10 text-primary" aria-hidden><MessageCircle size={20} /></span>
      <div className="min-w-0 flex-1 basis-64">
        <h2 id="wa-setup-heading" className="text-[15px] font-semibold leading-snug">Bring your WhatsApp group into AlumUnion</h2>
        <p className="mt-0.5 text-[13.5px] text-muted-foreground">Connect a group so members can ask the assistant about events, jobs and fundraisers, and you can post to it from here in two taps.</p>
      </div>
      <div className="flex shrink-0 items-center gap-4">
        <Link href="/settings?tab=whatsapp" className="inline-flex h-10 items-center bg-primary px-4 text-[14px] font-semibold text-primary-foreground">Set up WhatsApp</Link>
        <button type="button" onClick={snooze} className="py-2 text-[13px] font-semibold text-muted-foreground underline underline-offset-4">Not now</button>
      </div>
    </section>
  );
}
