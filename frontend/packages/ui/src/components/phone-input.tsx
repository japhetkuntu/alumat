"use client";

import * as React from "react";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "./select";
import { Input } from "./input";
import { cn } from "../lib/utils";
import { COUNTRY_DIAL_CODES, COUNTRY_DIAL_CODES_BY_LENGTH, DEFAULT_COUNTRY_ISO2, flagEmoji } from "../lib/countries";

/**
 * Splits a stored/composed phone value (e.g. "+233241234567", or a legacy
 * unformatted value like "0241234567") into a country + national number, so
 * an existing value can be re-edited without the user having to re-pick
 * their country every time. Best-effort only — numbers saved before this
 * component existed may be in inconsistent formats (see PhoneInput's own
 * doc comment), so a failed match just falls back to the default country
 * with the raw digits as the national number rather than throwing.
 */
function parsePhoneValue(value: string | undefined): { iso2: string; national: string } {
  const digits = (value ?? "").replace(/[^\d+]/g, "");
  if (digits.startsWith("+")) {
    const withoutPlus = digits.slice(1);
    const match = COUNTRY_DIAL_CODES_BY_LENGTH.find((c) => withoutPlus.startsWith(c.dialCode));
    if (match) {
      return { iso2: match.iso2, national: withoutPlus.slice(match.dialCode.length) };
    }
  }
  return { iso2: DEFAULT_COUNTRY_ISO2, national: digits };
}

/** Strips everything but digits — kept as typed (leading "0" included) so the field doesn't visibly eat the user's first keystroke; the trunk "0" is dropped separately, only in composeValue's output. */
function digitsOnly(raw: string): string {
  return raw.replace(/\D/g, "");
}

/** Drops one leading trunk "0" — the digit typed out of habit for a local number ("024...") that shouldn't survive into the internationally-dialable form ("+23324..."). */
function composeValue(iso2: string, national: string): string {
  const trimmed = national.replace(/^0/, "");
  if (!trimmed) return "";
  const country = COUNTRY_DIAL_CODES.find((c) => c.iso2 === iso2);
  return `+${country?.dialCode ?? DEFAULT_COUNTRY_ISO2}${trimmed}`;
}

export interface PhoneInputProps {
  id?: string;
  /** The full composed value, e.g. "+233241234567" — same shape stored on Member.Phone. Empty string when no national number has been entered yet. */
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  placeholder?: string;
  error?: boolean;
  disabled?: boolean;
  className?: string;
}

/**
 * Country-code select + national-number input, composed into one E.164-style
 * value ("+<dialcode><nationalnumber>") on every change — this is what gets
 * sent to the SMS provider, so appending the right dial code here (rather
 * than trusting free-text entry) is what makes SMS delivery reliable.
 */
export function PhoneInput({ id, value, onChange, onBlur, placeholder, error, disabled, className }: PhoneInputProps) {
  const parsed = React.useMemo(() => parsePhoneValue(value), [value]);
  const [iso2, setIso2] = React.useState(parsed.iso2);
  const [national, setNational] = React.useState(parsed.national);

  // Re-sync from an externally-changed value (e.g. profile data finishing
  // its fetch after this component already mounted with an empty default).
  const lastExternalValue = React.useRef(value);
  React.useEffect(() => {
    if (value !== lastExternalValue.current) {
      lastExternalValue.current = value;
      const next = parsePhoneValue(value);
      setIso2(next.iso2);
      setNational(next.national);
    }
  }, [value]);

  function emit(nextIso2: string, nextNational: string) {
    const composed = composeValue(nextIso2, nextNational);
    lastExternalValue.current = composed;
    onChange(composed);
  }

  const selectedCountry = COUNTRY_DIAL_CODES.find((c) => c.iso2 === iso2);

  return (
    <div className={cn("flex gap-2", className)}>
      <Select
        value={iso2}
        onValueChange={(next) => {
          setIso2(next);
          emit(next, national);
        }}
        disabled={disabled}
      >
        <SelectTrigger className="w-[110px] min-w-[110px] shrink-0" error={error}>
          <SelectValue>
            <span className="flex items-center gap-1.5">
              <span>{flagEmoji(iso2)}</span>
              <span>+{selectedCountry?.dialCode}</span>
            </span>
          </SelectValue>
        </SelectTrigger>
        <SelectContent>
          {COUNTRY_DIAL_CODES.map((c) => (
            <SelectItem key={c.iso2} value={c.iso2}>
              <span className="flex items-center gap-2">
                <span>{flagEmoji(c.iso2)}</span>
                <span className="truncate">{c.name}</span>
                <span className="text-muted-foreground">+{c.dialCode}</span>
              </span>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Input
        id={id}
        type="tel"
        inputMode="tel"
        autoComplete="tel-national"
        placeholder={placeholder ?? "24 123 4567"}
        value={national}
        error={error}
        disabled={disabled}
        onChange={(e) => {
          const next = digitsOnly(e.target.value);
          setNational(next);
          emit(iso2, next);
        }}
        onBlur={onBlur}
        className="flex-1"
      />
    </div>
  );
}
