"use client";

import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { ChevronDown, getInitials } from "@alumni/ui";
import { getMyInstitutions, startInstitutionSwitch } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";

/**
 * "Switch institution": the other institutions this person's email belongs to, one tap each, without signing in again. Shown only
 * when there is at least one other, so for most members it simply is not there. Quiet while loading or on any error.
 */
export function InstitutionSwitcher() {
  const { data } = useQuery({ queryKey: ["m-my-institutions"], queryFn: getMyInstitutions, staleTime: 10 * 60_000, retry: false });
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);

  const go = useMutation({
    mutationFn: startInstitutionSwitch,
    onSuccess: (url) => { window.location.assign(url); },
    onError: (e) => toast.error(handleApiError(e)),
  });

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
    <div ref={root} className="relative mt-1.5 px-2">
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
        className="flex min-h-9 w-full items-center justify-between gap-2 rounded-lg border border-sidebar-border px-2.5 text-[12.5px] font-semibold text-muted-foreground transition-colors hover:bg-primary/5 hover:text-foreground"
      >
        <span>Switch institution</span>
        <ChevronDown size={12} className={open ? "rotate-180 transition-transform" : "transition-transform"} />
      </button>

      {open && (
        <div role="menu" aria-label="Your other institutions" className="absolute left-2 right-2 top-full z-30 mt-1 border border-border bg-card p-1 shadow-lg">
          {data.map((t) => (
            <button
              key={t.institutionId}
              type="button"
              role="menuitem"
              disabled={go.isPending}
              onClick={() => go.mutate(t.institutionId)}
              className="flex min-h-11 w-full items-center gap-2.5 px-2 py-1.5 text-left transition-colors hover:bg-muted disabled:opacity-60"
            >
              {t.logoUrl ? (
                <img src={t.logoUrl} alt="" className="size-7 shrink-0 rounded-md border border-border/40 object-cover" />
              ) : (
                <span className="flex size-7 shrink-0 items-center justify-center rounded-md bg-primary text-[11px] font-bold text-white" aria-hidden>{getInitials(t.name)}</span>
              )}
              <span className="min-w-0 truncate text-[13.5px] font-semibold">{t.name}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
