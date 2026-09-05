"use client";

import Link from "next/link";
import { ArrowRight, Mail } from "@alumni/ui";
import { Button } from "@alumni/ui";

const PLATFORM_LINKS = [
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
      <div className="section__inner--wide py-12">
        <div className="grid grid-cols-2 gap-x-6 gap-y-8 sm:gap-x-8 lg:grid-cols-[1.6fr_1fr_1fr] pb-8">

          {/* Brand column */}
          <div className="col-span-2 lg:max-w-[36ch]">
            <Link href="/" className="flex items-center gap-3 mb-4">
              <img src="/alumunion-mark.svg" alt="" className="w-8 h-8 rounded-xl shrink-0" />
              <span className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>AlumUnion</span>
            </Link>
            <p className="text-[13px] leading-relaxed mb-4 max-w-[36ch]" style={{ color: "var(--muted-foreground)" }}>
              Free alumni portals for schools, universities, and any community that wants to stay connected.
            </p>
            <a href="mailto:hello@alumunion.com"
              className="inline-flex items-center gap-2 text-[12.5px] font-medium transition-colors hover:text-primary"
              style={{ color: "var(--muted-foreground)" }}>
              <Mail size={13} /> hello@alumunion.com
            </a>
          </div>

          {/* Platform column */}
          <div>
            <p className="text-[10.5px] font-bold tracking-[0.1em] uppercase mb-3" style={{ color: "var(--foreground)" }}>Platform</p>
            <nav className="flex flex-col gap-2.5" aria-label="Platform links">
              {PLATFORM_LINKS.map((link) => (
                <Link key={link.label} href={link.href}
                  className="text-[13px] font-medium transition-colors hover:text-primary w-fit"
                  style={{ color: "var(--muted-foreground)" }}>
                  {link.label}
                </Link>
              ))}
              <Link href="/why-not-whatsapp"
                className="text-[13px] font-medium transition-colors hover:text-primary w-fit"
                style={{ color: "var(--muted-foreground)" }}>
                Why not WhatsApp?
              </Link>
            </nav>
          </div>

          {/* Get started column */}
          <div>
            <p className="text-[10.5px] font-bold tracking-[0.1em] uppercase mb-3" style={{ color: "var(--foreground)" }}>Get started</p>
            <Link href="/#onboard"
              className="block mb-3 text-[13px] font-medium transition-colors hover:text-primary w-fit"
              style={{ color: "var(--muted-foreground)" }}>
              Onboard your institution
            </Link>
            <Link href="/#onboard">
              <Button size="sm" className="text-[12.5px] font-semibold gap-1.5">
                Get onboarded <ArrowRight size={12} />
              </Button>
            </Link>
          </div>
        </div>

        <div className="flex flex-col sm:flex-row items-center justify-between gap-3 pt-6 text-center sm:text-left" style={{ borderTop: "1px solid var(--border)" }}>
          <p className="text-[12px]" style={{ color: "var(--muted-foreground)", opacity: 0.8 }}>
            © {new Date().getFullYear()} AlumUnion. All rights reserved.
          </p>
          <div className="flex items-center gap-5">
            <Link href="/terms" className="text-[12px] font-medium transition-colors hover:text-foreground" style={{ color: "var(--muted-foreground)" }}>Terms</Link>
            <Link href="/privacy" className="text-[12px] font-medium transition-colors hover:text-foreground" style={{ color: "var(--muted-foreground)" }}>Privacy</Link>
          </div>
        </div>
      </div>
    </footer>
  );
}
