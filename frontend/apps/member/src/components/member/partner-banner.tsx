"use client";

import { useState } from "react";
import { X } from "@alumni/ui";
import { Button } from "@alumni/ui";

interface PartnerBannerProps {
  portalName: string;
  redirectUrl: string;
  message: string;
  expiresAt: string;
}

/**
 * Shown above every page for a while after someone registers through a partner portal's link: a plain-text message and one button
 * back to that portal. The server stops sending it once it has run out, so nothing here counts down. It can be hidden for the rest of
 * the browser session; it comes back on the next visit until the server stops sending it.
 */
export function PartnerBanner({ portalName, redirectUrl, message, expiresAt }: PartnerBannerProps) {
  const storageKey = `partner-banner-hidden:${expiresAt}`;
  const [hidden, setHidden] = useState(() => {
    try { return sessionStorage.getItem(storageKey) === "1"; } catch { return false; }
  });
  if (hidden) return null;

  function hide() {
    setHidden(true);
    try { sessionStorage.setItem(storageKey, "1"); } catch { /* it just comes back on the next page load */ }
  }

  return (
    <div role="region" aria-label={`Continue to ${portalName}`} className="sticky top-0 z-20 flex flex-wrap items-center gap-x-4 gap-y-2 border-b border-border bg-primary px-4 py-2.5 text-primary-foreground">
      <p className="min-w-0 flex-1 basis-64 text-[13px] leading-snug">{message}</p>
      <div className="flex shrink-0 items-center gap-1">
        <Button size="sm" variant="secondary" className="font-semibold" onClick={() => window.location.assign(redirectUrl)}>
          Continue to {portalName}
        </Button>
        <button type="button" onClick={hide} aria-label="Hide this message" className="flex size-9 items-center justify-center hover:bg-primary-foreground/15">
          <X size={15} />
        </button>
      </div>
    </div>
  );
}
