"use client";

import { useState } from "react";
import { Button, type ButtonProps } from "./button";
import { cn } from "../lib/utils";
import { Check, Share2 } from "./icons";

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
  /** Spread the buttons across the full width of their row, equal widths — for a card footer. */
  fill?: boolean;
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
  fill,
  variant = "outline",
  size = "sm",
  type = "button",
  className,
  ...props
}: ShareLinkButtonProps) {
  const [copied, setCopied] = useState(false);
  const [isSharing, setIsSharing] = useState(false);
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
        // some share targets (messaging apps reached through Android's Web Share bridge) to
        // render the link twice: the OS-level share intent appends `url` to
        // `text` for apps that only accept plain text, so a `text` that doesn't
        // already embed the link still ends up duplicated in the final message.
        // With a contextual message, the link is embedded in `text` and `url` is left out, which keeps
        // those apps from rendering it twice.
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


  // One wrapper so the buttons travel (and wrap) together wherever a page puts this: as bare siblings they
  // overflowed any narrow header row that did not itself allow wrapping.
  return (
    <div className={cn("max-w-full items-center gap-2", fill ? "flex w-full" : "inline-flex flex-wrap")}>
    <Button
      type={type}
      variant={variant}
      size={size}
      className={cn(fill && "flex-1", className)}
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
    </div>
  );
}
