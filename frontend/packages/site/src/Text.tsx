"use client";

import { useEffect, useRef } from "react";
import type { EditApi } from "./edit";

/** Strips a pasted or typed string down to plain text for single-line fields. */
const plain = (s: string) => s.replace(/\s+/g, " ").trim();

interface EdProps {
  value?: string;
  edit?: EditApi;
  sectionId?: string;
  itemId?: string;
  field: string;
  as?: "span" | "div";
  className?: string;
  /** Shown faintly in the editor when the text is empty. */
  placeholder?: string;
}

/**
 * A piece of text on the page. Visitors read it; in the editor the same text is edited right where it sits, so people change words by
 * clicking them. It writes back on every keystroke, and never lets formatting in: pasted text becomes plain text.
 */
export function Ed({ value = "", edit, sectionId, itemId, field, as = "span", className, placeholder }: EdProps) {
  const ref = useRef<HTMLElement>(null);
  const Tag = as as "span";
  // Keep the DOM in step with the saved value (after undo, or a template change) without moving the cursor while typing.
  useEffect(() => { const el = ref.current; if (el && edit && el.innerText !== value && document.activeElement !== el) el.innerText = value; }, [value, edit]);
  // The text is written into the element by hand rather than by React, so typing never moves the cursor.
  if (!edit) return value ? <Tag className={className}>{value}</Tag> : null;
  return (
    <Tag
      ref={ref as never}
      className={`${className ?? ""} ed`}
      contentEditable suppressContentEditableWarning spellCheck={false}
      data-placeholder={placeholder ?? "Type here"}
      onClick={(e) => e.stopPropagation()}
      onFocus={() => { /* selecting the section is handled by the click on its wrapper */ }}
      onPaste={(e) => { e.preventDefault(); document.execCommand("insertText", false, plain(e.clipboardData.getData("text/plain"))); }}
      onInput={(e) => { const v = (e.currentTarget as HTMLElement).innerText.replace(/\n+/g, " "); itemId ? edit.setItemField(sectionId!, itemId, field, v) : edit.setProp(sectionId!, field, v); }}
      onKeyDown={(e) => { if (e.key === "Enter") e.preventDefault(); }}
    />
  );
}

interface RichProps { value?: string; edit?: EditApi; sectionId?: string; itemId?: string; field: string; className?: string }

/**
 * A paragraph with a little formatting (bold, italic, lists, links). The server has already cleaned the HTML down to that small set,
 * so it is safe to show as is. In the editor it is edited in place, and only the same small set can be written back.
 */
export function Rich({ value = "", edit, sectionId, itemId, field, className }: RichProps) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => { const el = ref.current; if (el && edit && el.innerHTML !== value && document.activeElement !== el) el.innerHTML = value; }, [value, edit]);
  if (!edit) return value ? <div className={`rich ${className ?? ""}`} dangerouslySetInnerHTML={{ __html: value }} /> : null;
  return (
    <div
      ref={ref}
      className={`rich ed ${className ?? ""}`}
      contentEditable suppressContentEditableWarning spellCheck={false} data-placeholder="Type here"
      onClick={(e) => e.stopPropagation()}
      onPaste={(e) => { e.preventDefault(); document.execCommand("insertText", false, e.clipboardData.getData("text/plain")); }}
      onInput={(e) => { const html = (e.currentTarget as HTMLElement).innerHTML; itemId ? edit.setItemField(sectionId!, itemId, field, html) : edit.setProp(sectionId!, field, html); }}
    />
  );
}
