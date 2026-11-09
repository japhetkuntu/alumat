"use client";

import dynamic from "next/dynamic";
import { memo, useEffect, useRef, type ReactNode } from "react";
import { ConfirmModal, DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger, Label, LoadError, MoreHorizontal, cn } from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";

/** The editor is a large library, so it is fetched only when a form that needs it opens, and the form shows a placeholder meanwhile. */
const Editor = dynamic(() => import("@alumni/ui").then((m) => ({ default: m.RichTextEditor })), {
  ssr: false,
  loading: () => <div className="h-[190px] animate-pulse border border-border bg-muted/50" aria-busy="true" />,
});

/** A labelled rich-text field, set up the way the institution's forms set theirs up. */
export function RichField({ id, label, optional, value, onChange, placeholder, minHeight = 140 }: {
  id: string; label: string; optional?: boolean; value: string; onChange: (v: string) => void; placeholder?: string; minHeight?: number;
}) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}{optional && <span className="font-normal text-muted-foreground"> (optional)</span>}</Label>
      <Editor id={id} variant="compact" placeholder={placeholder} minHeight={minHeight} value={value} onChange={onChange} />
    </div>
  );
}

/** A plain labelled field wrapper, so every form lays its fields out the same way. */
export function Field({ id, label, optional, hint, children }: { id: string; label: string; optional?: boolean; hint?: string; children: ReactNode }) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}{optional && <span className="font-normal text-muted-foreground"> (optional)</span>}</Label>
      {children}
      {hint && <p className="text-[12.5px] leading-relaxed text-muted-foreground">{hint}</p>}
    </div>
  );
}

/** The message from a failed request, or nothing. */
export const errorText = (e: unknown): string | null => (e ? handleApiError(e) : null);

/**
 * One section of the Manage page. The tab already names it, so the heading is kept for screen readers only and the visible row holds
 * what is useful: a short summary on the left and the main action on the right. On a phone the content sits straight on the page; from
 * a tablet up it gets a card.
 */
export function Panel({ id, title, summary, action, children }: { id: string; title: string; summary?: ReactNode; action?: ReactNode; children: ReactNode }) {
  return (
    <section aria-labelledby={id} className="sm:rounded-[18px] sm:border sm:border-border sm:bg-background sm:p-5">
      <h2 id={id} className="sr-only">{title}</h2>
      {(summary || action) && (
        <div className="flex min-h-10 items-center justify-between gap-3">
          <p className="min-w-0 truncate text-[13px] font-medium text-muted-foreground">{summary}</p>
          {action}
        </div>
      )}
      {children}
    </section>
  );
}

/** What a section looks like while its code and data arrive: the same shape as the real thing, so nothing jumps. */
export function SectionSkeleton() {
  return (
    <div className="sm:rounded-[18px] sm:border sm:border-border sm:bg-background sm:p-5" aria-busy="true" aria-label="Loading">
      <div className="h-5 w-32 animate-pulse rounded bg-muted" />
      <div className="mt-4 space-y-3">{[0, 1, 2].map((i) => <div key={i} className="h-14 animate-pulse rounded-[12px] bg-muted/60" />)}</div>
    </div>
  );
}

/** The ways to change one item, kept behind a menu so a row reads as content first. Nothing at all when none apply. */
export const ItemMenu = memo(function ItemMenu({ label, onEdit, onRemove, removeLabel }: { label: string; onEdit?: () => void; onRemove?: () => void; removeLabel: string }) {
  if (!onEdit && !onRemove) return null;
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button type="button" aria-label={`Options for ${label}`} className="grid size-10 shrink-0 place-items-center rounded-full text-muted-foreground transition hover:bg-muted hover:text-foreground sm:size-9"><MoreHorizontal size={16} /></button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-[180px]">
        {onEdit && <DropdownMenuItem onSelect={onEdit}>Edit</DropdownMenuItem>}
        {onEdit && onRemove && <DropdownMenuSeparator />}
        {onRemove && <DropdownMenuItem destructive onSelect={onRemove}>{removeLabel}</DropdownMenuItem>}
      </DropdownMenuContent>
    </DropdownMenu>
  );
});

/** Loading, failed, empty or the list: one place so every section behaves the same. */
export function ListState({ loading, error, onRetry, empty, emptyText, children }: { loading: boolean; error: boolean; onRetry: () => void; empty: boolean; emptyText: string; children: ReactNode }) {
  if (loading) return <div className="mt-3 space-y-2" aria-busy="true">{[0, 1].map((i) => <div key={i} className="h-16 animate-pulse rounded-[12px] bg-muted/60" />)}</div>;
  if (error) return <LoadError onRetry={onRetry} />;
  if (empty) return <p className="mt-3 text-[13.5px] leading-relaxed text-muted-foreground">{emptyText}</p>;
  return <>{children}</>;
}

/** A long list's rows are only laid out when near the screen, so a list of two hundred scrolls as lightly as a list of ten. */
export const ROW = "[content-visibility:auto] [contain-intrinsic-size:auto_68px]";

/** Choose one of a few words, as pills: quicker than a dropdown for three or four options. */
export function Choice({ value, options, onChange, label }: { value: string; options: string[]; onChange: (v: string) => void; label: string }) {
  return (
    <div role="radiogroup" aria-label={label} className="flex flex-wrap gap-2">
      {options.map((o) => (
        <button key={o} type="button" role="radio" aria-checked={value === o} onClick={() => onChange(o)}
          className={cn("min-h-10 rounded-full px-4 text-[13px] font-medium ring-1 transition", value === o ? "bg-primary text-primary-foreground ring-primary" : "text-muted-foreground ring-border hover:bg-muted hover:text-foreground")}>{o}</button>
      ))}
    </div>
  );
}

/** Asks before something that cannot be undone. */
export function Confirm({ title, message, confirmLabel, cancelLabel = "Keep it", busy, onConfirm, onCancel }: {
  title: string; message: string; confirmLabel: string; cancelLabel?: string; busy?: boolean; onConfirm: () => void; onCancel: () => void;
}) {
  return <ConfirmModal open title={title} message={message} confirmLabel={confirmLabel} cancelLabel={cancelLabel} isLoading={busy} onConfirm={onConfirm} onCancel={onCancel} />;
}

/**
 * The page's sections. On a phone and a tablet it is a row of pills that scrolls sideways; from a laptop up it becomes a side menu that
 * stays in view while the section scrolls. `onIntent` fires when someone hovers or focuses a tab, so its code and data can start loading
 * before the tap.
 */
export function SectionNav<T extends string>({ tabs, value, onChange, onIntent }: {
  tabs: { id: T; label: string }[]; value: T; onChange: (id: T) => void; onIntent?: (id: T) => void;
}) {
  const row = useRef<HTMLDivElement>(null);
  // A deep link or a swipe can pick a tab that is off the edge of the row: bring it into view.
  useEffect(() => {
    const el = row.current?.querySelector<HTMLElement>('[aria-selected="true"]');
    el?.scrollIntoView({ inline: "center", block: "nearest" });
  }, [value]);
  const props = (t: { id: T }) => ({
    type: "button" as const, role: "tab" as const, "aria-selected": value === t.id,
    onClick: () => onChange(t.id), onPointerEnter: () => onIntent?.(t.id), onFocus: () => onIntent?.(t.id), onTouchStart: () => onIntent?.(t.id),
  });
  return (
    <>
      <div ref={row} role="tablist" className="-mx-4 flex gap-1.5 overflow-x-auto px-4 pb-1 sm:-mx-6 sm:px-6 lg:hidden [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {tabs.map((t) => (
          <button key={t.id} {...props(t)}
            className={cn("min-h-10 shrink-0 rounded-full px-4 text-[13.5px] font-medium transition", value === t.id ? "bg-foreground text-background" : "text-muted-foreground ring-1 ring-border hover:bg-muted hover:text-foreground")}>
            {t.label}
          </button>
        ))}
      </div>
      <nav aria-label="Sections" role="tablist" aria-orientation="vertical" className="sticky top-6 hidden flex-col gap-0.5 lg:flex">
        {tabs.map((t) => (
          <button key={t.id} {...props(t)}
            className={cn("rounded-[12px] px-3.5 py-2.5 text-left text-[14px] font-medium transition", value === t.id ? "bg-foreground text-background" : "text-muted-foreground hover:bg-muted hover:text-foreground")}>
            {t.label}
          </button>
        ))}
      </nav>
    </>
  );
}
