"use client";

import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown } from "@alumni/ui";
import { getMyInstitutions } from "@/lib/institution-api";

/**
 * Other institutions this administrator's email also administers, one click each. It opens that institution's own sign-in with
 * the email filled in; it does not skip the sign-in, because an administrator account can be created by someone else with a
 * password they chose, so a sign-in here proves nothing about the same email there. Hidden when there are none.
 */
export function InstitutionSwitcher() {
  const { data } = useQuery({ queryKey: ["staff-institutions"], queryFn: getMyInstitutions, staleTime: 10 * 60_000, retry: false });
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => { if (!root.current?.contains(e.target as Node)) setOpen(false); };
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => { document.removeEventListener("mousedown", onDown); document.removeEventListener("keydown", onKey); };
  }, [open]);

  if (!data || data.length === 0) return null;

  return (
    <div ref={root} className="relative mt-3">
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
        className="flex min-h-9 w-full items-center justify-between gap-2 rounded-md border border-white/10 px-2.5 text-[12.5px] font-semibold text-slate-300 transition-colors hover:bg-white/10 hover:text-white"
      >
        <span>Switch institution</span>
        <ChevronDown size={12} className={open ? "rotate-180 transition-transform" : "transition-transform"} />
      </button>

      {open && (
        <div role="menu" aria-label="Your other institutions" className="absolute left-0 right-0 top-full z-30 mt-1 border border-border bg-card p-1 text-foreground shadow-lg">
          {data.map((t) => (
            <a
              key={t.institutionId}
              role="menuitem"
              href={t.loginUrl}
              className="flex min-h-11 items-center gap-2.5 px-2 py-1.5 transition-colors hover:bg-muted"
            >
              {t.logoUrl ? (
                <img src={t.logoUrl} alt="" className="size-7 shrink-0 rounded-md border border-border/40 object-cover" />
              ) : (
                <span className="flex size-7 shrink-0 items-center justify-center rounded-md bg-primary text-[12px] font-bold text-white" aria-hidden>{t.name.charAt(0).toUpperCase()}</span>
              )}
              <span className="min-w-0 truncate text-[13.5px] font-semibold">{t.name}</span>
            </a>
          ))}
        </div>
      )}
    </div>
  );
}
