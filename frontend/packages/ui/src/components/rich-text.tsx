"use client";

import * as React from "react";
import DOMPurify from "dompurify";
import { cn } from "../lib/utils";
import { looksLikeHtml, plainToRichHtml, toEditorHtml, richTextToPlain } from "../lib/rich-text-utils";

/*
 * Descriptions can be plain text (everything written before the editor existed) or the small, safe HTML the editor makes. These helpers
 * treat both the same way for the reader, and never trust stored HTML: the server removes anything unsafe when it saves, and the viewer
 * below removes it again before showing it, so one mistake in either place cannot put a script on a page.
 */

const ALLOWED_TAGS = ["p", "br", "strong", "b", "em", "i", "u", "s", "strike", "del", "ul", "ol", "li", "h1", "h2", "h3", "h4", "blockquote", "hr", "a", "code", "pre"];
const ALLOWED_ATTR = ["href", "target", "rel", "style"];

let hooked = false;
function purifier() {
  if (!hooked) {
    hooked = true;
    DOMPurify.addHook("afterSanitizeAttributes", (node) => {
      if (node instanceof HTMLElement) {
        // The only style the editor makes is alignment; keep that and nothing else.
        const align = node.style?.textAlign;
        node.removeAttribute("style");
        if (align && ["left", "center", "right", "justify"].includes(align)) node.style.textAlign = align;
        if (node.tagName === "A" && node.getAttribute("href")) {
          node.setAttribute("target", "_blank");
          node.setAttribute("rel", "noopener noreferrer nofollow");
        }
      }
    });
  }
  return DOMPurify;
}

/** HTML that is safe to put on a page: only what the editor can make, links that open in a new tab, nothing that runs. */
export function sanitizeRichHtml(content?: string | null): string {
  if (!content || !content.trim()) return "";
  const html = looksLikeHtml(content) ? content : plainToRichHtml(content);
  return purifier().sanitize(html, {
    ALLOWED_TAGS, ALLOWED_ATTR, ALLOW_DATA_ATTR: false, ALLOWED_URI_REGEXP: /^(?:https?|mailto|tel):/i,
  }) as string;
}

const noop = () => () => {};
/** False while rendering on the server and during hydration, true in the browser afterwards. */
function useInBrowser() {
  return React.useSyncExternalStore(noop, () => true, () => false);
}

/** The reading styles for formatted descriptions, shared by the viewer and the editor. */
export const RICH_TEXT_STYLES =
  "[&_p]:my-2 [&_p:first-child]:mt-0 [&_p:last-child]:mb-0 [&_h1]:mt-5 [&_h1]:mb-2 [&_h1]:text-[1.35rem] [&_h1]:font-bold [&_h1]:leading-snug " +
  "[&_h2]:mt-5 [&_h2]:mb-2 [&_h2]:text-[1.15rem] [&_h2]:font-semibold [&_h2]:leading-snug [&_h3]:mt-4 [&_h3]:mb-1.5 [&_h3]:text-[1rem] [&_h3]:font-semibold " +
  "[&_h4]:mt-3 [&_h4]:mb-1 [&_h4]:font-semibold [&_ul]:my-2 [&_ul]:list-disc [&_ul]:pl-6 [&_ol]:my-2 [&_ol]:list-decimal [&_ol]:pl-6 [&_li]:my-0.5 [&_li>p]:my-0 " +
  "[&_blockquote]:my-3 [&_blockquote]:border-l-4 [&_blockquote]:border-border [&_blockquote]:pl-4 [&_blockquote]:text-muted-foreground [&_hr]:my-5 [&_hr]:border-border " +
  "[&_a]:text-primary [&_a]:underline [&_a]:underline-offset-2 [&_a]:break-words [&_code]:bg-muted [&_code]:px-1 [&_code]:text-[0.9em] [&_pre]:my-3 [&_pre]:overflow-x-auto [&_pre]:bg-muted [&_pre]:p-3 " +
  "[&_h1:first-child]:mt-0 [&_h2:first-child]:mt-0 [&_h3:first-child]:mt-0";

/**
 * Shows a description: formatted text from the editor, or plain text from before it. Always cleaned first; while rendering on the server
 * only the words are shown, so no stored markup is ever put into a page without being cleaned in the browser.
 */
export function RichText({ content, className }: { content?: string | null; className?: string }) {
  const inBrowser = useInBrowser();
  const html = React.useMemo(() => (inBrowser ? sanitizeRichHtml(content) : ""), [inBrowser, content]);
  if (!content || !content.trim()) return null;
  const base = cn("max-w-none break-words text-[0.9625rem] leading-[1.75]", RICH_TEXT_STYLES, className);
  if (!inBrowser) return <div className={cn(base, "whitespace-pre-wrap")}>{richTextToPlain(content)}</div>;
  return <div className={base} dangerouslySetInnerHTML={{ __html: html }} />;
}
