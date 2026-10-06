"use client";

import { useEffect, useState } from "react";
import { Button, buttonVariants, type ButtonProps } from "./button";
import { cn } from "../lib/utils";
import { Check, MessageCircle, Share2 } from "./icons";

type ShareResult = "shared" | "copied";

export interface ShareLinkButtonProps extends Omit<ButtonProps, "onClick" | "onError"> {
  url?: string | null;
  title?: string;
  /** Text that goes above the link (see lib/share-messages). When set, the link is placed inside the shared text. */
  message?: string;
  shareLabel?: string;
  copiedLabel?: string;
  onSuccess?: (result: ShareResult) => void;
  onError?: (message: string) => void;
}

async function copyText(value: string) {
  if (typeof navigator !== "undefined" && navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(value);
    return;
  }

  const textarea = document.createElement("textarea");
  textarea.value = value;
  textarea.setAttribute("readonly", "true");
  textarea.style.position = "fixed";
  textarea.style.opacity = "0";
  textarea.style.left = "-9999px";
  document.body.appendChild(textarea);
  textarea.select();
  textarea.setSelectionRange(0, textarea.value.length);
  document.execCommand("copy");
  document.body.removeChild(textarea);
}

export function ShareLinkButton({
  url,
  title,
  message,
  shareLabel = "Share",
  copiedLabel = "Copied!",
  onSuccess,
  onError,
  variant = "outline",
  size = "sm",
  type = "button",
  className,
  ...props
}: ShareLinkButtonProps) {
  const [copied, setCopied] = useState(false);
  const [isSharing, setIsSharing] = useState(false);
  // Where the system share sheet exists (phones) it already offers WhatsApp; where it doesn't (most desktops)
  // Share can only copy, so a contextual message gets its own WhatsApp button. Decided after mount to stay hydration-safe.
  const [offerWhatsApp, setOfferWhatsApp] = useState(false);
  useEffect(() => { setOfferWhatsApp(!!message && !("share" in navigator)); }, [message]);
  const resolvedUrl = url || (typeof window !== "undefined" ? window.location.href : "");
  const shareText = message ? `${message.trimEnd()}\n${resolvedUrl}` : resolvedUrl;

  const handleShare = async () => {
    if (!resolvedUrl) {
      onError?.("No link is available to share.");
      return;
    }

    const shareTitle = title || document.title || "Shared link";

    try {
      const shareApiAvailable = typeof navigator !== "undefined" && "share" in navigator;

      if (shareApiAvailable) {
        // Deliberately omit `text` — passing both `text` and `url` is what causes
        // some share targets (notably WhatsApp via Android's Web Share bridge) to
        // render the link twice: the OS-level share intent appends `url` to
        // `text` for apps that only accept plain text, so a `text` that doesn't
        // already embed the link still ends up duplicated in the final message.
        // With a contextual message, the link is embedded in `text` and `url` is left out, which keeps
        // WhatsApp from rendering it twice.
        const shareData = message ? { title: shareTitle, text: shareText } : { title: shareTitle, url: resolvedUrl };
        if (navigator.canShare && navigator.canShare(shareData)) {
          setIsSharing(true);
          await navigator.share(shareData);
          onSuccess?.("shared");
          return;
        }
      }

      await copyText(shareText);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
      onSuccess?.("copied");
    } catch (error) {
      const err = error as { name?: string };
      if (err?.name === "AbortError") return;
      try {
        await copyText(shareText);
        setCopied(true);
        window.setTimeout(() => setCopied(false), 2000);
        onSuccess?.("copied");
      } catch {
        onError?.("Unable to copy the link. Please copy it manually.");
      }
    } finally {
      setIsSharing(false);
    }
  };

  const waHref = `https://wa.me/?text=${encodeURIComponent(shareText)}`;

  return (
    <>
    <Button
      type={type}
      variant={variant}
      size={size}
      className={className}
      onClick={() => { void handleShare(); }}
      aria-label={copied ? copiedLabel : shareLabel}
      title={copied ? copiedLabel : shareLabel}
      {...props}
    >
      {copied ? (
        <>
          <Check size={14} />
          {copiedLabel}
        </>
      ) : (
        <>
          <Share2 size={14} />
          {isSharing ? "Sharing…" : shareLabel}
        </>
      )}
    </Button>
    {offerWhatsApp && (
      <a
        href={waHref} target="_blank" rel="noopener noreferrer" aria-label="Share on WhatsApp" title="Share on WhatsApp"
        className={cn(buttonVariants({ variant, size }), className)}
      >
        <MessageCircle size={14} />
        WhatsApp
      </a>
    )}
    </>
  );
}
