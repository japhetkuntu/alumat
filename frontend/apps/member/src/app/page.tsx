"use client";

import Link from "next/link";
import Image from "next/image";
import { useState, useEffect, useRef } from "react";
import {
  GraduationCap, Users, Briefcase, Heart, Globe,
  Menu, X, ArrowRight, ChevronRight,
  BookOpen, Trophy, CreditCard, Bell,
  MapPin, Zap, Shield, Star, Award, ChevronDown,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/* ─────────────────────────────────────────────────────────────────────────
   UNSPLASH IMAGE URLS
   All free-to-use under the Unsplash License — no attribution required.
   ───────────────────────────────────────────────────────────────────────── */
const IMG = {
  // Spotlight banners — professional Black men in formal settings
  vcIncoming: "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=800&q=80",   // confident man portrait by Fortune Vieyra
  vcOutgoing: "https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?auto=format&fit=crop&w=800&q=80",   // professional man in suit by Adeolu Eletu
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
  { icon: Briefcase,  label: "Careers",       title: "Jobs inside the network",       desc: "Roles posted by alumni employers before they reach public boards." },
  { icon: Users,      label: "Directory",     title: "Find any grad in seconds",      desc: "Search by class year, department, company, or country." },
  { icon: CreditCard, label: "Contributions", title: "Fund what matters",             desc: "Alumni-led campaigns for labs, scholarships, and campus improvements." },
  { icon: BookOpen,   label: "Class Notes",   title: "Keep the conversation going",   desc: "Post milestones, share knowledge, trade stories by graduation year." },
  { icon: Globe,      label: "Events",        title: "Never miss a reunion",          desc: "Homecomings, webinars, networking nights — RSVP in one place." },
  { icon: Heart,      label: "Mentorship",    title: "Give back. Get ahead.",         desc: "Connect with alumni who've already done what you're trying to do." },
  { icon: Trophy,     label: "Spotlights",    title: "Celebrate the wins",            desc: "Recognition for alumni making a difference in their fields." },
  { icon: Bell,       label: "Notifications", title: "Hear about what you care about", desc: "Jobs, campaigns, event invites — you choose what reaches you." },
];

const STATS = [
  { end: 5000, suffix: "+",    label: "Alumni registered",     desc: "Verified UMaT graduates"          },
  { end: 120,  suffix: "+",    label: "Countries represented", desc: "A truly global network"            },
  { end: 2,    prefix: "GHS ", suffix: "M+", label: "Raised in campaigns", desc: "Funding labs & scholarships" },
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
    scenario: "The campaign that needed 200 people",
    desc: "From lab equipment to student bursaries, alumni-led campaigns pool contributions from graduates across the world.",
  },
  {
    icon: Heart,
    eyebrow: "Mentorship",
    image: IMG.storyMentor,
    scenario: "The mentor who's already done it",
    desc: "Every programme, every career path — there's a UMaT graduate ahead of you on that road. The mentorship feature is how you find them.",
  },
];

const HOW_IT_WORKS = [
  { n: "01", icon: Shield, title: "Register in under two minutes", desc: "Create your account with your UMaT details. No long forms, no waiting for approval emails." },
  { n: "02", icon: MapPin,  title: "Build out your profile",        desc: "Add your career, company, location. The more context you give, the easier it is for the right people to find you." },
  { n: "03", icon: Zap,    title: "Use it",                         desc: "Browse jobs, back a campaign, request a mentor, or just show up in the directory so others can reach you." },
];

const SPOTLIGHTS = [
  {
    type: "incoming" as const,
    badge: "New appointment",
    badgeIcon: Star,
    image: IMG.vcIncoming,
    name: "Prof. Bernard Kumi-Boateng",
    role: "Vice Chancellor-Elect · Effective 1 August 2026",
    teaser: "A UMaT PhD alumnus appointed to lead the very university he graduated from.",
    full: "Prof. Bernard Kumi-Boateng earned his PhD in Geomatic Engineering right here at UMaT in 2012, and has spent nearly two decades building the institution he once studied at — rising from Lecturer to Full Professor, serving as Head of Department, Dean of Students, Dean of Planning and Quality Assurance, and most recently as Dean of the Faculty of Geosciences and Environmental Studies. On 13 April 2026, the University Council appointed him as Vice Chancellor effective 1 August 2026, following a rigorous national and international search. He is a Licensed Surveyor, a Fellow of WAIMM, and a Visiting Professor at universities in The Gambia and Kenya.",
    readMoreUrl: "https://umat.edu.gh/media-press/news/professor-bernard-kumi-boateng-appointed-vice-chancellor-of-the-university-of-mines-and-technology",
  },
  {
    type: "outgoing" as const,
    badge: "A tribute",
    badgeIcon: Award,
    image: IMG.vcOutgoing,
    name: "Prof. Richard Kwesi Amankwah",
    role: "Vice Chancellor · 2020–2026",
    teaser: "Six years of steady, transformative leadership. Thank you, Prof. Amankwah.",
    full: "Prof. Richard Kwesi Amankwah took office in August 2020 and leaves behind a university with stronger research partnerships, deeper industry ties, and a higher international profile. A Professor of Minerals Engineering with a PhD from Queen's University, Canada, he helped raise approximately $10 million in research funding, held visiting professorships across Africa, and was recognised as a 'Lecturer Icon' by the Students Representative Council. The entire alumni community thanks him for six years of dedicated service.",
    readMoreUrl: "https://umat.edu.gh/the-vice-chancellor",
  },
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
   STAT CARD
   ───────────────────────────────────────────────────────────────────────── */
function StatCard({ stat, active, delay }: { stat: typeof STATS[number]; active: boolean; delay: string }) {
  const count = useCounter(stat.end, active);
  const { ref, visible } = useFadeUp();
  return (
    <div ref={ref} style={{ transitionDelay: delay }}
      className={cn("card card__content transition-all duration-700 hover:-translate-y-1", visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-5")}>
      <div className="mb-4">
        {stat.prefix && (
          <p className="text-[11px] font-semibold tracking-wide uppercase mb-0.5" style={{ color: "var(--primary)", opacity: 0.7 }}>
            {stat.prefix}
          </p>
        )}
        <p className="font-[family-name:var(--font-display)] leading-none tabular-nums break-words"
          style={{ fontSize: "clamp(1.8rem,3.5vw,2.4rem)", fontWeight: 700, color: "var(--primary)", letterSpacing: "-0.02em" }}>
          {count.toLocaleString()}{stat.suffix}
        </p>
      </div>
      <div className="pt-4 border-t" style={{ borderColor: "var(--border)" }}>
        <p className="text-[14px] font-semibold mb-0.5" style={{ color: "var(--foreground)" }}>{stat.label}</p>
        {stat.desc && <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>{stat.desc}</p>}
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   FEATURE CARD
   ───────────────────────────────────────────────────────────────────────── */
function FeatureCard({ feature, delay }: { feature: typeof FEATURES[number]; delay: string }) {
  const { ref, visible } = useFadeUp();
  return (
    <div ref={ref} style={{ transitionDelay: delay }}
      className={cn("card group transition-all duration-500 hover:-translate-y-1 hover:shadow-sm", visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-5")}>
      <div className="card__content">
        <div className="w-10 h-10 rounded-[10px] flex items-center justify-center mb-4 transition-colors duration-200"
          style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}>
          <feature.icon size={17} style={{ color: "var(--primary)" }} />
        </div>
        <p className="text-[10px] font-bold tracking-[0.12em] uppercase mb-1.5" style={{ color: "var(--primary)" }}>
          {feature.label}
        </p>
        <h3 className="text-[14px] font-semibold leading-snug mb-2 group-hover:text-primary transition-colors duration-200"
          style={{ color: "var(--foreground)" }}>
          {feature.title}
        </h3>
        <p className="text-[13px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
          {feature.desc}
        </p>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   SPOTLIGHT CARD — real photo banner + brief text + expand
   ───────────────────────────────────────────────────────────────────────── */
function SpotlightCard({ item, delay }: { item: typeof SPOTLIGHTS[number]; delay: string }) {
  const { ref, visible } = useFadeUp();
  const [expanded, setExpanded] = useState(false);
  const BadgeIcon = item.badgeIcon;
  const isIncoming = item.type === "incoming";

  return (
    <div ref={ref} style={{ transitionDelay: delay }}
      className={cn("overflow-hidden rounded-2xl border transition-all duration-700 hover:shadow-md", visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-6")}
      style={{ borderColor: isIncoming ? "var(--color-border-info)" : "var(--border)", background: "var(--background)" }}>

      {/* ── Photo banner ── */}
      <div className="relative w-full overflow-hidden" style={{ height: 210 }}>
        <img
          src={item.image}
          alt={item.name}
          className="w-full h-full object-cover object-center transition-transform duration-700 hover:scale-105"
        />
        {/* Gradient scrim — bottom-heavy so name remains readable */}
        <div className="absolute inset-0" style={{ background: "linear-gradient(to top, rgba(0,0,0,0.68) 0%, rgba(0,0,0,0.1) 55%, transparent 100%)" }} />

        {/* Name + role overlaid on photo */}
        <div className="absolute bottom-0 left-0 right-0 p-4">
          <p className="text-[15px] font-semibold text-white leading-snug mb-0.5">{item.name}</p>
          <p className="text-[12px] text-white/75 font-medium">{item.role}</p>
        </div>

        {/* Badge — top left */}
        <div className="absolute top-3 left-3">
          <div className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1"
            style={{ background: "rgba(255,255,255,0.15)", backdropFilter: "blur(10px)", border: "1px solid rgba(255,255,255,0.25)" }}>
            <BadgeIcon size={10} color="white" />
            <span className="text-[10px] font-semibold tracking-wide text-white uppercase">{item.badge}</span>
          </div>
        </div>
      </div>

      {/* ── Card body ── */}
      <div className="p-5">
        <p className="text-[13.5px] leading-relaxed mb-4" style={{ color: "var(--muted-foreground)" }}>
          {item.teaser}
        </p>

        {expanded && (
          <p className="text-[13px] leading-[1.8] mb-4 pt-3 border-t" style={{ color: "var(--muted-foreground)", borderColor: "var(--border)" }}>
            {item.full}
          </p>
        )}

        <button
          onClick={() => setExpanded(e => !e)}
          className="flex items-center gap-1.5 text-[12.5px] font-semibold transition-opacity hover:opacity-70"
          style={{ color: "var(--primary)" }}
        >
          {expanded ? "Show less" : "Read more"}
          <ChevronDown size={13} className={cn("transition-transform duration-200", expanded && "rotate-180")} />
        </button>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   USE CASE CARD — with photo banner
   ───────────────────────────────────────────────────────────────────────── */
function UseCaseCard({ item, delay }: { item: typeof USE_CASES[number]; delay: string }) {
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
   ANNOUNCEMENT BANNER
   ───────────────────────────────────────────────────────────────────────── */
function AnnouncementBanner() {
  const [dismissed, setDismissed] = useState(false);
  if (dismissed) return null;
  return (
    <div className="relative flex items-center justify-center gap-2.5 px-10 py-2.5 text-center"
      style={{ background: "var(--primary)", color: "white" }}>
      <Star size={11} className="shrink-0 opacity-75" />
      <p className="text-[12.5px] font-medium">
        Prof. Bernard Kumi-Boateng appointed new UMaT Vice Chancellor — effective 1 August 2026.{" "}
        <button onClick={() => scrollToSection("#spotlight")}
          className="underline underline-offset-2 font-semibold opacity-90 hover:opacity-100 cursor-pointer">
          See the spotlight ↓
        </button>
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
      <AnnouncementBanner />

      {/* ════════════════════════════════════════════════════════════════
          NAVBAR
      ════════════════════════════════════════════════════════════════ */}
      <header className="sticky top-0 z-50 border-b" style={{ background: "rgba(255,255,255,0.97)", borderColor: "var(--border)" }}>
        <div className="section__inner flex items-center justify-between h-16 gap-4">

          <Link href="/" className="flex items-center gap-3 shrink-0">
            <div className="w-9 h-9 rounded-xl flex items-center justify-center shrink-0" style={{ background: "var(--primary)" }}>
              <GraduationCap size={17} color="white" />
            </div>
            <p className="text-[13.5px] font-semibold tracking-tight" style={{ color: "var(--foreground)" }}>UMaT Alumni</p>
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
          <div className="md:hidden fixed inset-0 top-16 z-40 flex flex-col px-5 py-5 gap-1 overflow-y-auto"
            style={{ background: "rgba(255,255,255,0.97)", borderTop: "1px solid var(--border)" }}>
            {NAV_LINKS.map(link => (
              <button key={link.label} onClick={() => { scrollToSection(link.href); setMenuOpen(false); }}
                className="w-full text-left rounded-xl px-4 py-3.5 text-base font-medium transition-colors hover:bg-secondary"
                style={{ color: "var(--muted-foreground)" }}>
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

        <div className="section__inner relative pt-2 pb-24">
          <div className="grid gap-12 lg:grid-cols-[1.1fr_0.9fr] items-start">

            {/* Left col */}
            <div>
              <Eyebrow>Verified UMaT alumni network</Eyebrow>
              <h1 className="font-[family-name:var(--font-display)] mb-6"
                style={{ fontSize: "clamp(2.4rem,5.5vw,4.2rem)", fontWeight: 700, lineHeight: 1.06, letterSpacing: "-0.025em", color: "var(--foreground)" }}>
                The alumni portal{" "}
                <span className="relative" style={{ color: "var(--primary)" }}>
                  built
                  <span className="absolute left-0 right-0 -bottom-1 h-[3px] rounded-full" style={{ background: "var(--brand-primary-light)" }} />
                </span>{" "}
                to connect, support, and grow together.
              </h1>
              <p className="mb-9 max-w-[52ch]"
                style={{ fontSize: "clamp(0.975rem,1.5vw,1.075rem)", lineHeight: 1.8, color: "var(--muted-foreground)" }}>
                One place for jobs, campaigns, mentorship, events, and verified connections —
                built to feel modern, reliable, and easy for graduates at every stage of their career.
              </p>
              <div className="flex flex-col sm:flex-row gap-3 mb-10">
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

              {/* Trust tiles */}
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                {[
                  { icon: GraduationCap, text: "Verified access"    },
                  { icon: Briefcase,     text: "Jobs from grads"    },
                  { icon: Heart,         text: "Mentor matching"    },
                  { icon: Globe,         text: "Events & campaigns" },
                ].map(item => (
                  <div key={item.text} className="card flex flex-col gap-3 p-4 transition-all duration-200 hover:-translate-y-0.5 cursor-default">
                    <div className="w-8 h-8 rounded-[8px] flex items-center justify-center shrink-0"
                      style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}>
                      <item.icon size={14} style={{ color: "var(--primary)" }} />
                    </div>
                    <p className="text-[12.5px] font-semibold leading-snug" style={{ color: "var(--foreground)" }}>{item.text}</p>
                  </div>
                ))}
              </div>
            </div>

            {/* Right — photo card */}
            <div className="card overflow-hidden flex flex-col lg:sticky lg:top-24"
              style={{ boxShadow: "0 4px 32px color-mix(in oklch, var(--primary) 8%, transparent)" }}>

              {/* Photo — fixed height, fills width */}
              <div className="relative w-full shrink-0 overflow-hidden" style={{ height: 200 }}>
                <img src={IMG.heroPanel} alt="UMaT students collaborating"
                  className="absolute inset-0 w-full h-full object-cover" />
                <div className="absolute inset-0"
                  style={{ background: "linear-gradient(to top, rgba(0,0,0,0.55) 0%, transparent 55%)" }} />
                <div className="absolute bottom-4 left-5 right-5">
                  <p className="text-white text-[13px] font-medium leading-snug">
                    One network. Every graduate, wherever they are.
                  </p>
                </div>
              </div>

              {/* Body — grows to fill remaining card height */}
              <div className="card__content flex flex-col flex-1">

                {/* Live indicator */}
                <div className="flex items-center gap-2 mb-5">
                  <span className="w-2 h-2 rounded-full animate-pulse" style={{ background: "var(--primary)" }} />
                  <span className="text-[11px] font-semibold tracking-[0.1em] uppercase"
                    style={{ color: "var(--muted-foreground)" }}>
                    Built around alumni needs
                  </span>
                </div>

                {/* Feature rows — flex-1 so they spread */}
                <div className="flex flex-col flex-1" style={{ borderColor: "var(--border)" }}>
                  {[
                    {
                      icon: Briefcase,
                      title: "Everything in one place",
                      body: "Jobs, mentorship, contributions, events — all in one tab.",
                    },
                    {
                      icon: Bell,
                      title: "Control what you see",
                      body: "Choose the updates, campaigns, and connections that matter.",
                    },
                    {
                      icon: Shield,
                      title: "Trusted, verified network",
                      body: "Every member is part of the UMaT community.",
                    },
                  ].map((item, idx) => (
                    <div key={item.title}
                      className={cn("flex items-start gap-3 py-4", idx < 2 && "border-b")}
                      style={{ borderColor: "var(--border)" }}>
                      <div className="shrink-0 w-8 h-8 rounded-lg flex items-center justify-center mt-0.5"
                        style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}>
                        <item.icon size={14} style={{ color: "var(--primary)" }} />
                      </div>
                      <div>
                        <p className="text-[13.5px] font-semibold leading-snug mb-0.5"
                          style={{ color: "var(--foreground)" }}>{item.title}</p>
                        <p className="text-[12.5px] leading-relaxed"
                          style={{ color: "var(--muted-foreground)" }}>{item.body}</p>
                      </div>
                    </div>
                  ))}
                </div>

                {/* CTA — pinned to bottom */}
                <div className="pt-5 mt-2 border-t" style={{ borderColor: "var(--border)" }}>
                  <Link href="/register">
                    <Button className="w-full gap-2 font-semibold">
                      Create your free account <ArrowRight size={14} />
                    </Button>
                  </Link>
                </div>

              </div>
            </div>

          </div>
        </div>
      </div>

      {/* ════════════════════════════════════════════════════════════════
          SPOTLIGHT — VC leadership transition
      ════════════════════════════════════════════════════════════════ */}
      <Section id="spotlight" className="border-y" style={{ background: "var(--secondary)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="mb-10 max-w-[52ch]">
            <Eyebrow>Alumni in leadership</Eyebrow>
            <h2 className="font-[family-name:var(--font-display)] mb-4" style={{ color: "var(--foreground)" }}>
              A UMaT graduate will lead UMaT.
            </h2>
            <p style={{ color: "var(--muted-foreground)", lineHeight: 1.75 }}>
              Prof. Bernard Kumi-Boateng — who earned his PhD at UMaT — has been appointed Vice Chancellor
              from 1 August 2026. We also take a moment to thank his predecessor.
            </p>
          </div>

          <div className="grid gap-6 sm:grid-cols-2">
            {SPOTLIGHTS.map((item, i) => (
              <SpotlightCard key={item.name} item={item} delay={`${i * 100}ms`} />
            ))}
          </div>

          <div className="mt-8 flex items-start gap-3 rounded-xl border p-4"
            style={{ background: "var(--color-background-info)", borderColor: "var(--color-border-info)" }}>
            <GraduationCap size={16} className="shrink-0 mt-0.5" style={{ color: "var(--primary)" }} />
            <p className="text-[13px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
              <span className="font-semibold" style={{ color: "var(--foreground)" }}>Are you an alumnus in a leadership role?</span>{" "}
              The alumni spotlight celebrates your achievements across the network.{" "}
              <Link href="/register" className="font-semibold underline underline-offset-2" style={{ color: "var(--primary)" }}>
                Join to be featured.
              </Link>
            </p>
          </div>
        </div>
      </Section>

      {/* ════════════════════════════════════════════════════════════════
          STATS
      ════════════════════════════════════════════════════════════════ */}
      <div className="border-b" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="text-center mb-12">
            <Eyebrow>Alumni impact</Eyebrow>
            <h2 className="font-[family-name:var(--font-display)]" style={{ color: "var(--foreground)" }}>
              What UMaT alumni are already doing here.
            </h2>
          </div>
          <div ref={statsRef} className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-[var(--space-gap)]">
            {STATS.map((stat, i) => (
              <StatCard key={stat.label} stat={stat} active={statsActive} delay={`${i * 75}ms`} />
            ))}
          </div>
        </div>
      </div>

      {/* ════════════════════════════════════════════════════════════════
          FEATURES
      ════════════════════════════════════════════════════════════════ */}
      <Section id="features" className="border-b" style={{ background: "var(--muted)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="mb-12 max-w-[56ch]">
            <Eyebrow>What's inside</Eyebrow>
            <h2 className="font-[family-name:var(--font-display)] mb-4" style={{ color: "var(--foreground)" }}>
              One portal for every alumni need.
            </h2>
            <p style={{ color: "var(--muted-foreground)", fontSize: "1.025rem", lineHeight: 1.75 }}>
              From jobs and mentorship to campaigns, events, and community connections.
            </p>
          </div>
          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-[var(--space-gap)]">
            {FEATURES.map((feature, i) => (
              <FeatureCard key={feature.title} feature={feature} delay={`${(i % 4) * 65}ms`} />
            ))}
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
              These are the most common ways UMaT graduates use the portal to move forward.
            </p>
          </div>

          <div className="grid gap-[var(--space-gap)] sm:grid-cols-3">
            {USE_CASES.map((item, i) => (
              <UseCaseCard key={item.scenario} item={item} delay={i * 70} />
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
              Three steps. That's all it takes.
            </h2>
          </div>
          <div className="max-w-4xl mx-auto grid gap-[var(--space-gap)] sm:grid-cols-3">
            {HOW_IT_WORKS.map((step, i) => (
              <div key={step.n} className="card transition-all duration-300 hover:-translate-y-1" style={{ transitionDelay: `${i * 75}ms` }}>
                <div className="card__content">
                  <p className="font-[family-name:var(--font-display)] leading-none mb-5 select-none"
                    style={{ fontSize: "3.5rem", fontWeight: 700, letterSpacing: "-0.03em", color: "var(--brand-primary-light)" }}>
                    {step.n}
                  </p>
                  <div className="w-9 h-9 rounded-[10px] flex items-center justify-center mb-4"
                    style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}>
                    <step.icon size={16} style={{ color: "var(--primary)" }} />
                  </div>
                  <h3 className="text-[15px] font-semibold mb-2 leading-snug" style={{ color: "var(--foreground)" }}>{step.title}</h3>
                  <p style={{ fontSize: "0.84rem", color: "var(--muted-foreground)", lineHeight: 1.75 }}>{step.desc}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </Section>

      {/* ════════════════════════════════════════════════════════════════
          FINAL CTA
      ════════════════════════════════════════════════════════════════ */}
      <Section className="border-t" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="relative overflow-hidden rounded-2xl border text-center"
            style={{ background: "var(--background)", borderColor: "var(--card-border)", boxShadow: "0 4px 40px color-mix(in oklch, var(--primary) 7%, transparent)" }}>
            <div className="absolute top-0 left-0 right-0 h-[3px]"
              style={{ background: "linear-gradient(90deg, transparent, var(--primary), transparent)" }} />
            <div className="absolute -top-20 left-1/2 -translate-x-1/2 w-80 h-52 rounded-full pointer-events-none"
              style={{ background: "radial-gradient(ellipse, color-mix(in oklch, var(--primary) 6%, transparent), transparent 70%)" }} />

            <div className="relative px-8 py-14 sm:px-16 sm:py-16 flex flex-col items-center">
              <Eyebrow>Take the next step</Eyebrow>
              <h2 className="font-[family-name:var(--font-display)] mb-5 mx-auto max-w-[22ch] text-center"
                style={{ fontSize: "clamp(1.9rem,4vw,3.2rem)", lineHeight: 1.1, color: "var(--foreground)" }}>
                UMaT shaped you.{" "}
                <span style={{ color: "var(--primary)" }}>Now shape what comes next.</span>
              </h2>
              <p className="mx-auto max-w-[46ch] mb-9"
                style={{ fontSize: "1.025rem", lineHeight: 1.75, color: "var(--muted-foreground)" }}>
                Join thousands of alumni already using the portal to connect, contribute, and grow with trusted peers.
              </p>
              <div className="flex flex-col sm:flex-row items-center justify-center gap-3">
                <Link href="/register">
                  <Button size="lg" className="w-full sm:w-auto h-12 px-10 text-[15px] font-semibold gap-2">
                    Create my account <ChevronRight size={16} />
                  </Button>
                </Link>
                <Link href="/login">
                  <Button size="lg" variant="ghost" className="w-full sm:w-auto h-12 px-9 text-[15px] font-medium">
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
            <div className="w-8 h-8 rounded-xl flex items-center justify-center shrink-0" style={{ background: "var(--primary)" }}>
              <GraduationCap size={14} color="white" />
            </div>
            <span className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>UMaT Alumni Portal</span>
          </Link>

          <p className="text-[11.5px] text-center" style={{ color: "var(--muted-foreground)", opacity: 0.75 }}>
            © {new Date().getFullYear()} University of Mines and Technology Alumni Association
          </p>

          <nav className="flex flex-wrap items-center justify-center gap-5" aria-label="Footer links">
            {[
              { label: "Sign in",   href: "/login"            },
              { label: "Join now",  href: "/register"         },
              { label: "Campaigns", href: "/payment-campaign" },
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
