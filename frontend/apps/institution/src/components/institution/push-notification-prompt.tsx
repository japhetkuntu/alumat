"use client";

import { useEffect, useState } from "react";
import { Bell, X } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { usePushNotifications } from "@/hooks/use-push-notifications";

const DISMISS_KEY = "push_prompt_dismissed_until";
const COOLDOWN_DAYS = 14;

export function PushNotificationPrompt() {
  const { isSupported, permission, isSubscribed, isBusy, subscribe } = usePushNotifications();
  const [dismissed, setDismissed] = useState(true);

  useEffect(() => {
    const until = Number(localStorage.getItem(DISMISS_KEY) ?? 0);
    setDismissed(Date.now() < until);
  }, []);

  if (!isSupported || permission !== "default" || isSubscribed || dismissed) return null;

  function dismiss() {
    localStorage.setItem(DISMISS_KEY, String(Date.now() + COOLDOWN_DAYS * 24 * 60 * 60 * 1000));
    setDismissed(true);
  }

  return (
    <div className="mx-4 sm:mx-6 mt-4 flex items-start gap-3 rounded-xl border border-border/40 bg-muted/30 px-4 py-3">
      <Bell size={16} className="mt-0.5 shrink-0 text-primary" />
      <div className="min-w-0 flex-1">
        <p className="text-[13px] font-semibold text-foreground">Get notified about new registrations and approvals</p>
        <p className="text-[12px] text-muted-foreground mt-0.5">
          Turn on browser notifications so you don&apos;t miss anything that needs your attention.
        </p>
      </div>
      <div className="flex shrink-0 items-center gap-1.5">
        <Button variant="ghost" size="sm" className="h-7 px-2.5 text-[12px] font-semibold" onClick={dismiss} disabled={isBusy}>
          Not now
        </Button>
        <Button size="sm" className="h-7 px-3 text-[12px] font-semibold" onClick={() => subscribe()} disabled={isBusy}>
          Enable
        </Button>
        <button
          type="button"
          aria-label="Dismiss"
          onClick={dismiss}
          className="ml-1 rounded-md p-1 text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
        >
          <X size={14} />
        </button>
      </div>
    </div>
  );
}
