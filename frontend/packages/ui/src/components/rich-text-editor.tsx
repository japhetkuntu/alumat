"use client";

import { useEditor, EditorContent, type Editor } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import Underline from "@tiptap/extension-underline";
import Link from "@tiptap/extension-link";
import TextAlign from "@tiptap/extension-text-align";
import Placeholder from "@tiptap/extension-placeholder";
import {
  Bold,
  Italic,
  Underline as UnderlineIcon,
  Strikethrough,
  List,
  ListOrdered,
  Quote,
  Undo,
  Redo,
  Link as LinkIcon,
  AlignLeft,
  AlignCenter,
  AlignRight,
  Heading1,
  Heading2,
  Heading3,
  Minus,
  Code,
  X,
} from "./icons";
import { Button } from "./button";
import { cn } from "../lib/utils";
import { useEffect, useState } from "react";
import { RICH_TEXT_STYLES, RichText } from "./rich-text";
import { toEditorHtml, richTextToPlain } from "../lib/rich-text-utils";

export type RichTextEditorVariant = "full" | "compact";

interface RichTextEditorProps {
  /** HTML from the editor, or plain text saved before it existed (loaded as paragraphs). */
  value: string;
  /** The HTML, or "" when the editor is empty. */
  onChange: (html: string) => void;
  placeholder?: string;
  className?: string;
  /** "compact" is the short toolbar for descriptions; "full" adds the extras (title sizes, strikethrough, alignment, divider, code). */
  variant?: RichTextEditorVariant;
  /** The writing area's minimum height in pixels. */
  minHeight?: number;
  /** Lets a <Label htmlFor> point at the writing area. */
  id?: string;
  "aria-label"?: string;
  /** Where to warn that the text is getting long. Defaults to the server's limit. */
  maxChars?: number;
}

function ToolbarButton({
  onClick,
  active,
  disabled,
  children,
  title,
}: {
  onClick: () => void;
  active?: boolean;
  disabled?: boolean;
  children: React.ReactNode;
  title: string;
}) {
  return (
    <Button
      type="button"
      size="icon"
      variant="ghost"
      className={cn(
        "h-9 w-9 shrink-0 sm:h-8 sm:w-8",
        active && "bg-muted text-foreground"
      )}
      onClick={onClick}
      disabled={disabled}
      title={title}
      aria-label={title}
    >
      {children}
    </Button>
  );
}

function Divider() {
  return <div className="mx-1 h-5 w-px shrink-0 bg-border" aria-hidden />;
}

/** Turns what someone typed into a link address we accept, or null. "example.com" becomes https://example.com. */
function normalizeUrl(raw: string): string | null {
  const v = raw.trim();
  if (!v) return null;
  if (/^(https?:\/\/|mailto:|tel:)/i.test(v)) return v;
  if (/^[a-z][a-z0-9+.-]*:/i.test(v)) return null;          // javascript:, data:, anything else with a scheme
  if (/\s/.test(v) || !/[.]/.test(v)) return null;
  return `https://${v}`;
}

function LinkBar({ editor, onClose }: { editor: Editor; onClose: () => void }) {
  const current = (editor.getAttributes("link").href as string | undefined) ?? "";
  const [url, setUrl] = useState(current);
  const hasSelection = !editor.state.selection.empty;
  const isLink = editor.isActive("link");
  const normalized = normalizeUrl(url);

  const apply = () => {
    if (!normalized) return;
    editor.chain().focus().extendMarkRange("link").setLink({ href: normalized }).run();
    onClose();
  };
  const remove = () => {
    editor.chain().focus().extendMarkRange("link").unsetLink().run();
    onClose();
  };

  return (
    <div className="flex flex-wrap items-center gap-2 border-b border-border bg-muted/20 px-2 py-2">
      {!hasSelection && !isLink ? (
        <p className="flex-1 text-[13px] text-muted-foreground">Select the words you want to turn into a link, then tap the link button again.</p>
      ) : (
        <>
          <input
            type="url"
            inputMode="url"
            autoFocus
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); apply(); } if (e.key === "Escape") onClose(); }}
            placeholder="Paste or type a web address"
            aria-label="Link address"
            className="h-9 min-w-[180px] flex-1 border border-input bg-background px-2.5 text-[14px] outline-none focus:border-primary"
          />
          <Button type="button" size="sm" onClick={apply} disabled={!normalized}>{isLink ? "Update" : "Add link"}</Button>
          {isLink && <Button type="button" size="sm" variant="outline" onClick={remove}>Remove link</Button>}
        </>
      )}
      <Button type="button" size="icon" variant="ghost" className="h-9 w-9 shrink-0" onClick={onClose} aria-label="Close link box"><X size={14} /></Button>
    </div>
  );
}

function Toolbar({ editor, variant, linkOpen, onToggleLink }: { editor: Editor; variant: RichTextEditorVariant; linkOpen: boolean; onToggleLink: () => void }) {
  const full = variant === "full";
  return (
    <div role="toolbar" aria-label="Formatting" className="flex items-center gap-0.5 overflow-x-auto border-b border-border bg-muted/30 px-2 py-1.5 [scrollbar-width:thin]">
      {full ? (
        <>
          <ToolbarButton onClick={() => editor.chain().focus().toggleHeading({ level: 1 }).run()} active={editor.isActive("heading", { level: 1 })} title="Heading 1"><Heading1 size={14} /></ToolbarButton>
          <ToolbarButton onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()} active={editor.isActive("heading", { level: 2 })} title="Heading 2"><Heading2 size={14} /></ToolbarButton>
          <ToolbarButton onClick={() => editor.chain().focus().toggleHeading({ level: 3 }).run()} active={editor.isActive("heading", { level: 3 })} title="Heading 3"><Heading3 size={14} /></ToolbarButton>
        </>
      ) : (
        <>
          <ToolbarButton onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()} active={editor.isActive("heading", { level: 2 })} title="Heading"><Heading2 size={14} /></ToolbarButton>
          <ToolbarButton onClick={() => editor.chain().focus().toggleHeading({ level: 3 }).run()} active={editor.isActive("heading", { level: 3 })} title="Subheading"><Heading3 size={14} /></ToolbarButton>
        </>
      )}
      <Divider />
      <ToolbarButton onClick={() => editor.chain().focus().toggleBold().run()} active={editor.isActive("bold")} title="Bold"><Bold size={14} /></ToolbarButton>
      <ToolbarButton onClick={() => editor.chain().focus().toggleItalic().run()} active={editor.isActive("italic")} title="Italic"><Italic size={14} /></ToolbarButton>
      <ToolbarButton onClick={() => editor.chain().focus().toggleUnderline().run()} active={editor.isActive("underline")} title="Underline"><UnderlineIcon size={14} /></ToolbarButton>
      {full && (
        <>
          <ToolbarButton onClick={() => editor.chain().focus().toggleStrike().run()} active={editor.isActive("strike")} title="Strikethrough"><Strikethrough size={14} /></ToolbarButton>
          <ToolbarButton onClick={() => editor.chain().focus().toggleCode().run()} active={editor.isActive("code")} title="Inline code"><Code size={14} /></ToolbarButton>
        </>
      )}
      <Divider />
      <ToolbarButton onClick={() => editor.chain().focus().toggleBulletList().run()} active={editor.isActive("bulletList")} title="Bullet list"><List size={14} /></ToolbarButton>
      <ToolbarButton onClick={() => editor.chain().focus().toggleOrderedList().run()} active={editor.isActive("orderedList")} title="Numbered list"><ListOrdered size={14} /></ToolbarButton>
      <ToolbarButton onClick={() => editor.chain().focus().toggleBlockquote().run()} active={editor.isActive("blockquote")} title="Quote"><Quote size={14} /></ToolbarButton>
      {full && (
        <>
          <ToolbarButton onClick={() => editor.chain().focus().setHorizontalRule().run()} title="Divider"><Minus size={14} /></ToolbarButton>
          <Divider />
          <ToolbarButton onClick={() => editor.chain().focus().setTextAlign("left").run()} active={editor.isActive({ textAlign: "left" })} title="Align left"><AlignLeft size={14} /></ToolbarButton>
          <ToolbarButton onClick={() => editor.chain().focus().setTextAlign("center").run()} active={editor.isActive({ textAlign: "center" })} title="Align center"><AlignCenter size={14} /></ToolbarButton>
          <ToolbarButton onClick={() => editor.chain().focus().setTextAlign("right").run()} active={editor.isActive({ textAlign: "right" })} title="Align right"><AlignRight size={14} /></ToolbarButton>
        </>
      )}
      <Divider />
      <ToolbarButton onClick={onToggleLink} active={linkOpen || editor.isActive("link")} title="Link"><LinkIcon size={14} /></ToolbarButton>
      <Divider />
      <ToolbarButton onClick={() => editor.chain().focus().undo().run()} disabled={!editor.can().undo()} title="Undo"><Undo size={14} /></ToolbarButton>
      <ToolbarButton onClick={() => editor.chain().focus().redo().run()} disabled={!editor.can().redo()} title="Redo"><Redo size={14} /></ToolbarButton>
    </div>
  );
}

export function RichTextEditor({
  value,
  onChange,
  placeholder = "Write something...",
  className,
  variant = "full",
  minHeight = 160,
  id,
  "aria-label": ariaLabel,
  maxChars = 20_000,
}: RichTextEditorProps) {
  const [linkOpen, setLinkOpen] = useState(false);
  const [length, setLength] = useState(() => richTextToPlain(value).length);

  const editor = useEditor({
    immediatelyRender: false,
    extensions: [
      StarterKit.configure({ heading: { levels: [1, 2, 3] }, link: false, underline: false }),
      Underline,
      Link.configure({
        openOnClick: false,
        autolink: true,
        HTMLAttributes: { class: "text-primary underline cursor-pointer" },
      }),
      TextAlign.configure({ types: ["heading", "paragraph"] }),
      Placeholder.configure({ placeholder }),
    ],
    content: toEditorHtml(value),
    onUpdate: ({ editor }) => {
      setLength(editor.getText().length);
      // An empty editor is "", not "<p></p>", so "nothing written" is the same everywhere.
      onChange(editor.isEmpty ? "" : editor.getHTML());
    },
    editorProps: {
      attributes: {
        ...(id ? { id } : {}),
        role: "textbox",
        "aria-multiline": "true",
        "aria-label": ariaLabel ?? "Description",
        class: cn(
          "max-w-none px-3.5 py-3 text-[15px] leading-relaxed focus:outline-none",
          // The hint shown while the editor is empty (the Placeholder extension marks the empty paragraph and carries the words).
          "[&_.is-editor-empty::before]:pointer-events-none [&_.is-editor-empty::before]:float-left [&_.is-editor-empty::before]:h-0 [&_.is-editor-empty::before]:text-muted-foreground/70 [&_.is-editor-empty::before]:content-[attr(data-placeholder)]",
          RICH_TEXT_STYLES,
        ),
        style: `min-height:${minHeight}px`,
      },
    },
  });

  // Follow the value when it is changed from outside (opening a different item to edit, resetting a form).
  useEffect(() => {
    if (!editor) return;
    const next = toEditorHtml(value);
    if (next === "" ? !editor.isEmpty : next !== editor.getHTML()) {
      editor.commands.setContent(next, { emitUpdate: false });
    }
  }, [value, editor]);

  if (!editor) {
    // Keep the space while the editor starts up, so the form does not jump.
    return <div className={cn("border border-input bg-background", className)} style={{ minHeight: minHeight + 46 }} aria-busy="true" />;
  }

  const nearLimit = length > maxChars * 0.9;

  return (
    <div className={cn("overflow-hidden border border-input bg-background transition-colors focus-within:border-primary", className)}>
      <Toolbar editor={editor} variant={variant} linkOpen={linkOpen} onToggleLink={() => setLinkOpen((o) => !o)} />
      {linkOpen && <LinkBar key={String(editor.getAttributes("link").href ?? "")} editor={editor} onClose={() => setLinkOpen(false)} />}
      <EditorContent editor={editor} />
      {nearLimit && (
        <p className={cn("border-t border-border px-3.5 py-1.5 text-right text-[12px]", length > maxChars ? "font-semibold text-destructive" : "text-muted-foreground")}>
          {length > maxChars ? `${(length - maxChars).toLocaleString()} characters over the limit` : `${(maxChars - length).toLocaleString()} characters left`}
        </p>
      )}
    </div>
  );
}

/** Shows formatted text (kept for existing callers). Cleaned before it is shown; see RichText. */
export function RichTextViewer({ content, className }: { content: string; className?: string }) {
  return <RichText content={content} className={className} />;
}
