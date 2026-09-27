"use client";

import { useEffect, useState } from "react";
import { Smartphone, Share2, X } from "@alumni/ui";
import { Button } from "@alumni/ui";

const DISMISS_KEY = "install_prompt_dismissed_until";
const COOLDOWN_DAYS = 30;

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: "accepted" | "dismissed" }>;
}

function isStandalone(): boolean {
  if (typeof window === "undefined") return false;
  return window.matchMedia("(display-mode: standalone)").matches || (window.navigator as { standalone?: boolean }).standalone === true;
}

function isIos(): boolean {
  if (typeof navigator === "undefined") return false;
  return /iphone|ipad|ipod/i.test(navigator.userAgent);
}

/**
 * Shared eligibility + state for the install nudge, split out from the banner itself so the
 * layout can decide whether to show this or the push-notification nudge — never both at once.
 */
export function useInstallPrompt() {
  const [deferredPrompt, setDeferredPrompt] = useState<BeforeInstallPromptEvent | null>(null);
  const [showIosHint, setShowIosHint] = useState(false);
  const [dismissed, setDismissed] = useState(true);

  useEffect(() => {
    if (isStandalone()) return;
    try {
      const until = Number(localStorage.getItem(DISMISS_KEY) ?? 0);
      setDismissed(Date.now() < until);
    } catch { /* ignore */ }

    function onBeforeInstallPrompt(e: Event) {
      e.preventDefault();
      setDeferredPrompt(e as BeforeInstallPromptEvent);
    }
    window.addEventListener("beforeinstallprompt", onBeforeInstallPrompt);

    if (isIos()) setShowIosHint(true);

    return () => window.removeEventListener("beforeinstallprompt", onBeforeInstallPrompt);
  }, []);

  const visible = !dismissed && !isStandalone() && (!!deferredPrompt || showIosHint);

  function dismiss() {
    try {
      localStorage.setItem(DISMISS_KEY, String(Date.now() + COOLDOWN_DAYS * 24 * 60 * 60 * 1000));
    } catch { /* ignore */ }
    setDismissed(true);
  }

  async function install() {
    if (!deferredPrompt) return;
    await deferredPrompt.prompt();
    const { outcome } = await deferredPrompt.userChoice;
    if (outcome === "accepted") setDeferredPrompt(null);
    dismiss();
  }

  return { visible, canInstall: !!deferredPrompt, showIosHint: showIosHint && !deferredPrompt, dismiss, install };
}

/**
 * A quiet, dismissible nudge to install the admin portal to the home screen. Chrome/Edge/
 * Android get the native install prompt; iOS Safari has no such API, so it gets short
 * written instructions for the Share-sheet flow instead. Render only when
 * useInstallPrompt().visible is true — the layout picks this over the push-notification
 * nudge when both would qualify.
 */
export function InstallPromptBanner() {
  const { canInstall, showIosHint, dismiss, install } = useInstallPrompt();

  return (
    <div className="mx-4 mt-4 mb-2 rounded-xl border border-border/40 bg-muted/30 p-4 sm:mx-0 sm:mt-0 sm:mb-4 sm:px-4 sm:py-3">
      <div className="flex flex-col gap-3.5 sm:flex-row sm:items-center sm:gap-3">
        <div className="flex min-w-0 flex-1 items-start gap-3">
          <Smartphone size={18} className="mt-0.5 shrink-0 text-primary" />
          <div className="min-w-0 flex-1">
            <p className="text-[14px] font-semibold leading-snug text-foreground sm:text-[13px]">Add this portal to your home screen</p>
            <p className="mt-1 text-[13px] leading-snug text-muted-foreground sm:mt-0.5 sm:text-[12px]">
              {showIosHint
                ? <>Tap <Share2 size={12} className="inline -mt-0.5" /> Share, then &quot;Add to Home Screen&quot;.</>
                : "One tap to open it like an app, no browser bar."}
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
          {canInstall && (
            <Button
              size="sm"
              className="h-10 flex-1 whitespace-nowrap px-2 text-[13px] font-semibold sm:h-7 sm:flex-none sm:px-3 sm:text-[12px]"
              onClick={() => void install()}
            >
              Add to home screen
            </Button>
          )}
          <Button
            variant="ghost"
            size="sm"
            className="h-10 flex-1 whitespace-nowrap px-2 text-[13px] font-semibold sm:h-7 sm:flex-none sm:px-2.5 sm:text-[12px]"
            onClick={dismiss}
          >
            {canInstall ? "Not now" : "Got it"}
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
