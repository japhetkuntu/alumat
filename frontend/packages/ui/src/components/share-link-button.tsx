"use client";

import { useState } from "react";
import { Button, type ButtonProps } from "./button";
import { Check, Share2 } from "./icons";

type ShareResult = "shared" | "copied";

export interface ShareLinkButtonProps extends Omit<ButtonProps, "onClick" | "onError"> {
  url?: string | null;
  title?: string;
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
  const resolvedUrl = url || (typeof window !== "undefined" ? window.location.href : "");

  const handleShare = async () => {
    if (!resolvedUrl) {
      onError?.("No link is available to share.");
      return;
    }

    const shareTitle = title || document.title || "Shared link";

    try {
      const shareApiAvailable = typeof navigator !== "undefined" && "share" in navigator;

      if (shareApiAvailable) {
        const shareData = { title: shareTitle, text: shareTitle, url: resolvedUrl };
        if (navigator.canShare && navigator.canShare(shareData)) {
          setIsSharing(true);
          await navigator.share(shareData);
          onSuccess?.("shared");
          return;
        }
      }

      await copyText(resolvedUrl);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
      onSuccess?.("copied");
    } catch (error) {
      const err = error as { name?: string };
      if (err?.name === "AbortError") return;
      try {
        await copyText(resolvedUrl);
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

  return (
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
  );
}
