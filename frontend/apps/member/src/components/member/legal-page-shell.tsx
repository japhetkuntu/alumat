"use client";

import Link from "next/link";
import { ArrowLeft, GraduationCap } from "lucide-react";

export function LegalPageShell({
  title,
  effectiveDate,
  children,
}: {
  title: string;
  effectiveDate: string;
  children: React.ReactNode;
}) {
  return (
    <div className="min-h-screen" style={{ background: "var(--background)" }}>
      <header className="sticky top-0 z-50 border-b backdrop-blur-xl" style={{ background: "color-mix(in oklch, var(--background) 86%, transparent)", borderColor: "var(--border)" }}>
        <div className="max-w-[760px] mx-auto flex items-center justify-between h-16 px-6">
          <Link href="/" className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0" style={{ background: "var(--primary)" }}>
              <GraduationCap size={15} color="white" />
            </div>
            <span className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>Alumni Portal</span>
          </Link>
          <Link href="/" className="flex items-center gap-1.5 text-[13px] font-medium hover:text-foreground" style={{ color: "var(--muted-foreground)" }}>
            <ArrowLeft size={14} /> Back
          </Link>
        </div>
      </header>

      <main className="max-w-[760px] mx-auto px-6 py-14">
        <h1 className="font-[family-name:var(--font-display)] mb-2" style={{ fontSize: "clamp(1.75rem, 4vw, 2.25rem)", fontWeight: 700, color: "var(--foreground)" }}>
          {title}
        </h1>
        <p className="text-[13px] mb-10" style={{ color: "var(--muted-foreground)" }}>Effective {effectiveDate}</p>

        <div className="legal-content space-y-8" style={{ color: "var(--foreground)" }}>
          {children}
        </div>
      </main>

      <footer className="border-t py-8" style={{ borderColor: "var(--border)" }}>
        <div className="max-w-[760px] mx-auto px-6 flex flex-wrap items-center justify-between gap-4 text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>
          <p>&copy; {new Date().getFullYear()} Alumni Portal</p>
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
  return (
    <section>
      <h2 className="text-[17px] font-bold mb-3" style={{ color: "var(--foreground)" }}>{heading}</h2>
      <div className="space-y-3 text-[14.5px]" style={{ color: "var(--muted-foreground)", lineHeight: 1.75 }}>
        {children}
      </div>
    </section>
  );
}
