"use client";

import Link from "next/link";
import { Mail } from "@alumni/ui";

const PLATFORM_LINKS = [
  { label: "Explore the portal", href: "/#product" },
  { label: "Costs", href: "/#costs" },
  { label: "Features",     href: "/#features"     },
  { label: "How it works", href: "/#how-it-works" },
  { label: "FAQ",          href: "/#faq"          },
];

/** Shared footer for the platform marketing homepage and its sub-pages (e.g.
 * /why-not-whatsapp). Internal section links always point at the homepage's
 * anchors via plain <Link> navigation (not the homepage's own JS
 * scrollToSection, which only works same-page) so they're correct from any
 * page, not just "/". */
export function MarketingFooter() {
  return (
    <footer className="border-t" style={{ background: "var(--muted)", borderColor: "var(--border)" }}>
      <div className="mk-wrap marketing-footer-inner py-8 sm:py-9">
        <div className="flex flex-col gap-6 sm:flex-row sm:items-center sm:justify-between pb-6">
          <div className="max-w-[58ch]">
            <Link href="/" className="flex items-center gap-3 mb-3">
              <img src="/alumunion-logo-horizontal.svg" alt="AlumUnion" width={1870} height={420} className="h-8 w-auto object-contain shrink-0 dark:rounded-sm dark:bg-white dark:px-2" />
            </Link>
            <p className="text-[15px] leading-relaxed mb-3" style={{ color: "var(--muted-foreground)" }}>
              A community platform for institutions to organize alumni, members, supporters, and stakeholders in one place.
            </p>
            <a href="mailto:hello@alumunion.com"
              className="inline-flex items-center gap-2 text-[14px] font-medium transition-colors hover:text-primary"
              style={{ color: "var(--muted-foreground)" }}>
              <Mail size={13} /> hello@alumunion.com
            </a>
          </div>
          <Link href="/#onboard" className="shrink-0">
            <span className="inline-flex min-h-11 whitespace-nowrap items-center rounded-full bg-primary px-6 text-primary-foreground text-[15px] font-semibold">Build your community</span>
          </Link>
        </div>

        <div className="flex flex-col gap-4 border-t border-border py-5 md:flex-row md:items-center md:gap-9">
          <p className="shrink-0 text-[13px] font-bold tracking-[0.1em] uppercase" style={{ color: "var(--foreground)" }}>Explore</p>
          <nav className="flex flex-wrap gap-x-8 gap-y-3" aria-label="Platform links">
            {PLATFORM_LINKS.map((link) => (
              <Link key={link.label} href={link.href}
                className="text-[15px] font-medium transition-colors hover:text-primary"
                style={{ color: "var(--muted-foreground)" }}>
                {link.label}
              </Link>
            ))}
            <Link href="/why-not-whatsapp"
              className="text-[15px] font-medium transition-colors hover:text-primary"
              style={{ color: "var(--muted-foreground)" }}>
              Why not WhatsApp?
            </Link>
          </nav>
        </div>

        <div className="flex flex-col sm:flex-row items-center justify-between gap-3 pt-5 text-center sm:text-left border-t border-border">
          <p className="text-[14px]" style={{ color: "var(--muted-foreground)", opacity: 0.8 }}>
            © {new Date().getFullYear()} AlumUnion. All rights reserved.
          </p>
          <div className="flex flex-wrap items-center justify-center gap-x-5 gap-y-2">
            <Link href="/terms" className="text-[14px] font-medium transition-colors hover:text-foreground" style={{ color: "var(--muted-foreground)" }}>Terms</Link>
            <Link href="/privacy" className="text-[14px] font-medium transition-colors hover:text-foreground" style={{ color: "var(--muted-foreground)" }}>Privacy</Link>
            <Link href="/institution-agreement" className="text-[14px] font-medium transition-colors hover:text-foreground" style={{ color: "var(--muted-foreground)" }}>Institution Agreement</Link>
          </div>
        </div>
      </div>
    </footer>
  );
}
