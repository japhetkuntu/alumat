"use client";

import Link from "next/link";
import { useState, useEffect, useRef, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import type { LucideIcon } from "lucide-react";
import {
  GraduationCap, Users, Briefcase, Heart, Globe,
  Menu, X, ArrowRight, ChevronRight,
  BookOpen, Trophy, CreditCard, Bell,
  MapPin, Zap, Shield, Star, Award,
} from "lucide-react";
import { Button } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { publicMemberClient } from "@/lib/api-client";

/* ─────────────────────────────────────────────────────────────────────────
   DYNAMIC CONTENT — Stories and the news banner are editable by both the
   platform team and this institution's own admins (see the Landing content
   tab in the Platform Portal / Institution Portal settings). Everything
   else on this page stays static for now.
   ───────────────────────────────────────────────────────────────────────── */
const STORY_ICONS: Record<string, LucideIcon> = {
  Briefcase, Users, CreditCard, BookOpen, Globe, Heart, Trophy, Bell,
  GraduationCap, Shield, MapPin, Zap, Star, Award,
};
const STORY_FALLBACK_IMAGES = [
  "https://images.unsplash.com/photo-1521737711867-e3b97375f902?auto=format&fit=crop&w=600&q=80",
  "https://images.unsplash.com/photo-1559027615-cd4628902d4a?auto=format&fit=crop&w=600&q=80",
  "https://images.unsplash.com/photo-1522202176988-66273c2fd55f?auto=format&fit=crop&w=600&q=80",
];

interface DynamicLandingStory {
  icon: string;
  eyebrow: string;
  scenario: string;
  description: string;
  imageUrl?: string | null;
}

interface DynamicNewsBanner {
  enabled: boolean;
  text: string;
  linkText?: string | null;
  linkUrl?: string | null;
}

interface LandingContent {
  landingPageStories: DynamicLandingStory[];
  newsBanner: DynamicNewsBanner | null;
  displayName?: string | null;
  logoUrl?: string | null;
}

function useLandingContent() {
  const { data } = useQuery({
    queryKey: ["member-landing-content"],
    queryFn: async () => {
      const res = await publicMemberClient.get<{ data: LandingContent }>("/public/institution/theme");
      return res.data.data;
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
  return data;
}

/* ─────────────────────────────────────────────────────────────────────────
   UNSPLASH IMAGE URLS
   All free-to-use under the Unsplash License — no attribution required.
   ───────────────────────────────────────────────────────────────────────── */
const IMG = {
  // Hero right-side panel background — students in lecture hall
  heroPanel: "https://images.unsplash.com/photo-1523240795612-9a054b0db644?auto=format&fit=crop&w=900&q=80",    // students studying together
  // Use-case/stories section — 3 contextual images
  storyJobs:    "https://images.unsplash.com/photo-1521737711867-e3b97375f902?auto=format&fit=crop&w=600&q=80",  // people in office meeting
  storyGiving:  "https://images.unsplash.com/photo-1559027615-cd4628902d4a?auto=format&fit=crop&w=600&q=80",    // hands giving / community
  storyMentor:  "https://images.unsplash.com/photo-1522202176988-66273c2fd55f?auto=format&fit=crop&w=600&q=80", // mentor and mentee at table
};

/* ─────────────────────────────────────────────────────────────────────────
   DATA
   ───────────────────────────────────────────────────────────────────────── */
const NAV_LINKS = [
  { label: "Features",     href: "#features"     },
  { label: "Stories",      href: "#stories"      },
  { label: "How it works", href: "#how-it-works" },
];

const FEATURES = [
  { icon: Briefcase,  label: "Careers",       title: "Jobs inside the network",       desc: "Roles posted by alumni employers before they reach public boards — first look, before LinkedIn.", big: true },
  { icon: Users,      label: "Directory",     title: "Find any grad in seconds",      desc: "Search by class year, department, company, or country." },
  { icon: CreditCard, label: "Contributions", title: "Fund what matters",             desc: "Alumni-led fundraisers for labs, scholarships, and campus improvements." },
  { icon: BookOpen,   label: "Class Notes",   title: "Keep the conversation going",   desc: "Post milestones, share knowledge, trade stories by graduation year." },
  { icon: Globe,      label: "Events",        title: "Never miss a reunion",          desc: "Homecomings, webinars, networking nights — RSVP in one place." },
  { icon: Heart,      label: "Mentorship",    title: "Give back. Get ahead.",         desc: "Connect with alumni who've already done what you're trying to do, one conversation at a time.", big: true },
  { icon: Trophy,     label: "Spotlights",    title: "Celebrate the wins",            desc: "Recognition for alumni making a difference in their fields." },
  { icon: Bell,       label: "Notifications", title: "Hear about what you care about", desc: "Jobs, fundraisers, event invites — you choose what reaches you." },
];

const STATS = [
  { end: 5000, suffix: "+",    label: "Alumni registered",     desc: "Verified graduates"          },
  { end: 120,  suffix: "+",    label: "Countries represented", desc: "A truly global network"            },
  { end: 2,    prefix: "GHS ", suffix: "M+", label: "Raised in fundraisers", desc: "Funding labs & scholarships" },
  { end: 300,  suffix: "+",    label: "Jobs posted",           desc: "Roles shared by alumni employers"  },
];

const USE_CASES = [
  {
    icon: Briefcase,
    eyebrow: "Career",
    image: IMG.storyJobs,
    scenario: "The job that never reached a public board",
    desc: "Alumni employers post directly to the portal first — before LinkedIn, before agencies. Being in the network means seeing those roles first.",
  },
  {
    icon: CreditCard,
    eyebrow: "Giving",
    image: IMG.storyGiving,
    scenario: "The fundraiser that needed 200 people",
    desc: "From lab equipment to student bursaries, alumni-led fundraisers pool contributions from graduates across the world.",
  },
  {
    icon: Heart,
    eyebrow: "Mentorship",
    image: IMG.storyMentor,
    scenario: "The mentor who's already done it",
    desc: "Every programme, every career path — there's a graduate ahead of you on that road. The mentorship feature is how you find them.",
  },
];

const HOW_IT_WORKS = [
  { n: "01", icon: Shield, title: "Register in under two minutes", desc: "Create your account with your alumni details. No long forms, no waiting for approval emails." },
  { n: "02", icon: MapPin,  title: "Build out your profile",        desc: "Add your career, company, location. The more context you give, the easier it is for the right people to find you." },
  { n: "03", icon: Zap,    title: "Use it",                         desc: "Browse jobs, back a fundraiser, request a mentor, or just show up in the directory so others can reach you." },
];

/* ─────────────────────────────────────────────────────────────────────────
   HOOKS
   ───────────────────────────────────────────────────────────────────────── */
function scrollToSection(id: string) {
  document.getElementById(id.replace("#", ""))?.scrollIntoView({ behavior: "smooth" });
}

function useFadeUp(threshold = 0.1) {
  const ref = useRef<HTMLDivElement>(null);
  const [visible, setVisible] = useState(false);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const obs = new IntersectionObserver(
      ([e]) => { if (e.isIntersecting) { setVisible(true); obs.disconnect(); } },
      { threshold, rootMargin: "0px 0px -40px 0px" }
    );
    obs.observe(el);
    return () => obs.disconnect();
  }, [threshold]);
  return { ref, visible };
}

function useCounter(end: number, active: boolean) {
  const [count, setCount] = useState(0);
  useEffect(() => {
    if (!active) return;
    const duration = 1800;
    const start = performance.now();
    const tick = (now: number) => {
      const t = Math.min((now - start) / duration, 1);
      const ease = 1 - Math.pow(1 - t, 3);
      setCount(Math.round(ease * end));
      if (t < 1) requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  }, [active, end]);
  return count;
}

/* ─────────────────────────────────────────────────────────────────────────
   PRIMITIVES
   ───────────────────────────────────────────────────────────────────────── */
function Eyebrow({ children }: { children: React.ReactNode }) {
  return (
    <div className="inline-flex items-center gap-2 rounded-full px-3.5 py-1.5 mb-5"
      style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}>
      <span className="w-1.5 h-1.5 rounded-full" style={{ background: "var(--primary)" }} />
      <span className="text-[11px] font-semibold tracking-[0.1em] uppercase" style={{ color: "var(--color-text-info)" }}>
        {children}
      </span>
    </div>
  );
}

function Section({ id, children, className, style }: {
  id?: string; children: React.ReactNode; className?: string; style?: React.CSSProperties;
}) {
  const { ref, visible } = useFadeUp();
  return (
    <section id={id} ref={ref}
      className={cn("transition-all duration-700 ease-out", visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-6", className)}
      style={style}>
      {children}
    </section>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   STAT ROW — for the dark full-bleed stats band (no card chrome)
   ───────────────────────────────────────────────────────────────────────── */
function StatRow({ stat, active, delay, index }: { stat: typeof STATS[number]; active: boolean; delay: string; index: number }) {
  const count = useCounter(stat.end, active);
  const { ref, visible } = useFadeUp();
  return (
    <div ref={ref} style={{ transitionDelay: delay }}
      className={cn("px-5 py-1 sm:px-8 transition-all duration-700", index === 0 && "pl-0", visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-4")}>
      <p className="font-[family-name:var(--font-display)] leading-none tabular-nums break-words mb-2"
        style={{ fontSize: "clamp(1.6rem,3vw,2.1rem)", fontWeight: 700, color: "var(--background)", letterSpacing: "-0.02em" }}>
        {stat.prefix}{count.toLocaleString()}{stat.suffix}
      </p>
      <p className="text-[12.5px] font-medium" style={{ color: "color-mix(in oklch, var(--background) 65%, transparent)" }}>{stat.label}</p>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   FEATURE CARD
   ───────────────────────────────────────────────────────────────────────── */
function FeatureCard({ feature, delay }: { feature: typeof FEATURES[number]; delay: string }) {
  const { ref, visible } = useFadeUp();
  const big = "big" in feature && feature.big;
  return (
    <div ref={ref} style={{ transitionDelay: delay }}
      className={cn(
        "card group transition-all duration-500 hover:-translate-y-1 hover:shadow-sm",
        big && "sm:col-span-2",
        visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-5"
      )}>
      <div className={cn("card__content", big && "sm:flex sm:items-center sm:gap-6")}>
        <div className={cn("w-10 h-10 rounded-[10px] flex items-center justify-center transition-colors duration-200 shrink-0",
          big ? "sm:w-14 sm:h-14 mb-4 sm:mb-0" : "mb-4")}
          style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}>
          <feature.icon size={big ? 20 : 17} className={big ? "sm:size-6" : ""} style={{ color: "var(--primary)" }} />
        </div>
        <div>
          <p className="text-[10px] font-bold tracking-[0.12em] uppercase mb-1.5" style={{ color: "var(--primary)" }}>
            {feature.label}
          </p>
          <h3 className={cn("font-semibold leading-snug mb-2 group-hover:text-primary transition-colors duration-200", big ? "text-[17px]" : "text-[14px]")}
            style={{ color: "var(--foreground)" }}>
            {feature.title}
          </h3>
          <p className={cn("leading-relaxed", big ? "text-[13.5px] max-w-[42ch]" : "text-[13px]")} style={{ color: "var(--muted-foreground)" }}>
            {feature.desc}
          </p>
        </div>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   USE CASE CARD — with photo banner
   ───────────────────────────────────────────────────────────────────────── */
interface UseCaseItem {
  icon: LucideIcon;
  eyebrow: string;
  image: string;
  scenario: string;
  desc: string;
}

function UseCaseCard({ item, delay }: { item: UseCaseItem; delay: string }) {
  const { ref, visible } = useFadeUp();
  return (
    <div ref={ref} style={{ transitionDelay: `${delay}ms` }}
      className={cn("card group overflow-hidden transition-all duration-500 hover:-translate-y-1 hover:shadow-md", visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-5")}>
      {/* Photo */}
      <div className="relative overflow-hidden" style={{ height: 160 }}>
        <img src={item.image} alt={item.eyebrow}
          className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105" />
        <div className="absolute inset-0" style={{ background: "linear-gradient(to top, rgba(0,0,0,0.45) 0%, transparent 60%)" }} />
        <div className="absolute bottom-3 left-4">
          <span className="text-[10px] font-bold tracking-[0.1em] uppercase text-white/80">{item.eyebrow}</span>
        </div>
      </div>
      {/* Body */}
      <div className="card__content">
        <h3 className="font-[family-name:var(--font-display)] leading-snug mb-3"
          style={{ fontSize: "1rem", color: "var(--foreground)" }}>
          {item.scenario}
        </h3>
        <p style={{ fontSize: "0.84rem", color: "var(--muted-foreground)", lineHeight: 1.75 }}>{item.desc}</p>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   HOW-IT-WORKS STEP — sits on the connecting timeline, not a boxed card
   ───────────────────────────────────────────────────────────────────────── */
function HowItWorksStep({ step, delay }: { step: typeof HOW_IT_WORKS[number]; delay: string }) {
  const { ref, visible } = useFadeUp();
  return (
    <div ref={ref}
      className={cn("relative transition-all duration-500", visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-4")}
      style={{ transitionDelay: delay }}>
      {/* Oversized display numeral — the visual anchor, not a generic icon badge */}
      <p
        className="font-[family-name:var(--font-display)] leading-none select-none mb-3"
        style={{ fontSize: "3.75rem", fontWeight: 700, color: "var(--primary)", opacity: 0.14 }}
        aria-hidden="true"
      >
        {step.n}
      </p>
      <div className="flex items-center gap-2 mb-2 -mt-9">
        <step.icon size={15} style={{ color: "var(--primary)" }} />
        <h3 className="text-[16px] font-semibold leading-snug" style={{ color: "var(--foreground)" }}>{step.title}</h3>
      </div>
      <p style={{ fontSize: "0.875rem", color: "var(--muted-foreground)", lineHeight: 1.75 }}>{step.desc}</p>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   ANNOUNCEMENT BANNER
   ───────────────────────────────────────────────────────────────────────── */
function AnnouncementBanner({ banner }: { banner: DynamicNewsBanner }) {
  const [dismissed, setDismissed] = useState(false);
  if (dismissed) return null;
  const isAnchor = banner.linkUrl?.startsWith("#");
  return (
    <div className="relative flex items-center justify-center gap-2.5 px-10 py-2.5 text-center"
      style={{ background: "var(--primary)", color: "white" }}>
      <Star size={11} className="shrink-0 opacity-75" />
      <p className="text-[12.5px] font-medium">
        {banner.text}{" "}
        {banner.linkText && banner.linkUrl && (
          isAnchor ? (
            <button onClick={() => scrollToSection(banner.linkUrl!)}
              className="underline underline-offset-2 font-semibold opacity-90 hover:opacity-100 cursor-pointer">
              {banner.linkText} ↓
            </button>
          ) : (
            <Link href={banner.linkUrl} className="underline underline-offset-2 font-semibold opacity-90 hover:opacity-100">
              {banner.linkText} →
            </Link>
          )
        )}
      </p>
      <button onClick={() => setDismissed(true)}
        className="absolute right-3 top-1/2 -translate-y-1/2 opacity-60 hover:opacity-100 transition-opacity"
        aria-label="Dismiss">
        <X size={13} />
      </button>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PAGE
   ───────────────────────────────────────────────────────────────────────── */
export default function LandingPage() {
  const [menuOpen, setMenuOpen] = useState(false);
  const statsRef = useRef<HTMLDivElement>(null);
  const [statsActive, setStatsActive] = useState(false);
  const content = useLandingContent();

  const stories = useMemo(() => {
    if (!content?.landingPageStories?.length) return USE_CASES;
    return content.landingPageStories.map((s, i) => ({
      icon: STORY_ICONS[s.icon] ?? Star,
      eyebrow: s.eyebrow,
      image: s.imageUrl || STORY_FALLBACK_IMAGES[i % STORY_FALLBACK_IMAGES.length],
      scenario: s.scenario,
      desc: s.description,
    }));
  }, [content]);

  useEffect(() => {
    document.body.style.overflow = menuOpen ? "hidden" : "";
    return () => { document.body.style.overflow = ""; };
  }, [menuOpen]);

  useEffect(() => {
    const el = statsRef.current;
    if (!el) return;
    const obs = new IntersectionObserver(
      ([e]) => { if (e.isIntersecting) { setStatsActive(true); obs.disconnect(); } },
      { threshold: 0.2 }
    );
    obs.observe(el);
    return () => obs.disconnect();
  }, []);

  return (
    <div className="min-h-screen overflow-x-hidden" style={{ background: "var(--background)", color: "var(--foreground)" }}>

      {/* ── Announcement banner ── */}
      {content?.newsBanner?.enabled && content.newsBanner.text && (
        <AnnouncementBanner banner={content.newsBanner} />
      )}

      {/* ════════════════════════════════════════════════════════════════
          NAVBAR
      ════════════════════════════════════════════════════════════════ */}
      <header className="sticky top-0 z-50 border-b backdrop-blur-xl"
        style={{ background: "color-mix(in oklch, var(--background) 86%, transparent)", borderColor: "var(--border)" }}>
        <div className="section__inner flex items-center justify-between h-16 gap-4">

          <Link href="/" className="flex items-center gap-3 shrink-0">
            {content?.logoUrl ? (
              <img src={content.logoUrl} alt={content.displayName ?? "Logo"} className="w-9 h-9 rounded-xl object-cover shrink-0" />
            ) : (
              <div className="w-9 h-9 rounded-xl flex items-center justify-center shrink-0" style={{ background: "var(--primary)" }}>
                <GraduationCap size={17} color="white" />
              </div>
            )}
            <p className="text-[13.5px] font-semibold tracking-tight" style={{ color: "var(--foreground)" }}>{content?.displayName || "Alumni Portal"}</p>
          </Link>

          <nav className="hidden md:flex items-center gap-0.5" aria-label="Primary">
            {NAV_LINKS.map(link => (
              <button key={link.label} onClick={() => scrollToSection(link.href)}
                className="rounded-lg px-4 py-2 text-[13.5px] font-medium transition-colors hover:bg-secondary"
                style={{ color: "var(--muted-foreground)" }}>
                {link.label}
              </button>
            ))}
          </nav>

          <div className="hidden md:flex items-center gap-2">
            <Link href="/login"><Button variant="ghost" size="sm" className="text-[13px] font-medium">Sign in</Button></Link>
            <Link href="/register"><Button size="sm" className="text-[13px] font-semibold gap-1.5">Join now <ArrowRight size={12} /></Button></Link>
          </div>

          <button className="md:hidden flex items-center justify-center w-9 h-9 rounded-lg border transition-colors hover:bg-secondary"
            style={{ borderColor: "var(--border)" }}
            onClick={() => setMenuOpen(o => !o)} aria-label="Toggle navigation" aria-expanded={menuOpen}>
            {menuOpen ? <X size={16} /> : <Menu size={16} />}
          </button>
        </div>

        {menuOpen && (
          <div className="md:hidden fixed inset-x-0 top-16 z-40 flex flex-col px-5 py-8 gap-5 overflow-y-auto h-[calc(100dvh-4rem)]"
            style={{ background: "var(--background)", borderTop: "1px solid var(--border)" }}>
            {NAV_LINKS.map(link => (
              <button key={link.label} onClick={() => { scrollToSection(link.href); setMenuOpen(false); }}
                className="w-full text-left font-[family-name:var(--font-display)] text-[26px] font-semibold transition-colors hover:text-primary"
                style={{ color: "var(--foreground)" }}>
                {link.label}
              </button>
            ))}
            <div className="mt-4 pt-4 flex flex-col gap-2.5" style={{ borderTop: "1px solid var(--border)" }}>
              <Link href="/login" onClick={() => setMenuOpen(false)}><Button variant="outline" className="w-full font-medium">Sign in</Button></Link>
              <Link href="/register" onClick={() => setMenuOpen(false)}>
                <Button className="w-full font-semibold gap-2">Join now <ArrowRight size={14} /></Button>
              </Link>
            </div>
          </div>
        )}
      </header>

      {/* ════════════════════════════════════════════════════════════════
          HERO
      ════════════════════════════════════════════════════════════════ */}
      <div className="relative overflow-hidden" style={{ background: "var(--background)" }}>
        <div className="absolute inset-0 bg-subtle-pattern opacity-[0.45] pointer-events-none" />
        <div className="absolute -top-40 -right-40 w-[640px] h-[640px] rounded-full pointer-events-none"
          style={{ background: "radial-gradient(circle, color-mix(in oklch, var(--primary) 6%, transparent) 0%, transparent 65%)" }} />

        <div className="section__inner--wide relative pt-6 pb-20">
          <div className="grid gap-x-10 gap-y-14 lg:grid-cols-[1.35fr_1fr] items-end">

            {/* Left col */}
            <div className="lg:pr-6">
              <Eyebrow>Verified alumni network</Eyebrow>
              <h1 className="font-[family-name:var(--font-display)] mb-7 max-w-[19ch]"
                style={{ fontSize: "clamp(2.4rem,4.6vw,3.75rem)", fontWeight: 700, lineHeight: 1.12, letterSpacing: "-0.025em", color: "var(--foreground)" }}>
                The alumni portal{" "}
                <span className="relative whitespace-nowrap" style={{ color: "var(--primary)" }}>
                  built
                  <span className="absolute left-0 right-0 -bottom-1 h-[3px] rounded-full" style={{ background: "var(--brand-primary-light)" }} />
                </span>{" "}
                to connect, support, and grow together.
              </h1>
              <p className="mb-10 max-w-[46ch]"
                style={{ fontSize: "clamp(1rem,1.5vw,1.125rem)", lineHeight: 1.75, color: "var(--muted-foreground)" }}>
                One place for jobs, fundraisers, mentorship, events, and verified connections —
                built to feel modern, reliable, and easy for graduates at every stage of their career.
              </p>
              <div className="flex flex-col sm:flex-row gap-3 mb-12">
                <Link href="/register">
                  <Button size="lg" className="w-full sm:w-auto h-12 px-8 text-[15px] font-semibold gap-2">
                    Join the network <ArrowRight size={15} />
                  </Button>
                </Link>
                <Link href="/login">
                  <Button size="lg" variant="outline" className="w-full sm:w-auto h-12 px-8 text-[15px] font-medium">
                    Already a member
                  </Button>
                </Link>
              </div>

              {/* Trust strip — inline, not a grid of boxes */}
              <div className="flex flex-wrap items-center gap-x-7 gap-y-3">
                {[
                  { icon: GraduationCap, text: "Verified access"    },
                  { icon: Briefcase,     text: "Jobs from grads"    },
                  { icon: Heart,         text: "Mentor matching"    },
                  { icon: Globe,         text: "Events & fundraisers" },
                ].map(item => (
                  <div key={item.text} className="flex items-center gap-2 cursor-default">
                    <item.icon size={15} style={{ color: "var(--primary)" }} />
                    <p className="text-[12.5px] font-semibold" style={{ color: "var(--foreground)" }}>{item.text}</p>
                  </div>
                ))}
              </div>
            </div>

            {/* Right — single photo, overlaid stat badge, no nested card list */}
            <div className="relative">
              <div className="relative w-full overflow-hidden rounded-2xl" style={{ height: "min(72vh, 560px)", boxShadow: "0 8px 40px color-mix(in oklch, var(--primary) 12%, transparent)" }}>
                <img src={IMG.heroPanel} alt="Students collaborating"
                  className="absolute inset-0 w-full h-full object-cover" />
                <div className="absolute inset-0"
                  style={{ background: "linear-gradient(200deg, rgba(0,0,0,0.05) 20%, rgba(0,0,0,0.72) 100%)" }} />

                <div className="hidden sm:flex absolute top-5 left-5 items-center gap-2 rounded-full px-3 py-1.5"
                  style={{ background: "rgba(255,255,255,0.15)", backdropFilter: "blur(10px)", border: "1px solid rgba(255,255,255,0.25)" }}>
                  <span className="w-1.5 h-1.5 rounded-full animate-pulse" style={{ background: "#fff" }} />
                  <span className="text-[10.5px] font-semibold tracking-[0.08em] uppercase text-white">Built around alumni needs</span>
                </div>

                <div className="absolute bottom-0 left-0 right-0 p-6">
                  <p className="text-white font-[family-name:var(--font-display)] leading-snug mb-3" style={{ fontSize: "1.35rem" }}>
                    One network.<br />Every graduate, wherever they are.
                  </p>
                  <Link href="/register">
                    <Button className="gap-2 font-semibold">
                      Create your free account <ArrowRight size={14} />
                    </Button>
                  </Link>
                </div>
              </div>

              {/* Floating stat card — inset on the photo at mobile widths, floating clear of it from sm+ */}
              <div className="absolute right-3 top-3 sm:-right-6 sm:-top-6 rounded-xl px-4 py-3 sm:px-5 sm:py-4"
                style={{ background: "var(--background)", border: "1px solid var(--card-border)", boxShadow: "0 10px 30px rgba(0,0,0,0.12)" }}>
                <p className="font-[family-name:var(--font-display)] leading-none mb-1 text-[1.3rem] sm:text-[1.6rem]" style={{ fontWeight: 700, color: "var(--primary)" }}>5,000+</p>
                <p className="text-[10.5px] sm:text-[11.5px] font-medium" style={{ color: "var(--muted-foreground)" }}>Verified graduates</p>
              </div>
            </div>

          </div>
        </div>
      </div>

      {/* ════════════════════════════════════════════════════════════════
          STATS — full-bleed banded row, not a card grid
      ════════════════════════════════════════════════════════════════ */}
      <div className="border-b" style={{ background: "var(--foreground)", borderColor: "var(--border)" }}>
        <div className="section__inner--wide">
          <div className="flex flex-col lg:flex-row lg:items-center gap-10 lg:gap-0 py-14">
            <div className="lg:w-[280px] lg:pr-10 shrink-0">
              <p className="text-[11px] font-semibold tracking-[0.12em] uppercase mb-3" style={{ color: "color-mix(in oklch, var(--background) 55%, transparent)" }}>
                Alumni impact
              </p>
              <h2 className="font-[family-name:var(--font-display)]" style={{ color: "var(--background)", fontSize: "clamp(1.5rem,2.4vw,2rem)", lineHeight: 1.15 }}>
                What alumni are already doing here.
              </h2>
            </div>
            <div ref={statsRef} className="flex-1 grid grid-cols-2 md:grid-cols-4 divide-x" style={{ borderColor: "color-mix(in oklch, var(--background) 15%, transparent)" }}>
              {STATS.map((stat, i) => (
                <StatRow key={stat.label} stat={stat} active={statsActive} delay={`${i * 75}ms`} index={i} />
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* ════════════════════════════════════════════════════════════════
          FEATURES
      ════════════════════════════════════════════════════════════════ */}
      <Section id="features" className="border-b" style={{ background: "var(--muted)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="mb-12 max-w-[56ch]">
            <Eyebrow>What&apos;s inside</Eyebrow>
            <h2 className="font-[family-name:var(--font-display)] mb-4" style={{ color: "var(--foreground)" }}>
              One portal for every alumni need.
            </h2>
            <p style={{ color: "var(--muted-foreground)", fontSize: "1.025rem", lineHeight: 1.75 }}>
              From jobs and mentorship to fundraisers, events, and community connections.
            </p>
          </div>
          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-[var(--space-gap)]">
            {FEATURES.map((feature, i) => (
              <FeatureCard key={feature.title} feature={feature} delay={`${(i % 4) * 65}ms`} />
            ))}
            {/* Filler tile — closes out the bento row instead of leaving a gap */}
            <Link href="/register" className="sm:col-span-2 card group flex items-center justify-between gap-4 p-6 transition-all duration-500 hover:-translate-y-1"
              style={{ background: "var(--primary)", borderColor: "var(--primary)" }}>
              <div>
                <p className="text-[14px] font-semibold text-white mb-1">That&apos;s everything — see it live</p>
                <p className="text-[12.5px]" style={{ color: "color-mix(in oklch, white 75%, transparent)" }}>Create a free account and explore the full portal.</p>
              </div>
              <ArrowRight size={18} className="text-white shrink-0 transition-transform group-hover:translate-x-1" />
            </Link>
          </div>
        </div>
      </Section>

      {/* ════════════════════════════════════════════════════════════════
          STORIES — photo cards
      ════════════════════════════════════════════════════════════════ */}
      <Section id="stories" className="border-b" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="mb-12 max-w-[50ch]">
            <Eyebrow>Real situations</Eyebrow>
            <h2 className="font-[family-name:var(--font-display)] mb-4" style={{ color: "var(--foreground)" }}>
              The three reasons most alumni join.
            </h2>
            <p style={{ color: "var(--muted-foreground)", lineHeight: 1.75 }}>
              These are the most common ways graduates use the portal to move forward.
            </p>
          </div>

          <div className="grid gap-[var(--space-gap)] sm:grid-cols-3">
            {stories.map((item, i) => (
              <div key={item.scenario} className={i === 1 ? "sm:mt-8" : undefined}>
                <UseCaseCard item={item} delay={`${i * 70}ms`} />
              </div>
            ))}
          </div>

          <div className="mt-10 flex justify-center">
            <Link href="/register">
              <Button className="h-11 px-8 font-semibold gap-2">Join the network <ArrowRight size={14} /></Button>
            </Link>
          </div>
        </div>
      </Section>

      {/* ════════════════════════════════════════════════════════════════
          HOW IT WORKS
      ════════════════════════════════════════════════════════════════ */}
      <Section id="how-it-works" style={{ background: "var(--secondary)" }}>
        <div className="section__inner section">
          <div className="text-center mb-12">
            <Eyebrow>Getting started</Eyebrow>
            <h2 className="font-[family-name:var(--font-display)] max-w-[28ch] mx-auto" style={{ color: "var(--foreground)" }}>
              Three steps. That&apos;s all it takes.
            </h2>
          </div>
          <div className="max-w-5xl mx-auto grid gap-10 sm:gap-8 sm:grid-cols-3 sm:divide-x" style={{ borderColor: "var(--border)" }}>
            {HOW_IT_WORKS.map((step, i) => (
              <div key={step.n} className={i > 0 ? "sm:pl-8" : undefined}>
                <HowItWorksStep step={step} delay={`${i * 100}ms`} />
              </div>
            ))}
          </div>
        </div>
      </Section>

      {/* ════════════════════════════════════════════════════════════════
          FINAL CTA
      ════════════════════════════════════════════════════════════════ */}
      <Section style={{ background: "var(--primary)" }}>
        <div className="relative overflow-hidden">
          <div className="absolute -top-24 -left-24 w-96 h-96 rounded-full pointer-events-none"
            style={{ background: "radial-gradient(circle, rgba(255,255,255,0.08), transparent 70%)" }} />
          <div className="absolute -bottom-32 -right-16 w-[420px] h-[420px] rounded-full pointer-events-none"
            style={{ background: "radial-gradient(circle, rgba(255,255,255,0.06), transparent 70%)" }} />

          <div className="section__inner--wide relative py-20 sm:py-24">
            <div className="grid gap-10 lg:grid-cols-[1.2fr_auto] items-end">
              <div>
                <p className="text-[11px] font-semibold tracking-[0.12em] uppercase mb-4" style={{ color: "rgba(255,255,255,0.65)" }}>
                  Take the next step
                </p>
                <h2 className="font-[family-name:var(--font-display)] mb-5 max-w-[16ch]"
                  style={{ fontSize: "clamp(2rem,4.2vw,3.4rem)", lineHeight: 1.06, color: "white" }}>
                  Your journey shaped you. Now shape what comes next.
                </h2>
                <p className="max-w-[46ch]" style={{ fontSize: "1.025rem", lineHeight: 1.75, color: "rgba(255,255,255,0.8)" }}>
                  Join thousands of alumni already using the portal to connect, contribute, and grow with trusted peers.
                </p>
              </div>
              <div className="flex flex-col sm:flex-row lg:flex-col items-stretch gap-3 shrink-0">
                <Link href="/register">
                  <Button size="lg" className="w-full h-12 px-10 text-[15px] font-semibold gap-2"
                    style={{ background: "white", color: "var(--primary)" }}>
                    Create my account <ChevronRight size={16} />
                  </Button>
                </Link>
                <Link href="/login">
                  <Button size="lg" variant="outline" className="w-full h-12 px-9 text-[15px] font-medium"
                    style={{ borderColor: "rgba(255,255,255,0.35)", color: "white", background: "transparent" }}>
                    Sign in instead
                  </Button>
                </Link>
              </div>
            </div>
          </div>
        </div>
      </Section>

      {/* ════════════════════════════════════════════════════════════════
          FOOTER
      ════════════════════════════════════════════════════════════════ */}
      <footer className="border-t py-9" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
        <div className="section__inner flex flex-col sm:flex-row items-center justify-between gap-5">
          <Link href="/" className="flex items-center gap-3">
            {content?.logoUrl ? (
              <img src={content.logoUrl} alt={content.displayName ?? "Logo"} className="w-8 h-8 rounded-xl object-cover shrink-0" />
            ) : (
              <div className="w-8 h-8 rounded-xl flex items-center justify-center shrink-0" style={{ background: "var(--primary)" }}>
                <GraduationCap size={14} color="white" />
              </div>
            )}
            <span className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>{content?.displayName || "Alumni Portal"}</span>
          </Link>

          <p className="text-[11.5px] text-center" style={{ color: "var(--muted-foreground)", opacity: 0.75 }}>
            © {new Date().getFullYear()} {content?.displayName || "Alumni Portal"}
          </p>

          <nav className="flex flex-wrap items-center justify-center gap-5" aria-label="Footer links">
            {[
              { label: "Sign in",   href: "/login"    },
              { label: "Join now",  href: "/register" },
              { label: "Terms",     href: "/terms"    },
              { label: "Privacy",   href: "/privacy"  },
            ].map(link => (
              <Link key={link.label} href={link.href}
                className="text-[12.5px] font-medium transition-colors hover:text-foreground"
                style={{ color: "var(--muted-foreground)" }}>
                {link.label}
              </Link>
            ))}
          </nav>
        </div>
      </footer>

    </div>
  );
}
