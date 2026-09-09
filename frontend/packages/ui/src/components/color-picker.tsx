"use client";

import * as React from "react";
import { cn } from "../lib/utils";
import { Input } from "./input";
import { Label } from "./label";

const HEX_RE = /^#[0-9a-fA-F]{6}$/;

function normalizeHex(raw: string): string | null {
  let v = raw.trim();
  if (!v) return null;
  if (!v.startsWith("#")) v = `#${v}`;
  if (/^#[0-9a-fA-F]{3}$/.test(v)) {
    v = `#${v[1]}${v[1]}${v[2]}${v[2]}${v[3]}${v[3]}`;
  }
  return HEX_RE.test(v) ? v.toUpperCase() : null;
}

export interface ColorPickerProps {
  label?: string;
  /** Current color as a 6-digit hex string, e.g. "#2563EB". */
  value: string;
  onChange: (hex: string) => void;
  /** A handful of quick-pick swatches shown below the field — brand colors from elsewhere on the platform, or none at all. */
  presets?: string[];
  placeholder?: string;
  helperText?: string;
  className?: string;
}

/**
 * A swatch (backed by the native OS color picker) paired with a typed hex
 * field — either one drives the other, so picking visually and pasting a
 * known hex both just work. The swatch itself is the click target: the
 * native `<input type="color">` is invisibly stacked on top of it rather
 * than shown as its own separate, ugly control.
 */
export function ColorPicker({ label, value, onChange, presets, placeholder = "#2563EB", helperText, className }: ColorPickerProps) {
  const [draft, setDraft] = React.useState(value);
  const [invalid, setInvalid] = React.useState(false);
  // Nothing chosen yet is a real, distinct state — worth showing as visibly
  // empty (not a filled-in color) so it's never mistaken for a deliberate
  // pick. A caller substituting some other hex as a "default" here would
  // recreate exactly that confusion, so this component owns the empty look
  // itself rather than leaving it to callers to fake a placeholder color.
  const isSet = normalizeHex(value) !== null;
  const swatchColor = normalizeHex(value) ?? "#FFFFFF";

  React.useEffect(() => {
    setDraft(value);
    setInvalid(false);
  }, [value]);

  function commit(raw: string) {
    const normalized = normalizeHex(raw);
    if (normalized) {
      setInvalid(false);
      onChange(normalized);
    } else {
      setInvalid(true);
    }
  }

  return (
    <div className={cn("space-y-1.5", className)}>
      {label && <Label>{label}</Label>}
      <div className="flex items-center gap-2">
        <span
          className={cn(
            "relative shrink-0 w-11 h-11 overflow-hidden cursor-pointer transition-transform duration-150 hover:scale-105 active:scale-95",
            isSet ? "border border-input" : "border border-dashed border-muted-foreground/50"
          )}
          style={{
            background: isSet
              ? swatchColor
              : "repeating-linear-gradient(45deg, transparent, transparent 4px, var(--muted) 4px, var(--muted) 8px)",
          }}
          title={isSet ? swatchColor : "Not set"}
        >
          <input
            type="color"
            value={swatchColor}
            onChange={(e) => {
              setDraft(e.target.value.toUpperCase());
              onChange(e.target.value.toUpperCase());
            }}
            className="absolute -top-2 -left-2 w-[calc(100%+16px)] h-[calc(100%+16px)] cursor-pointer opacity-0"
            aria-label={label ? `${label} color picker` : "Color picker"}
          />
        </span>
        <Input
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onBlur={() => commit(draft)}
          onKeyDown={(e) => e.key === "Enter" && commit(draft)}
          placeholder={isSet ? placeholder : "Not set"}
          error={invalid}
          className="w-[140px] font-mono uppercase tracking-wide"
          maxLength={7}
        />
      </div>
      {invalid && <p className="text-[11.5px] text-destructive">Enter a valid hex color, like #2563EB.</p>}
      {!invalid && !isSet && <p className="text-[11.5px] text-muted-foreground">Click the swatch or type a hex to set this color.</p>}
      {!invalid && isSet && helperText && <p className="text-[11.5px] text-muted-foreground">{helperText}</p>}
      {presets && presets.length > 0 && (
        <div className="flex items-center gap-1.5 pt-0.5">
          {presets.map((p) => {
            const hex = normalizeHex(p);
            if (!hex) return null;
            const active = hex === swatchColor;
            return (
              <button
                key={hex}
                type="button"
                onClick={() => { setDraft(hex); onChange(hex); }}
                title={hex}
                className={cn(
                  "w-5 h-5 border transition-transform duration-150 hover:scale-110",
                  active ? "border-foreground ring-2 ring-offset-1 ring-ring/40" : "border-input"
                )}
                style={{ background: hex }}
              />
            );
          })}
        </div>
      )}
    </div>
  );
}
