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
    // Phone: inset from the screen edges (the portal's page content is edge-to-edge there),
    // text on top and two full-width buttons below so both are easy thumb targets.
    // Tablet and up: one compact row.
    <div className="mx-4 mt-4 mb-2 rounded-xl border border-border/40 bg-muted/30 p-4 sm:mx-0 sm:mt-0 sm:mb-4 sm:px-4 sm:py-3">
      <div className="flex flex-col gap-3.5 sm:flex-row sm:items-center sm:gap-3">
        <div className="flex min-w-0 flex-1 items-start gap-3">
          <Bell size={18} className="mt-0.5 shrink-0 text-primary" />
          <div className="min-w-0 flex-1">
            <p className="text-[14px] font-semibold leading-snug text-foreground sm:text-[13px]">Get notified about new jobs and events</p>
            <p className="mt-1 text-[13px] leading-snug text-muted-foreground sm:mt-0.5 sm:text-[12px]">
              Turn on browser notifications so you don&apos;t miss updates from your community.
            </p>
          </div>
          <button
            type="button"
            aria-label="Dismiss"
            onClick={dismiss}
            className="-mr-1 -mt-1 shrink-0 rounded-md p-1.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground sm:hidden"
          >
            <X size={16} />
          </button>
        </div>
        <div className="flex items-center gap-2 sm:shrink-0 sm:gap-1.5">
          <Button
            size="sm"
            className="h-10 flex-1 whitespace-nowrap px-2 text-[13px] font-semibold sm:h-7 sm:flex-none sm:px-3 sm:text-[12px]"
            onClick={() => void subscribe().catch(() => {})}
            disabled={isBusy}
          >
            Enable notifications
          </Button>
          <Button
            variant="ghost"
            size="sm"
            className="h-10 flex-1 whitespace-nowrap px-2 text-[13px] font-semibold sm:h-7 sm:flex-none sm:px-2.5 sm:text-[12px]"
            onClick={dismiss}
            disabled={isBusy}
          >
            Not now
          </Button>
          <button
            type="button"
            aria-label="Dismiss"
            onClick={dismiss}
            className="ml-1 hidden rounded-md p-1 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground sm:inline-flex"
          >
            <X size={14} />
          </button>
        </div>
      </div>
    </div>
  );
}
