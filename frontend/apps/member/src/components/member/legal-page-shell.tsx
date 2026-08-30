"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { ArrowLeft, GraduationCap, List, X } from "lucide-react";

function slugify(heading: string): string {
  return heading
    .replace(/^\d+\.\s*/, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

interface TocEntry {
  id: string;
  label: string;
}

/** Sticky, scroll-spied "On this page" nav — built by reading the rendered
 * <h2> headings straight out of the content, so it can never drift out of
 * sync with the actual sections a Terms/Privacy page defines. */
function useToc(contentRef: React.RefObject<HTMLDivElement | null>) {
  const [toc, setToc] = useState<TocEntry[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);

  useEffect(() => {
    const root = contentRef.current;
    if (!root) return;
    const headings = Array.from(root.querySelectorAll<HTMLHeadingElement>("h2[id]"));
    setToc(headings.map((h) => ({ id: h.id, label: h.textContent ?? "" })));

    const obs = new IntersectionObserver(
      (entries) => {
        const visible = entries.filter((e) => e.isIntersecting).sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
        if (visible[0]) setActiveId(visible[0].target.id);
      },
      { rootMargin: "-15% 0px -70% 0px", threshold: 0 }
    );
    headings.forEach((h) => obs.observe(h));
    return () => obs.disconnect();
  }, [contentRef]);

  return { toc, activeId };
}

function TocLink({ entry, active, onClick }: { entry: TocEntry; active: boolean; onClick?: () => void }) {
  return (
    <a
      href={`#${entry.id}`}
      onClick={onClick}
      className="block py-1.5 text-[13px] leading-snug transition-colors"
      style={{
        color: active ? "var(--primary)" : "var(--muted-foreground)",
        fontWeight: active ? 600 : 500,
        borderLeft: active ? "2px solid var(--primary)" : "2px solid transparent",
        paddingLeft: "12px",
        marginLeft: "-1px",
      }}
    >
      {entry.label}
    </a>
  );
}

export function LegalPageShell({
  title,
  effectiveDate,
  children,
}: {
  title: string;
  effectiveDate: string;
  children: React.ReactNode;
}) {
  const contentRef = useRef<HTMLDivElement>(null);
  const { toc, activeId } = useToc(contentRef);
  const [mobileNavOpen, setMobileNavOpen] = useState(false);

  return (
    <div className="min-h-screen" style={{ background: "var(--background)" }}>
      <header className="sticky top-0 z-50 border-b backdrop-blur-xl" style={{ background: "color-mix(in oklch, var(--background) 86%, transparent)", borderColor: "var(--border)" }}>
        <div className="max-w-[1040px] mx-auto flex items-center justify-between h-16 px-6">
          <Link href="/" className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0" style={{ background: "var(--primary)" }}>
              <GraduationCap size={15} color="white" />
            </div>
            <span className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>AlumUnion</span>
          </Link>
          <Link href="/" className="flex items-center gap-1.5 text-[13px] font-medium hover:text-foreground" style={{ color: "var(--muted-foreground)" }}>
            <ArrowLeft size={14} /> Back
          </Link>
        </div>
      </header>

      <main className="max-w-[1040px] mx-auto px-6 py-14">
        <div className="inline-flex items-center gap-2.5 mb-5">
          <span className="h-px w-6" style={{ background: "var(--primary)" }} />
          <span className="text-[12.5px] font-semibold tracking-wide" style={{ color: "var(--primary)" }}>Legal</span>
        </div>
        <h1 className="font-[family-name:var(--font-display)] mb-2" style={{ fontSize: "clamp(1.9rem, 4vw, 2.5rem)", fontWeight: 700, color: "var(--foreground)" }}>
          {title}
        </h1>
        <p className="text-[13px] mb-8" style={{ color: "var(--muted-foreground)" }}>Effective {effectiveDate}</p>

        {/* Mobile "On this page" — a collapsible quick-nav; the sticky sidebar below is desktop-only */}
        {toc.length > 0 && (
          <div className="lg:hidden mb-10 rounded-xl border" style={{ borderColor: "var(--border)" }}>
            <button
              onClick={() => setMobileNavOpen((v) => !v)}
              className="w-full flex items-center justify-between gap-3 px-4 py-3"
              aria-expanded={mobileNavOpen}
            >
              <span className="flex items-center gap-2 text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                <List size={14} /> On this page
              </span>
              {mobileNavOpen ? <X size={14} style={{ color: "var(--muted-foreground)" }} /> : <span className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>{toc.length} sections</span>}
            </button>
            {mobileNavOpen && (
              <div className="px-4 pb-4 space-y-0.5" style={{ borderTop: "1px solid var(--border)" }}>
                {toc.map((entry) => (
                  <TocLink key={entry.id} entry={entry} active={entry.id === activeId} onClick={() => setMobileNavOpen(false)} />
                ))}
              </div>
            )}
          </div>
        )}

        <div className="grid lg:grid-cols-[1fr_220px] gap-12 items-start">
          <div ref={contentRef} className="legal-content space-y-10 min-w-0" style={{ color: "var(--foreground)" }}>
            {children}
          </div>

          {/* Desktop sticky sidebar */}
          {toc.length > 0 && (
            <nav aria-label="On this page" className="hidden lg:block sticky top-24 self-start">
              <p className="text-[10.5px] font-bold tracking-[0.1em] uppercase mb-3" style={{ color: "var(--muted-foreground)" }}>
                On this page
              </p>
              <div className="space-y-0.5" style={{ borderLeft: "2px solid var(--border)" }}>
                {toc.map((entry) => (
                  <TocLink key={entry.id} entry={entry} active={entry.id === activeId} />
                ))}
              </div>
            </nav>
          )}
        </div>
      </main>

      <footer className="border-t py-8" style={{ borderColor: "var(--border)" }}>
        <div className="max-w-[1040px] mx-auto px-6 flex flex-wrap items-center justify-between gap-4 text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>
          <p>&copy; {new Date().getFullYear()} AlumUnion</p>
          <div className="flex items-center gap-5">
            <Link href="/terms" className="hover:text-foreground">Terms</Link>
            <Link href="/privacy" className="hover:text-foreground">Privacy</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}

export function Section({ heading, children }: { heading: string; children: React.ReactNode }) {
  const id = slugify(heading);
  return (
    <section className="scroll-mt-24 pt-10 first:pt-0 border-t first:border-t-0" style={{ borderColor: "var(--border)" }}>
      <h2 id={id} className="text-[18px] font-bold mb-3.5 scroll-mt-24" style={{ color: "var(--foreground)" }}>{heading}</h2>
      <div className="space-y-3 text-[14.5px]" style={{ color: "var(--muted-foreground)", lineHeight: 1.75 }}>
        {children}
      </div>
    </section>
  );
}
