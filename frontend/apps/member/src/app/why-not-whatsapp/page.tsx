"use client";

import Link from "next/link";
import {
  ArrowLeft, ArrowRight, Check, X as XIcon, ExternalLink,
} from "@alumni/ui";
import { Button, cn } from "@alumni/ui";
import { Section, ScrollProgressBar, useScrolled, useTilt, useCountUp, CustomCursor, CustomCursorStyles } from "../_marketing/primitives";
import { MarketingFooter } from "../_marketing/footer";
import { CapIllustration, NoDirectoryIllustration, OnePhoneIllustration, NoDataIllustration, FraudIllustration } from "../_marketing/product-panels";

/* ─────────────────────────────────────────────────────────────────────────
   DATA — every claim here is sourced; see the Sources section on this page.
   ───────────────────────────────────────────────────────────────────────── */
const LIMITS: { title: string; desc: string; source: string; panel: React.ComponentType<{ className?: string }> }[] = [
  { title: "It caps out", desc: "A WhatsApp group maxes out at 1,024 members. A Community stretches that to 5,000 across up to 50 sub-groups, still a hard ceiling a growing membership will eventually hit.", source: "WhatsApp's own published limits", panel: CapIllustration },
  { title: "You can find messages, not people", desc: "WhatsApp search finds words in conversations. There's no member directory, so you can't filter by year joined, chapter or location. Finding a person means scrolling and asking around.", source: "WhatsApp Help Center: searching and group info", panel: NoDirectoryIllustration },
  { title: "One phone, one point of failure", desc: "Group control is tied to whoever's personal phone number set it up. If that admin changes numbers, loses their phone, or steps down, there's no organization account underneath, just a person's device.", source: "WhatsApp Help Center: group admins and phone numbers", panel: OnePhoneIllustration },
  { title: "No structure, no data", desc: "No built-in directory, no RSVP tracking, no dues or fundraiser collection, and no engagement analytics. Even in WhatsApp Communities, this stays a chat thread, not a management tool.", source: "WhatsApp Communities' documented feature set", panel: NoDataIllustration },
  { title: "It's a real fraud target", desc: "Group-chat scams follow a pattern: someone impersonates a member or a charity and asks the group for money. In the first half of 2024 the UK's Action Fraud logged 636 reports tied to WhatsApp group chats. Any fundraising drive run in a chat has the same exposure.", source: "UK Action Fraud, H1 2024", panel: FraudIllustration },
];

const COMPARISON: { row: string; whatsapp: string; alumunion: string }[] = [
  { row: "Member capacity",        whatsapp: "Caps at 1,024 (5,000 for a Community)", alumunion: "No cap, built for your whole membership" },
  { row: "Finding people",         whatsapp: "Scroll and guess who's who",             alumunion: "Searchable directory by name, chapter, location" },
  { row: "Search",                 whatsapp: "Searches messages, not members",           alumunion: "Search across members, events, jobs and news" },
  { row: "Who's in charge",        whatsapp: "Tied to one admin's personal phone",     alumunion: "Role-based admin accounts your organization controls" },
  { row: "Collecting dues/funds",  whatsapp: "Manual, screenshot-and-trust",           alumunion: "Secure online payments with automatic records" },
  { row: "Events",                 whatsapp: "Lost in the scroll, no RSVP tracking",   alumunion: "Built-in events with RSVP tracking" },
  { row: "Jobs & mentorship",      whatsapp: "Buried somewhere in chat history",       alumunion: "Dedicated jobs board & mentorship matching" },
  { row: "Photos & memories",      whatsapp: "Lost when storage clears or phones change", alumunion: "Permanent photo albums, organized by event" },
  { row: "Engagement insight",     whatsapp: "No idea who's actually engaged",         alumunion: "Real engagement data for your admin team" },
  { row: "Cost",                   whatsapp: "\"Free,\" but nobody's really running it", alumunion: "Free, and actually built for the job" },
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
function SourceCard({ s, index }: { s: typeof SOURCES[number]; index: number }) {
  const tilt = useTilt<HTMLAnchorElement>(4);
  return (
    <a ref={tilt.ref} href={s.url} target="_blank" rel="noopener noreferrer"
      onMouseMove={tilt.onMouseMove} onMouseLeave={tilt.onMouseLeave}
      style={tilt.style}
      className="card group flex items-start gap-4 p-5 transition-shadow duration-300 hover:shadow-sm hover:border-primary/40">
      <div className="w-9 h-9 rounded-none flex items-center justify-center shrink-0 font-[family-name:var(--font-display)] font-bold text-[13px]"
        style={{ background: "var(--card)", border: "1px solid var(--border-emphasis, var(--border))", color: "var(--primary)" }}>
        {String(index + 1).padStart(2, "0")}
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
  );
}

function StatCard({ end, decimals, suffix, desc }: { end: number; decimals: number; suffix: string; desc: string }) {
  const { ref, value } = useCountUp(end, decimals);
  return (
    <div ref={ref} className="card p-5">
      <p className="font-[family-name:var(--font-display)] leading-none mb-1.5" style={{ fontSize: "2rem", fontWeight: 700, color: "var(--foreground)" }}>
        {value}{suffix}
      </p>
      <p className="text-[12.5px] leading-snug" style={{ color: "var(--muted-foreground)" }}>{desc}</p>
    </div>
  );
}

function LimitRow({ item, index }: { item: typeof LIMITS[number]; index: number }) {
  const reverse = index % 2 === 1;
  return (
    <div className="grid items-center gap-8 py-8 sm:gap-12 md:py-12 lg:grid-cols-2">
      <div className={cn("min-w-0 p-5 sm:p-8", reverse && "lg:order-2")} style={{ background: "var(--muted)", border: "1px solid var(--border)" }}>
        <item.panel className="mx-auto w-full max-w-[420px]" />
      </div>
      <div className={cn("min-w-0", reverse && "lg:order-1")}>
        <p className="mb-3 text-[13px] font-bold tabular-nums" style={{ color: "var(--destructive)" }}>{String(index + 1).padStart(2, "0")}</p>
        <h3 className="mb-3 text-[21px] font-semibold leading-snug sm:text-[24px]" style={{ color: "var(--foreground)" }}>{item.title}</h3>
        <p className="mb-3 max-w-[52ch] text-[14.5px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>{item.desc}</p>
        <p className="text-[12px]" style={{ color: "var(--muted-foreground)", opacity: 0.7 }}>Source: {item.source}</p>
      </div>
    </div>
  );
}

const COMPARE_ROWS: { whatsapp: string; alumunion: string }[] = [
  { whatsapp: "Caps at 1,024 members", alumunion: "No cap, built for the whole membership" },
  { whatsapp: "Scroll and guess who's who", alumunion: "Searchable directory by name, chapter, location" },
  { whatsapp: "Tied to one admin's personal phone", alumunion: "Role-based accounts the organization owns" },
  { whatsapp: "Manual, screenshot-and-trust dues", alumunion: "Secure online payments, automatic records" },
  { whatsapp: "No RSVP tracking, no analytics", alumunion: "Real RSVPs and engagement reports" },
];

/** The short version, side by side, before the detail below. Stacks on a phone so nothing is ever cut off. */
function CompareAtAGlance() {
  return (
    <div className="grid gap-4 text-left md:grid-cols-2">
      <div className="p-6 sm:p-8" style={{ background: "var(--muted)", border: "1px solid var(--border)" }}>
        <p className="mb-4 text-[11px] font-bold uppercase tracking-[0.1em]" style={{ color: "var(--muted-foreground)" }}>WhatsApp group</p>
        <ul className="space-y-3.5">
          {COMPARE_ROWS.map((r) => (
            <li key={r.whatsapp} className="flex items-start gap-2.5 text-[14px]" style={{ color: "var(--muted-foreground)" }}>
              <XIcon size={15} className="mt-0.5 shrink-0" style={{ color: "var(--destructive)" }} />
              {r.whatsapp}
            </li>
          ))}
        </ul>
      </div>
      <div className="p-6 sm:p-8" style={{ background: "var(--card)", border: "1px solid var(--primary)" }}>
        <p className="mb-4 text-[11px] font-bold uppercase tracking-[0.1em]" style={{ color: "var(--primary)" }}>AlumUnion</p>
        <ul className="space-y-3.5">
          {COMPARE_ROWS.map((r) => (
            <li key={r.alumunion} className="flex items-start gap-2.5 text-[14px] font-medium" style={{ color: "var(--foreground)" }}>
              <Check size={15} className="mt-0.5 shrink-0" style={{ color: "var(--primary)" }} />
              {r.alumunion}
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}

export default function WhyNotWhatsAppPage() {
  const scrolled = useScrolled(24);
  return (
    <div className="min-h-screen overflow-x-hidden au-cursor-zone" style={{ background: "var(--background)", color: "var(--foreground)" }}>
      <CustomCursorStyles />
      <CustomCursor />
      <ScrollProgressBar />

      {/* ── Header — shrinks slightly once the page has scrolled, matching the homepage ── */}
      <header className="sticky top-0 z-50 border-b backdrop-blur-xl transition-shadow duration-300"
        style={{
          background: "color-mix(in oklch, var(--background) 86%, transparent)",
          borderColor: "var(--border)",
          boxShadow: scrolled ? "0 4px 20px rgba(0,0,0,0.05)" : "none",
        }}>
        <div className={cn("section__inner flex items-center justify-between gap-4 transition-[height] duration-300 ease-out", scrolled ? "h-14" : "h-16")}>
          <Link href="/" className="flex items-center gap-3 shrink-0">
            <img src="/alumunion-mark.svg" alt="" className={cn("rounded-lg shrink-0 transition-all duration-300", scrolled ? "w-7 h-7" : "w-8 h-8")} />
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
          <h1 className="font-[family-name:var(--font-display)] mb-6 max-w-[26ch]"
            style={{ fontSize: "clamp(2.2rem,4.6vw,3.5rem)", fontWeight: 700, lineHeight: 1.12, letterSpacing: "-0.025em", color: "var(--foreground)", margin: "0 auto 1.5rem" }}>
            WhatsApp wasn&apos;t built to run your community.
          </h1>
          <p className="max-w-[54ch] mb-10" style={{ fontSize: "1.05rem", lineHeight: 1.75, color: "var(--muted-foreground)", margin: "0 auto 2.5rem" }}>
            It&apos;s free, familiar, and everyone already has it, which is why so many communities start there.
            A chat app can&apos;t keep records, collect money safely or hold your members together as you grow. Here&apos;s what that looks like, with sources.
          </p>

          <div className="max-w-[860px] mx-auto text-left">
            <p className="text-center text-[12px] font-semibold uppercase tracking-wide mb-3" style={{ color: "var(--muted-foreground)" }}>
              The short version
            </p>
            <CompareAtAGlance />
            <div className="mt-8 text-center">
              <Link href="/#onboard">
                <Button size="lg" className="h-12 gap-2 px-8 text-[14.5px] font-semibold">
                  Get your community onboarded <ArrowRight size={15} />
                </Button>
              </Link>
            </div>
          </div>
        </div>
      </div>

      {/* ── The real limits ── */}
      <Section className="border-b " style={{ background: "var(--muted)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="mb-12 max-w-[56ch] text-center mx-auto">
            <h2 className="font-[family-name:var(--font-display)] mb-4 " style={{ color: "var(--foreground)" }}>
              Five ways it actually holds your community back.
            </h2>
            <p style={{ color: "var(--muted-foreground)", fontSize: "1.025rem", lineHeight: 1.75 }}>
              Each point names its source, and the full list with links is at the bottom of the page.
            </p>
          </div>
          <div className="mx-auto max-w-5xl divide-y" style={{ borderColor: "var(--border)" }}>
            {LIMITS.map((item, i) => <LimitRow key={item.title} item={item} index={i} />)}
          </div>
        </div>
      </Section>

      {/* ── Comparison table ── */}
      <Section className="border-b" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="mb-6 max-w-[56ch] mx-auto text-center">
            <h2 className="font-[family-name:var(--font-display)]" style={{ color: "var(--foreground)", margin: "0 auto" }}>
              WhatsApp group vs. AlumUnion.
            </h2>
          </div>

          <ul className="space-y-3 sm:hidden">
            {COMPARISON.map((r) => (
              <li key={r.row} className="border p-4" style={{ borderColor: "var(--border)", background: "var(--card)" }}>
                <p className="mb-3 text-[13px] font-bold" style={{ color: "var(--foreground)" }}>{r.row}</p>
                <p className="mb-2 flex items-start gap-2 text-[13px]" style={{ color: "var(--muted-foreground)" }}>
                  <XIcon size={14} className="mt-0.5 shrink-0" style={{ color: "var(--destructive)" }} />{r.whatsapp}
                </p>
                <p className="flex items-start gap-2 text-[13px] font-medium" style={{ color: "var(--foreground)" }}>
                  <Check size={14} className="mt-0.5 shrink-0" style={{ color: "var(--primary)" }} />{r.alumunion}
                </p>
              </li>
            ))}
          </ul>

          {/* .card sets overflow:hidden for its rounded corners/shadow, which clips
              horizontal scroll if applied to the same element — so the scrollable
              region is a separate inner wrapper, not the card itself. */}
          <div className="card hidden sm:block">
            <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse min-w-[560px]">
              <thead>
                <tr style={{ borderBottom: "1px solid var(--border)" }}>
                  <th className="p-4 text-[11px] font-bold uppercase tracking-wide" style={{ color: "var(--muted-foreground)" }}></th>
                  <th className="p-4 text-[12.5px] font-bold" style={{ color: "var(--muted-foreground)" }}>WhatsApp group</th>
                  <th className="p-4 text-[12.5px] font-bold" style={{ color: "var(--primary)" }}>AlumUnion</th>
                </tr>
              </thead>
              <tbody>
                {COMPARISON.map((r) => (
                  <tr key={r.row} className="transition-colors duration-150 hover:bg-muted/60" style={{ borderBottom: "1px solid var(--border)" }}>
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
        </div>
      </Section>

      {/* ── The wider picture (context, not causation) ── */}
      <Section className="border-b" style={{ background: "var(--muted)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="grid gap-10 lg:grid-cols-2">
            <div>
              <h2 className="font-[family-name:var(--font-display)] mb-4 max-w-[20ch]" style={{ color: "var(--foreground)" }}>
                Keeping a community engaged is hard everywhere, not just on WhatsApp.
              </h2>
              <p style={{ color: "var(--muted-foreground)", fontSize: "0.975rem", lineHeight: 1.75 }}>
                To be fair, WhatsApp isn&apos;t the cause. Participation in member organizations has been falling for
                decades, however they communicate. The best-measured example is alumni associations, so that&apos;s where
                these figures come from. A real platform can help push back against the trend; a chat group alone won&apos;t.
              </p>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
              <StatCard end={7.8} decimals={1} suffix="%" desc="Average alumni giving participation in 2023, down from 8.5% in 2016, and ~20% in the 1980s." />
              <StatCard end={75} decimals={0} suffix="%" desc="Of alumni say they'd engage more if access to their association were mobile-friendly." />
            </div>
          </div>
        </div>
      </Section>

      {/* ── Sources ── */}
      <Section className="border-b" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <h2 className="font-[family-name:var(--font-display)] mb-4 max-w-[26ch] text-center" style={{ color: "var(--foreground)", margin: "0 auto 1rem" }}>
            Every claim on this page, sourced.
          </h2>
          <p className="mb-10 max-w-[56ch] mx-auto text-center" style={{ color: "var(--muted-foreground)", fontSize: "1.025rem", lineHeight: 1.75 }}>
            Nothing above is a guess. Here&apos;s exactly where each number and claim comes from. Click through and check
            for yourself.
          </p>
          <div className="grid sm:grid-cols-2 gap-4 max-w-4xl mx-auto">
            {SOURCES.map((s, i) => <SourceCard key={s.name} s={s} index={i} />)}
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
              Give your community something built for the job, free.
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
