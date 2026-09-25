"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import {
  Briefcase, Calendar, FolderOpen, Megaphone, Newspaper, Search, Users, ArrowRight,
} from "@alumni/ui";
import { Dialog, DialogContent, DialogTitle, DialogDescription } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { getCampaigns, getEvents, getJobs, getMembers, getNewsPosts, getResources } from "@/lib/institution-api";

interface SearchPage { href: string; label: string }
interface Hit { key: string; href: string; title: string; subtitle?: string }
interface Group { label: string; icon: React.ElementType; hits: Hit[]; loading: boolean }

/** Waits for typing to pause before searching, so each keystroke doesn't fire a round of requests. */
function useDebounced<T>(value: T, ms = 250): T {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}

const MIN_CHARS = 2;

/** True when every word typed appears somewhere in the text, in any order, ignoring case and extra spaces. */
function hasAllWords(text: string, typed: string): boolean {
  const haystack = text.toLowerCase();
  return typed.split(/\s+/).filter(Boolean).every((w) => haystack.includes(w));
}

/**
 * One search box for the whole staff portal. Opens from the magnifier in the header or with Ctrl/Cmd+K.
 * It looks in the menu pages, then in members, fundraisers, events, jobs, news and resources at the same time, and shows whatever answers. Nothing here is a new endpoint: each
 * section uses the same request its own page already makes.
 */
export function GlobalSearch({ pages, hotkey = false }: { pages: SearchPage[]; hotkey?: boolean }) {
  const [open, setOpen] = useState(false);
  const [term, setTerm] = useState("");
  const q = useDebounced(term.trim());
  const active = open && q.length >= MIN_CHARS;

  useEffect(() => {
    if (!hotkey) return;
    function onKey(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setOpen((o) => !o);
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [hotkey]);

  const has = (href: string) => pages.some((p) => p.href === href);
  const opts = { enabled: active, staleTime: 30_000, retry: 0 } as const;
  const members = useQuery({ queryKey: ["gs-members", q], queryFn: () => getMembers({ page: 1, pageSize: 5, search: q }), ...opts, enabled: active && has("/members") });
  const jobs = useQuery({ queryKey: ["gs-jobs", q], queryFn: () => getJobs(1, 5, q), ...opts, enabled: active && has("/jobs") });
  const news = useQuery({ queryKey: ["gs-news", q], queryFn: () => getNewsPosts(1, 5, q), ...opts, enabled: active && has("/news") });
  const resources = useQuery({ queryKey: ["gs-resources", q], queryFn: () => getResources(1, 5, undefined, q), ...opts, enabled: active && has("/resources") });
  // Fundraisers and events have no server-side search, so fetch one page once and filter it here.
  const campaigns = useQuery({ queryKey: ["gs-campaigns"], queryFn: () => getCampaigns(1, 50), enabled: open && has("/campaigns"), staleTime: 5 * 60_000, retry: 0 });
  const events = useQuery({ queryKey: ["gs-events"], queryFn: () => getEvents(1, 50), enabled: open && has("/events"), staleTime: 5 * 60_000, retry: 0 });

  const groups: Group[] = useMemo(() => {
    const lc = q.toLowerCase();
    const pageHits: Hit[] = pages
      .filter((p) => hasAllWords(p.label, lc))
      .slice(0, 4)
      .map((p) => ({ key: p.href, href: p.href, title: p.label }));
    const eventHits: Hit[] = (events.data?.results ?? [])
      .filter((e) => hasAllWords(`${e.title} ${e.venue ?? ""}`, lc))
      .slice(0, 5)
      .map((e) => ({ key: e.id, href: "/events", title: e.title, subtitle: e.venue ?? undefined }));
    const campaignHits: Hit[] = (campaigns.data?.results ?? [])
      .filter((c) => hasAllWords(c.title, lc))
      .slice(0, 5)
      .map((c) => ({ key: c.id, href: `/campaigns/${c.id}`, title: c.title, subtitle: c.isMembershipCampaign ? "Dues" : "Fundraiser" }));
    return [
      { label: "Pages", icon: ArrowRight, hits: pageHits, loading: false },
      { label: "Members", icon: Users, loading: members.isFetching, hits: (members.data?.results ?? []).map((m) => ({ key: m.id, href: `/members/${m.id}`, title: `${m.firstName} ${m.lastName}`, subtitle: [m.email, m.graduationYear ? `Cohort ${m.graduationYear}` : null].filter(Boolean).join(" · ") })) },
      { label: "Fundraisers & dues", icon: Megaphone, loading: campaigns.isFetching, hits: campaignHits },
      { label: "Events", icon: Calendar, loading: events.isFetching, hits: eventHits },
      { label: "Jobs", icon: Briefcase, loading: jobs.isFetching, hits: (jobs.data?.results ?? []).map((j) => ({ key: j.id, href: `/jobs/${j.id}`, title: j.title, subtitle: `${j.company} · ${j.location}` })) },
      { label: "News", icon: Newspaper, loading: news.isFetching, hits: (news.data?.results ?? []).map((n) => ({ key: n.id, href: `/news/${n.id}`, title: n.title, subtitle: n.status })) },
      { label: "Resources", icon: FolderOpen, loading: resources.isFetching, hits: (resources.data?.results ?? []).map((r) => ({ key: r.id, href: `/resources/${r.id}`, title: r.title, subtitle: r.category })) },
    ];
  }, [q, pages, events.data, events.isFetching, campaigns.data, campaigns.isFetching, members.data, members.isFetching, jobs.data, jobs.isFetching, news.data, news.isFetching, resources.data, resources.isFetching]);

  const shown = groups.filter((g) => g.hits.length > 0);
  const busy = groups.some((g) => g.loading);

  function close() {
    setOpen(false);
    setTerm("");
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label="Search the portal"
        className="inline-flex h-10 w-10 items-center justify-center text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
      >
        <Search size={18} />
      </button>

      <Dialog open={open} onOpenChange={(v) => (v ? setOpen(true) : close())}>
        <DialogContent className="top-[4%] max-h-[90dvh] translate-y-0 gap-0 overflow-hidden p-0 sm:top-[8%] sm:max-w-xl">
          <DialogTitle className="sr-only">Search the portal</DialogTitle>
          <DialogDescription className="sr-only">Find pages, members, fundraisers, events, jobs, news and resources.</DialogDescription>
          <div className="flex items-center gap-2 border-b border-border pl-3 pr-14">
            <Search size={16} className="shrink-0 text-muted-foreground" />
            <input
              autoFocus
              value={term}
              onChange={(e) => setTerm(e.target.value)}
              placeholder="Search members, fundraisers, events…"
              aria-label="Search the portal"
              className="h-12 min-h-0 w-full !border-0 bg-transparent text-[16px] !shadow-none !outline-none !ring-0 placeholder:text-muted-foreground/70 focus:!border-0 focus:!shadow-none md:text-[14px]"
            />
          </div>

          <div className="max-h-[calc(90dvh-3.5rem)] overflow-y-auto overscroll-contain">
            {q.length < MIN_CHARS ? (
              <div className="px-4 py-6">
                <p className="text-[13px] font-semibold text-foreground">Find anything in one place</p>
                <p className="mt-1 text-[12.5px] leading-relaxed text-muted-foreground">
                  Type at least two letters to search members, fundraisers, events, jobs, news and resources. Or jump straight to a page:
                </p>
                <div className="mt-3 flex flex-wrap gap-2">
                  {pages.slice(0, 8).map((p) => (
                    <Link key={p.href} href={p.href} onClick={close} className="border border-border px-3 py-1.5 text-[12.5px] font-semibold text-muted-foreground hover:border-primary/40 hover:text-foreground">
                      {p.label}
                    </Link>
                  ))}
                </div>
              </div>
            ) : shown.length === 0 && !busy ? (
              <div className="px-4 py-8 text-center">
                <p className="text-[13.5px] font-semibold text-foreground">Nothing found for &ldquo;{q}&rdquo;</p>
                <p className="mt-1 text-[12.5px] text-muted-foreground">Check the spelling, or try a shorter or more general word.</p>
              </div>
            ) : (
              <div className="py-2" aria-live="polite">
                {shown.map((g) => (
                  <section key={g.label} className="py-1">
                    <p className="flex items-center gap-2 px-4 py-1.5 text-[11px] font-bold uppercase tracking-[.08em] text-muted-foreground">
                      <g.icon size={12} />
                      {g.label}
                    </p>
                    <ul>
                      {g.hits.map((h) => (
                        <li key={h.key}>
                          <Link
                            href={h.href}
                            onClick={close}
                            className={cn("flex flex-col gap-0.5 px-4 py-2.5 transition-colors hover:bg-muted focus-visible:bg-muted focus-visible:outline-none")}
                          >
                            <span className="text-[13.5px] font-semibold leading-snug text-foreground">{h.title}</span>
                            {h.subtitle && <span className="text-[12px] text-muted-foreground">{h.subtitle}</span>}
                          </Link>
                        </li>
                      ))}
                    </ul>
                  </section>
                ))}
                {busy && <p className="px-4 py-2 text-[12px] text-muted-foreground">Still searching…</p>}
              </div>
            )}
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
