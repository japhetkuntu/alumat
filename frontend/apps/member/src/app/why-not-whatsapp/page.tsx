"use client";

import Link from "next/link";
import type { LucideIcon } from "lucide-react";
import {
  ArrowLeft, ArrowRight, UserX, SearchX, ShieldAlert,
  MessageCircleOff, PhoneOff, Check, X as XIcon, ExternalLink,
} from "lucide-react";
import { Button } from "@alumni/ui";
import { Eyebrow, Section } from "../_marketing/primitives";
import { MarketingFooter } from "../_marketing/footer";

/* ─────────────────────────────────────────────────────────────────────────
   DATA — every claim here is sourced; see the Sources section on this page.
   ───────────────────────────────────────────────────────────────────────── */
const LIMITS: { icon: LucideIcon; title: string; desc: string; source: string }[] = [
  { icon: UserX, title: "It caps out", desc: "A WhatsApp group maxes out at 1,024 members. A Community stretches that to 5,000 across up to 50 sub-groups — still a hard ceiling a growing alumni base will eventually hit.", source: "WhatsApp's own published limits" },
  { icon: SearchX, title: "You can't find anything", desc: "WhatsApp's search only finds text matches inside one chat at a time. There's no member directory, no filtering by class year or location — just scrolling and hoping.", source: "WhatsApp product limitations, widely documented" },
  { icon: PhoneOff, title: "One phone, one point of failure", desc: "Group control is tied to whoever's personal phone number set it up. If that admin changes numbers, loses their phone, or steps down, there's no institutional account underneath — just a person's device.", source: "How WhatsApp group admin actually works" },
  { icon: MessageCircleOff, title: "No structure, no data", desc: "No built-in directory, no RSVP tracking, no dues or fundraiser collection, and no engagement analytics — even in WhatsApp Communities, this stays a chat thread, not a management tool.", source: "WhatsApp Communities' documented feature set" },
  { icon: ShieldAlert, title: "It's a real fraud target", desc: "The UK's Action Fraud logged 636 reports tied to WhatsApp group-chat scams in the first half of 2024 alone — a common tactic is impersonating a group member (or a charity) to solicit money. That's exactly the shape of a fundraising drive run over a WhatsApp group.", source: "UK Action Fraud, H1 2024" },
];

const COMPARISON: { row: string; whatsapp: string; alumunion: string }[] = [
  { row: "Member capacity",        whatsapp: "Caps at 1,024 (5,000 for a Community)", alumunion: "No cap — built for your whole alumni base" },
  { row: "Finding people",         whatsapp: "Scroll and guess who's who",             alumunion: "Searchable directory by name, class year, location" },
  { row: "Search",                 whatsapp: "Text search, one chat at a time",        alumunion: "Search across events, jobs, directory, everything" },
  { row: "Who's in charge",        whatsapp: "Tied to one admin's personal phone",     alumunion: "Role-based admin accounts your institution controls" },
  { row: "Collecting dues/funds",  whatsapp: "Manual, screenshot-and-trust",           alumunion: "Secure online payments with automatic records" },
  { row: "Events",                 whatsapp: "Lost in the scroll, no RSVP tracking",   alumunion: "Built-in events with RSVP tracking" },
  { row: "Jobs & mentorship",      whatsapp: "Buried somewhere in chat history",       alumunion: "Dedicated jobs board & mentorship matching" },
  { row: "Photos & memories",      whatsapp: "Lost when storage clears or phones change", alumunion: "Permanent photo albums, organized by event" },
  { row: "Engagement insight",     whatsapp: "No idea who's actually engaged",         alumunion: "Real engagement data for your admin team" },
  { row: "Cost",                   whatsapp: "\"Free,\" but nobody's really running it", alumunion: "Free — and actually built for the job" },
];

const SOURCES = [
  { name: "WhatsApp group & Community size limits", org: "WhatsApp / Meta", backs: "The 1,024-member group cap and 5,000-member Community cap", url: "https://faq.whatsapp.com/" },
  { name: "WhatsApp group-chat scam alert, H1 2024", org: "UK Action Fraud (government body)", backs: "The 636 reported group-chat scam cases", url: "https://www.actionfraud.police.uk/" },
  { name: "Rethinking Participation Rates", org: "CASE, citing the CAE Voluntary Support of Education Survey", backs: "The 7.8% / 8.5% / ~20% alumni giving participation figures", url: "https://www.case.org/resources/issues/september-october-2023/rethinking-participation-rates" },
  { name: "The Ultimate Collection of Statistics for Alumni Engagement, Giving and Membership", org: "AlumniAccess", backs: "The 75% mobile-access preference figure", url: "https://blog.alumniaccess.com/member_marketing_statistics_ultimate_collection_alumni-2015" },
];

/* ─────────────────────────────────────────────────────────────────────────
   COMPONENTS
   ───────────────────────────────────────────────────────────────────────── */
function LimitCard({ item }: { item: typeof LIMITS[number] }) {
  return (
    <div className="card">
      <div className="card__content">
        <div className="w-10 h-10 rounded-[10px] flex items-center justify-center mb-4 shrink-0"
          style={{ background: "color-mix(in oklch, var(--destructive) 12%, transparent)", border: "1px solid color-mix(in oklch, var(--destructive) 35%, transparent)" }}>
          <item.icon size={17} style={{ color: "var(--destructive)" }} />
        </div>
        <h3 className="text-[15px] font-semibold leading-snug mb-2" style={{ color: "var(--foreground)" }}>{item.title}</h3>
        <p className="text-[13.5px] leading-relaxed mb-3" style={{ color: "var(--muted-foreground)" }}>{item.desc}</p>
        <p className="text-[11px] font-medium uppercase tracking-wide" style={{ color: "var(--muted-foreground)", opacity: 0.65 }}>— {item.source}</p>
      </div>
    </div>
  );
}

export default function WhyNotWhatsAppPage() {
  return (
    <div className="min-h-screen overflow-x-hidden" style={{ background: "var(--background)", color: "var(--foreground)" }}>

      {/* ── Header ── */}
      <header className="sticky top-0 z-50 border-b backdrop-blur-xl"
        style={{ background: "color-mix(in oklch, var(--background) 86%, transparent)", borderColor: "var(--border)" }}>
        <div className="section__inner flex items-center justify-between h-16 gap-4">
          <Link href="/" className="flex items-center gap-3 shrink-0">
            <img src="/alumunion-mark.svg" alt="" className="w-8 h-8 rounded-lg shrink-0" />
            <span className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>AlumUnion</span>
          </Link>
          <Link href="/" className="flex items-center gap-1.5 text-[13px] font-medium hover:text-foreground" style={{ color: "var(--muted-foreground)" }}>
            <ArrowLeft size={14} /> Back to home
          </Link>
        </div>
      </header>

      {/* ── Hero ── */}
      <div className="relative overflow-hidden" style={{ background: "var(--background)" }}>
        <div className="absolute inset-0 bg-subtle-pattern opacity-[0.45] pointer-events-none" />
        <div className="section__inner--wide relative pt-16 pb-16 text-center">
          <Eyebrow>The honest comparison</Eyebrow>
          <h1 className="font-[family-name:var(--font-display)] mb-6 max-w-[26ch]"
            style={{ fontSize: "clamp(2.2rem,4.6vw,3.5rem)", fontWeight: 700, lineHeight: 1.12, letterSpacing: "-0.025em", color: "var(--foreground)", margin: "0 auto 1.5rem" }}>
            WhatsApp wasn&apos;t built to run your alumni community.
          </h1>
          <p className="max-w-[54ch]" style={{ fontSize: "1.05rem", lineHeight: 1.75, color: "var(--muted-foreground)", margin: "0 auto" }}>
            It&apos;s free, familiar, and everyone already has it — that&apos;s exactly why so many institutions start there.
            But a chat app is not a community platform. Here&apos;s the honest, sourced case for why it shows.
          </p>
        </div>
      </div>

      {/* ── The real limits ── */}
      <Section className="border-b" style={{ background: "var(--muted)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="mb-12 max-w-[56ch]">
            <Eyebrow>The real limits</Eyebrow>
            <h2 className="font-[family-name:var(--font-display)] mb-4" style={{ color: "var(--foreground)" }}>
              Five ways it actually holds your community back.
            </h2>
            <p style={{ color: "var(--muted-foreground)", fontSize: "1.025rem", lineHeight: 1.75 }}>
              Not opinions — every point below is sourced. See the full list under Sources.
            </p>
          </div>
          <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-[var(--space-gap)]">
            {LIMITS.map((item) => <LimitCard key={item.title} item={item} />)}
          </div>
        </div>
      </Section>

      {/* ── Comparison table ── */}
      <Section className="border-b" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="mb-10 max-w-[56ch] mx-auto text-center">
            <Eyebrow>Side by side</Eyebrow>
            <h2 className="font-[family-name:var(--font-display)]" style={{ color: "var(--foreground)", margin: "0 auto" }}>
              WhatsApp group vs. AlumUnion.
            </h2>
          </div>

          <div className="card overflow-x-auto">
            <table className="w-full text-left border-collapse min-w-[640px]">
              <thead>
                <tr style={{ borderBottom: "1px solid var(--border)" }}>
                  <th className="p-4 text-[11px] font-bold uppercase tracking-wide" style={{ color: "var(--muted-foreground)" }}></th>
                  <th className="p-4 text-[12.5px] font-bold" style={{ color: "var(--muted-foreground)" }}>WhatsApp group</th>
                  <th className="p-4 text-[12.5px] font-bold" style={{ color: "var(--primary)" }}>AlumUnion</th>
                </tr>
              </thead>
              <tbody>
                {COMPARISON.map((r) => (
                  <tr key={r.row} style={{ borderBottom: "1px solid var(--border)" }}>
                    <td className="p-4 text-[13px] font-semibold whitespace-nowrap" style={{ color: "var(--foreground)" }}>{r.row}</td>
                    <td className="p-4 text-[13px]" style={{ color: "var(--muted-foreground)" }}>
                      <span className="flex items-start gap-2">
                        <XIcon size={14} className="shrink-0 mt-0.5" style={{ color: "var(--destructive)" }} />
                        {r.whatsapp}
                      </span>
                    </td>
                    <td className="p-4 text-[13px] font-medium" style={{ color: "var(--foreground)" }}>
                      <span className="flex items-start gap-2">
                        <Check size={14} className="shrink-0 mt-0.5" style={{ color: "var(--primary)" }} />
                        {r.alumunion}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </Section>

      {/* ── The wider picture (context, not causation) ── */}
      <Section className="border-b" style={{ background: "var(--muted)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="grid gap-10 lg:grid-cols-2">
            <div>
              <Eyebrow>The wider picture</Eyebrow>
              <h2 className="font-[family-name:var(--font-display)] mb-4 max-w-[20ch]" style={{ color: "var(--foreground)" }}>
                Alumni engagement is a national problem — not just a WhatsApp one.
              </h2>
              <p style={{ color: "var(--muted-foreground)", fontSize: "0.975rem", lineHeight: 1.75 }}>
                To be clear: this isn&apos;t WhatsApp&apos;s fault. Alumni participation has been declining for decades,
                across every kind of institution, no matter how they communicate. It&apos;s the industry-wide trend a
                real platform can help push back against — a chat group alone won&apos;t.
              </p>
            </div>
            <div className="grid grid-cols-2 gap-5">
              <div className="card p-5">
                <p className="font-[family-name:var(--font-display)] leading-none mb-1.5" style={{ fontSize: "2rem", fontWeight: 700, color: "var(--foreground)" }}>7.8%</p>
                <p className="text-[12.5px] leading-snug" style={{ color: "var(--muted-foreground)" }}>Average alumni giving participation in 2023 — down from 8.5% in 2016, and ~20% in the 1980s.</p>
              </div>
              <div className="card p-5">
                <p className="font-[family-name:var(--font-display)] leading-none mb-1.5" style={{ fontSize: "2rem", fontWeight: 700, color: "var(--foreground)" }}>75%</p>
                <p className="text-[12.5px] leading-snug" style={{ color: "var(--muted-foreground)" }}>Of alumni say they&apos;d engage more if access to their association were mobile-friendly.</p>
              </div>
            </div>
          </div>
        </div>
      </Section>

      {/* ── Sources ── */}
      <Section className="border-b" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <Eyebrow>Sources</Eyebrow>
          <h2 className="font-[family-name:var(--font-display)] mb-4 max-w-[26ch]" style={{ color: "var(--foreground)" }}>
            Every claim on this page, sourced.
          </h2>
          <p className="mb-10 max-w-[56ch]" style={{ color: "var(--muted-foreground)", fontSize: "1.025rem", lineHeight: 1.75 }}>
            Nothing above is a guess. Here&apos;s exactly where each number and claim comes from — click through and check
            for yourself.
          </p>
          <div className="grid sm:grid-cols-2 gap-4 max-w-4xl">
            {SOURCES.map((s, i) => (
              <a key={s.name} href={s.url} target="_blank" rel="noopener noreferrer"
                className="card group flex items-start gap-4 p-5 transition-all duration-300 hover:-translate-y-0.5 hover:shadow-sm hover:border-primary/40">
                <div className="w-9 h-9 rounded-[10px] flex items-center justify-center shrink-0 font-[family-name:var(--font-display)] font-bold text-[13px]"
                  style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)", color: "var(--primary)" }}>
                  {String(i + 1).padStart(2, "0")}
                </div>
                <div className="min-w-0">
                  <p className="text-[13.5px] font-semibold leading-snug mb-1 group-hover:text-primary transition-colors" style={{ color: "var(--foreground)" }}>
                    {s.name}
                  </p>
                  <p className="text-[11.5px] font-medium mb-2" style={{ color: "var(--muted-foreground)" }}>{s.org}</p>
                  <p className="text-[12px] leading-relaxed mb-2.5" style={{ color: "var(--muted-foreground)", opacity: 0.85 }}>Backs: {s.backs}</p>
                  <span className="inline-flex items-center gap-1.5 text-[11.5px] font-semibold" style={{ color: "var(--primary)" }}>
                    View source <ExternalLink size={12} className="transition-transform group-hover:translate-x-0.5 group-hover:-translate-y-0.5" />
                  </span>
                </div>
              </a>
            ))}
          </div>
        </div>
      </Section>

      {/* ── Final CTA ── */}
      <Section style={{ background: "var(--brand-primary-dark, var(--primary))" }}>
        <div className="relative overflow-hidden">
          <div className="absolute -top-24 -left-24 w-96 h-96 rounded-full pointer-events-none"
            style={{ background: "radial-gradient(circle, rgba(255,255,255,0.08), transparent 70%)" }} />
          <div className="section__inner--wide relative py-20 sm:py-24 text-center">
            <h2 className="font-[family-name:var(--font-display)] mb-6 max-w-[22ch]"
              style={{ fontSize: "clamp(1.9rem,3.8vw,2.75rem)", lineHeight: 1.15, color: "white", margin: "0 auto 1.5rem" }}>
              Give your alumni something built for the job — free.
            </h2>
            <Link href="/#onboard">
              <Button size="lg" className="h-12 px-10 text-[15px] font-semibold gap-2"
                style={{ background: "white", color: "var(--primary)" }}>
                Get your institution onboarded <ArrowRight size={16} />
              </Button>
            </Link>
          </div>
        </div>
      </Section>

      <MarketingFooter />
    </div>
  );
}
