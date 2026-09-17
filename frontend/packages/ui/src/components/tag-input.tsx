"use client";

import * as React from "react";
import { X } from "./icons";
import { cn } from "../lib/utils";

export interface TagInputProps {
  id?: string;
  value: string[];
  onChange: (value: string[]) => void;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
}

/**
 * Free-text multi-value input (e.g. clubs, leadership roles): type a value,
 * press Enter/comma to add it as a tag, Backspace on an empty field removes
 * the last one. Kept intentionally simple — no autocomplete/suggestions —
 * since these are member-entered, open-ended lists with no fixed source list.
 */
export function TagInput({ id, value, onChange, placeholder, disabled, className }: TagInputProps) {
  const [draft, setDraft] = React.useState("");

  function commitDraft() {
    const trimmed = draft.trim();
    if (trimmed && !value.includes(trimmed)) onChange([...value, trimmed]);
    setDraft("");
  }

  function removeAt(index: number) {
    onChange(value.filter((_, i) => i !== index));
  }

  return (
    <div
      className={cn(
        "flex flex-wrap items-center gap-1.5 min-h-11 w-full border border-input bg-background px-2.5 py-1.5 transition-all duration-150",
        "focus-within:border-ring focus-within:ring-2 focus-within:ring-ring/25",
        disabled && "cursor-not-allowed opacity-55 bg-muted/40",
        className
      )}
    >
      {value.map((tag, i) => (
        <span
          key={`${tag}-${i}`}
          className="inline-flex items-center gap-1 border border-accent/40 bg-accent-50 text-accent-800 px-2 py-0.5 text-[12px] font-medium"
        >
          {tag}
          {!disabled && (
            <button
              type="button"
              onClick={() => removeAt(i)}
              className="text-accent-800/60 hover:text-accent-800"
              aria-label={`Remove ${tag}`}
            >
              <X size={11} />
            </button>
          )}
        </span>
      ))}
      <input
        id={id}
        type="text"
        value={draft}
        disabled={disabled}
        placeholder={value.length === 0 ? placeholder : undefined}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter" || e.key === ",") {
            e.preventDefault();
            commitDraft();
          } else if (e.key === "Backspace" && draft === "" && value.length > 0) {
            removeAt(value.length - 1);
          }
        }}
        onBlur={commitDraft}
        className="flex-1 min-w-[100px] bg-transparent text-[16px] md:text-[14px] text-foreground placeholder:text-muted-foreground/65 focus:outline-none disabled:cursor-not-allowed"
      />
    </div>
  );
}
