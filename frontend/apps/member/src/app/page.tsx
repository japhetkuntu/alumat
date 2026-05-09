"use client";

import Link from "next/link";
import { useState, useEffect, useRef } from "react";
import {
  GraduationCap, Users, Briefcase, Heart, Globe,
  Menu, X, ArrowRight, ChevronRight,
  BookOpen, Trophy, CreditCard, Bell,
  MapPin, Zap, Shield,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/* ─────────────────────────────────────────────────────────────────────────
   DATA
   ───────────────────────────────────────────────────────────────────────── */

const NAV_LINKS = [
  { label: "What's inside",  href: "#features"     },
  { label: "See it work",    href: "#stories"      },
  { label: "Start in 2 min", href: "#how-it-works" },
];

const FEATURES = [
  {
    icon: Briefcase,
    label: "Careers",
    title: "Jobs shared inside the network",
    desc: "Roles posted by alumni employers — often before they reach public boards.",
  },
  {
    icon: Users,
    label: "Directory",
    title: "Find any grad in seconds",
    desc: "Search by class year, department, company, or country.",
  },
  {
    icon: CreditCard,
    label: "Contributions",
    title: "Pay dues. Fund what matters.",
    desc: "Alumni-led campaigns for labs, scholarships, and campus improvements.",
  },
  {
    icon: BookOpen,
    label: "Class Notes",
    title: "Keep the conversation going",
    desc: "Post milestones, share knowledge, trade stories by graduation class.",
  },
  {
    icon: Globe,
    label: "Events",
    title: "Never miss a reunion",
    desc: "Homecomings, webinars, networking nights — RSVP in one place.",
  },
  {
    icon: Heart,
    label: "Mentorship",
    title: "Give back. Get ahead.",
    desc: "Connect with alumni who've done what you're trying to do.",
  },
  {
    icon: Trophy,
    label: "Spotlights",
    title: "Celebrate the wins",
    desc: "Leaderboards and recognition for alumni who give back the most.",
  },
  {
    icon: Bell,
    label: "Notifications",
    title: "Hear about what you care about",
    desc: "Jobs, campaigns, event invites — you choose what reaches you.",
  },
];

const USE_CASES = [
  {
    icon: Briefcase,
    eyebrow: "Career",
    scenario: "The job that never reached a public board",
    desc: "Alumni employers post directly to the portal first — before LinkedIn, before agencies. Being in the network means seeing those roles before anyone else does.",
  },
  {
    icon: CreditCard,
    eyebrow: "Giving",
    scenario: "The campaign that needed 200 people",
    desc: "From lab equipment to student bursaries, alumni-led campaigns pool contributions from graduates across the world — even those who haven't visited campus in years.",
  },
  {
    icon: Heart,
    eyebrow: "Mentorship",
    scenario: "The mentor who's already done what you're trying to do",
    desc: "Every programme, every career path — there's a UMaT graduate ahead of you on that road. The mentorship feature is how you find them.",
  },
];

const STATS = [
  { end: 5000, suffix: "+",    label: "Alumni registered",    desc: "Verified UMaT graduates"         },
  { end: 120,  suffix: "+",    label: "Countries represented", desc: "A truly global network"           },
  { end: 2,    prefix: "GHS ", suffix: "M+", label: "Raised in campaigns", desc: "Funding labs & scholarships" },
  { end: 300,  suffix: "+",    label: "Jobs posted",           desc: "Roles shared by alumni employers" },
];

const HOW_IT_WORKS = [
  {
    n: "01",
    icon: Shield,
    title: "Register in under two minutes",
    desc: "Create your account with your UMaT details. No long forms, no waiting for approval emails.",
  },
  {
    n: "02",
    icon: MapPin,
    title: "Build out your profile",
    desc: "Add your career, company, location. The more context you give, the easier it is for the right people to find you.",
  },
  {
    n: "03",
    icon: Zap,
    title: "Use it",
    desc: "Browse jobs, back a campaign, request a mentor, or just show up in the directory so others can reach you.",
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
    const duration = 1600;
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

/** Section eyebrow label — uses brand info color tokens */
function Eyebrow({ children }: { children: React.ReactNode }) {
  return (
    <div
      className="inline-flex items-center gap-2 rounded-full px-3.5 py-1.5 mb-5"
      style={{
        background: "var(--color-background-info)",
        border: "1px solid var(--color-border-info)",
      }}
    >
      <span className="w-1.5 h-1.5 rounded-full" style={{ background: "var(--primary)" }} />
      <span
        className="text-[11px] font-semibold tracking-[0.1em] uppercase"
        style={{ color: "var(--color-text-info)" }}
      >
        {children}
      </span>
    </div>
  );
}

/** Section wrapper with scroll-triggered fade-up */
function Section({
  id,
  children,
  className,
  style,
}: {
  id?: string;
  children: React.ReactNode;
  className?: string;
  style?: React.CSSProperties;
}) {
  const { ref, visible } = useFadeUp();
  return (
    <section
      id={id}
      ref={ref}
      className={cn(
        "transition-all duration-700 ease-out",
        visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-6",
        className
      )}
      style={style}
    >
      {children}
    </section>
  );
}

/** Animated stat counter card */
function StatCard({
  stat,
  active,
  delay,
}: {
  stat: (typeof STATS)[number];
  active: boolean;
  delay: string;
}) {
  const count = useCounter(stat.end, active);
  const { ref, visible } = useFadeUp();
  return (
    <div
      ref={ref}
      style={{ transitionDelay: delay }}
      className={cn(
        "card card__content group transition-all duration-700 hover:-translate-y-1",
        visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-5"
      )}
    >
      <p
        className="font-[family-name:var(--font-display)] leading-none tabular-nums mb-4"
        style={{
          fontSize: "clamp(2.4rem,4.5vw,1rem)",
          fontWeight: 700,
          color: "var(--primary)",
          letterSpacing: "-0.03em",
        }}
      >
        {stat.prefix ?? ""}{count.toLocaleString()}{stat.suffix}
      </p>
      <div className="pt-4 border-t" style={{ borderColor: "var(--border)" }}>
        <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
          {stat.label}
        </p>
        {stat.desc && (
          <p className="mt-0.5 text-[12px]" style={{ color: "var(--muted-foreground)" }}>
            {stat.desc}
          </p>
        )}
      </div>
    </div>
  );
}

/** Feature card for the 8-up grid */
function FeatureCard({
  feature,
  delay,
}: {
  feature: (typeof FEATURES)[number];
  delay: string;
}) {
  const { ref, visible } = useFadeUp();
  return (
    <div
      ref={ref}
      style={{ transitionDelay: delay }}
      className={cn(
        "card group transition-all duration-500 hover:-translate-y-1",
        visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-5"
      )}
    >
      <div className="card__content">
        <div
          className="w-10 h-10 rounded-[10px] flex items-center justify-center mb-5 transition-colors duration-200"
          style={{
            background: "var(--color-background-info)",
            border: "1px solid var(--color-border-info)",
          }}
        >
          <feature.icon size={17} style={{ color: "var(--primary)" }} />
        </div>
        <p
          className="text-[10px] font-bold tracking-[0.12em] uppercase mb-1.5"
          style={{ color: "var(--primary)" }}
        >
          {feature.label}
        </p>
        <h3
          className="text-[14.5px] font-semibold leading-snug mb-2 transition-colors duration-200 group-hover:text-primary"
          style={{ color: "var(--foreground)" }}
        >
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
    <div
      className="min-h-screen overflow-x-hidden"
      style={{ background: "var(--background)", color: "var(--foreground)" }}
    >

      {/* ════════════════════════════════════════════════════════════════
          NAVBAR
      ════════════════════════════════════════════════════════════════ */}
      <header
        className="sticky top-0 z-50 border-b"
        style={{ background: "rgba(255,255,255,0.97)", borderColor: "var(--border)" }}
      >
        <div className="section__inner flex items-center justify-between h-16 gap-4">

          <Link href="/" className="flex items-center gap-3 shrink-0 min-h-0 min-w-0">
            <div
              className="w-9 h-9 rounded-xl flex items-center justify-center shrink-0"
              style={{ background: "var(--primary)" }}
            >
              <GraduationCap size={17} color="white" />
            </div>
            <div className="leading-tight">
              <p className="text-[13px] font-semibold tracking-tight" style={{ color: "var(--foreground)" }}>
                UMaT Alumni
              </p>
            
            </div>
          </Link>

          <nav className="hidden md:flex items-center gap-0.5" aria-label="Primary">
            {NAV_LINKS.map((link) => (
              <button
                key={link.label}
                onClick={() => scrollToSection(link.href)}
                className="rounded-lg px-4 py-2 text-[13.5px] font-medium transition-colors hover:bg-secondary"
                style={{ color: "var(--muted-foreground)" }}
              >
                {link.label}
              </button>
            ))}
          </nav>

          <div className="hidden md:flex items-center gap-2">
            <Link href="/login">
              <Button variant="ghost" size="sm" className="text-[13px] font-medium">
                Sign in
              </Button>
            </Link>
            <Link href="/register">
              <Button size="sm" className="text-[13px] font-semibold gap-1.5">
                Join now <ArrowRight size={12} />
              </Button>
            </Link>
          </div>

          <button
            className="md:hidden flex items-center justify-center w-9 h-9 rounded-lg border transition-colors hover:bg-secondary"
            style={{ borderColor: "var(--border)" }}
            onClick={() => setMenuOpen((o) => !o)}
            aria-label="Toggle navigation"
            aria-expanded={menuOpen}
          >
            {menuOpen ? <X size={16} /> : <Menu size={16} />}
          </button>
        </div>

        {menuOpen && (
          <div
            className="md:hidden fixed inset-0 top-16 z-40 flex flex-col px-5 py-5 gap-1 overflow-y-auto"
            style={{ background: "rgba(255,255,255,0.97)", borderTop: "1px solid var(--border)" }}
          >
            {NAV_LINKS.map((link) => (
              <button
                key={link.label}
                onClick={() => { scrollToSection(link.href); setMenuOpen(false); }}
                className="w-full text-left rounded-xl px-4 py-3.5 text-base font-medium transition-colors hover:bg-secondary"
                style={{ color: "var(--muted-foreground)" }}
              >
                {link.label}
              </button>
            ))}
            <div className="mt-4 pt-4 flex flex-col gap-2.5" style={{ borderTop: "1px solid var(--border)" }}>
              <Link href="/login" onClick={() => setMenuOpen(false)}>
                <Button variant="outline" className="w-full font-medium">Sign in</Button>
              </Link>
              <Link href="/register" onClick={() => setMenuOpen(false)}>
                <Button className="w-full font-semibold gap-2">
                  Join now <ArrowRight size={14} />
                </Button>
              </Link>
            </div>
          </div>
        )}
      </header>


      {/* ════════════════════════════════════════════════════════════════
          HERO — var(--background) white
      ════════════════════════════════════════════════════════════════ */}
      <div className="relative overflow-hidden" style={{ background: "var(--background)" }}>

        {/* Dot-grid texture from global.css .bg-subtle-pattern */}
        <div className="absolute inset-0 bg-subtle-pattern opacity-[0.45] pointer-events-none" />

        {/* Soft brand radial glow */}
        <div
          className="absolute -top-40 -right-40 w-[640px] h-[640px] rounded-full pointer-events-none"
          style={{
            background:
              "radial-gradient(circle, color-mix(in oklch, var(--primary) 6%, transparent) 0%, transparent 65%)",
          }}
        />

        <div className="section__inner relative pt-2 pb-24">
          <div className="grid gap-12 lg:grid-cols-[1.1fr_0.9fr] items-center">

            {/* Left col */}
            <div>
              <Eyebrow>Verified UMaT alumni network</Eyebrow>

              <h1
                className="font-[family-name:var(--font-display)] mb-6"
                style={{
                  fontSize: "clamp(2.4rem,5.5vw,4.2rem)",
                  fontWeight: 700,
                  lineHeight: 1.06,
                  letterSpacing: "-0.025em",
                  color: "var(--foreground)",
                }}
              >
                The alumni portal{" "}
                <span className="relative" style={{ color: "var(--primary)" }}>
                  built
                  <span
                    className="absolute left-0 right-0 -bottom-1 h-[3px] rounded-full"
                    style={{ background: "var(--brand-primary-light)" }}
                  />
                </span>{" "}
                to connect, support, and grow together.
              </h1>

              <p
                className="mb-9 max-w-[52ch]"
                style={{
                  fontSize: "clamp(0.975rem,1.5vw,1.075rem)",
                  lineHeight: 1.8,
                  color: "var(--muted-foreground)",
                }}
              >
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

              {/* Trust tiles — stacked icon + text, 2-col mobile → 4-col sm+ */}
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                {[
                  { icon: GraduationCap, text: "Verified alumni access"  },
                  { icon: Briefcase,     text: "Jobs posted by grads"     },
                  { icon: Heart,         text: "Mentor matches from UMaT" },
                  { icon: Globe,         text: "Events & campaigns"        },
                ].map((item) => (
                  <div
                    key={item.text}
                    className="card flex flex-col gap-3 p-4 transition-all duration-200 hover:-translate-y-0.5 cursor-default"
                  >
                    <div
                      className="w-9 h-9 rounded-[10px] flex items-center justify-center shrink-0"
                      style={{
                        background: "var(--color-background-info)",
                        border: "1px solid var(--color-border-info)",
                      }}
                    >
                      <item.icon size={15} style={{ color: "var(--primary)" }} />
                    </div>
                    <p
                      className="text-[13px] font-semibold leading-snug"
                      style={{ color: "var(--foreground)" }}
                    >
                      {item.text}
                    </p>
                  </div>
                ))}
              </div>
            </div>

            {/* Right info card */}
            <div
              className="card"
              style={{ boxShadow: "0 4px 32px color-mix(in oklch, var(--primary) 9%, transparent)" }}
            >
              <div className="card__content">
                <div className="flex items-center gap-2 mb-7">
                  <span
                    className="w-2 h-2 rounded-full animate-pulse"
                    style={{ background: "var(--primary)" }}
                  />
                  <span
                    className="text-[11px] font-semibold tracking-[0.1em] uppercase"
                    style={{ color: "var(--muted-foreground)" }}
                  >
                    Built around alumni needs
                  </span>
                </div>

                {[
                  {
                    title: "Everything in one place",
                    body: "Jobs, mentorship, contributions, events, and community updates — all organized for quick access.",
                  },
                  {
                    title: "Control what matters",
                    body: "Choose the notifications, campaigns, and connections that matter to you.",
                  },
                  {
                    title: "Trusted, verified network",
                    body: "Every member is part of the UMaT community, so you can connect with confidence.",
                  },
                ].map((item, i, arr) => (
                  <div
                    key={item.title}
                    className={cn("py-4", i !== arr.length - 1 && "border-b")}
                    style={{ borderColor: "var(--border)" }}
                  >
                    <p className="text-[14px] font-semibold mb-1" style={{ color: "var(--foreground)" }}>
                      {item.title}
                    </p>
                    <p className="text-[13px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
                      {item.body}
                    </p>
                  </div>
                ))}

                <div className="mt-6 pt-5 border-t" style={{ borderColor: "var(--border)" }}>
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
          STATS — var(--secondary) soft green tint
      ════════════════════════════════════════════════════════════════ */}
      <div
        className="border-y"
        style={{ background: "var(--secondary)", borderColor: "var(--border)" }}
      >
        <div className="section__inner section">
          <div className="text-center mb-12">
            <Eyebrow>Alumni impact</Eyebrow>
            <h2
              className="font-[family-name:var(--font-display)]"
              style={{ color: "var(--foreground)" }}
            >
              What UMaT alumni are already doing here.
            </h2>
          </div>
          <div
            ref={statsRef}
            className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-[var(--space-gap)]"
          >
            {STATS.map((stat, i) => (
              <StatCard
                key={stat.label}
                stat={stat}
                active={statsActive}
                delay={`${i * 75}ms`}
              />
            ))}
          </div>
        </div>
      </div>


      {/* ════════════════════════════════════════════════════════════════
          FEATURES — var(--background) white
      ════════════════════════════════════════════════════════════════ */}
      <Section style={{ background: "var(--background)" }} id="features">
        <div className="section__inner section">
          <div className="mb-12 max-w-[56ch]">
            <Eyebrow>Pick your path</Eyebrow>
            <h2
              className="font-[family-name:var(--font-display)] mb-4"
              style={{ color: "var(--foreground)" }}
            >
              One portal for every alumni need.
            </h2>
            <p style={{ color: "var(--muted-foreground)", fontSize: "1.025rem", lineHeight: 1.75 }}>
              From jobs and mentorship to campaigns, events, and community connections.
            </p>
          </div>
          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-[var(--space-gap)]">
            {FEATURES.map((feature, i) => (
              <FeatureCard
                key={feature.title}
                feature={feature}
                delay={`${(i % 4) * 65}ms`}
              />
            ))}
          </div>
        </div>
      </Section>


      {/* ════════════════════════════════════════════════════════════════
          STORIES — var(--muted) deeper green tint
      ════════════════════════════════════════════════════════════════ */}
      <Section
        id="stories"
        className="border-y"
        style={{ background: "var(--muted)", borderColor: "var(--border)" }}
      >
        <div className="section__inner section">
          <div className="mb-12 max-w-[50ch]">
            <Eyebrow>Built for real situations</Eyebrow>
            <h2
              className="font-[family-name:var(--font-display)] mb-4"
              style={{ color: "var(--foreground)" }}
            >
              Real alumni needs, solved with clear tools.
            </h2>
            <p style={{ color: "var(--muted-foreground)", lineHeight: 1.75 }}>
              These are the most common ways UMaT graduates use the portal to move forward every day.
            </p>
          </div>

          <div className="grid gap-[var(--space-gap)] sm:grid-cols-3">
            {USE_CASES.map((item, i) => (
              <div
                key={item.scenario}
                className="card group relative overflow-hidden transition-all duration-300 hover:-translate-y-1"
                style={{ transitionDelay: `${i * 70}ms` }}
              >
                {/* Corner accent blob */}
                <div
                  className="absolute top-0 right-0 w-28 h-28 rounded-bl-full pointer-events-none opacity-70 group-hover:opacity-100 transition-opacity duration-300"
                  style={{ background: "var(--color-background-info)" }}
                />
                <div className="card__content relative">
                  <div
                    className="w-12 h-12 rounded-xl flex items-center justify-center mb-5"
                    style={{
                      background: "var(--color-background-info)",
                      border: "1px solid var(--color-border-info)",
                    }}
                  >
                    <item.icon size={20} style={{ color: "var(--primary)" }} />
                  </div>
                  <p
                    className="text-[10px] font-bold tracking-[0.12em] uppercase mb-2"
                    style={{ color: "var(--primary)" }}
                  >
                    {item.eyebrow}
                  </p>
                  <h3
                    className="font-[family-name:var(--font-display)] mb-3 leading-snug"
                    style={{ fontSize: "1.1rem", color: "var(--foreground)" }}
                  >
                    {item.scenario}
                  </h3>
                  <p style={{ fontSize: "0.845rem", color: "var(--muted-foreground)", lineHeight: 1.75 }}>
                    {item.desc}
                  </p>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-10 flex justify-center">
            <Link href="/register">
              <Button className="h-11 px-8 font-semibold gap-2">
                Join the network <ArrowRight size={14} />
              </Button>
            </Link>
          </div>
        </div>
      </Section>


      {/* ════════════════════════════════════════════════════════════════
          HOW IT WORKS — var(--background) white
      ════════════════════════════════════════════════════════════════ */}
      <Section id="how-it-works" style={{ background: "var(--background)" }}>
        <div className="section__inner section">
          <div className="text-center mb-12">
            <Eyebrow>Getting started</Eyebrow>
            <h2
              className="font-[family-name:var(--font-display)] max-w-[28ch] mx-auto"
              style={{ color: "var(--foreground)" }}
            >
              Three steps to join and make the portal work for you.
            </h2>
          </div>

          <div className="max-w-4xl mx-auto grid gap-[var(--space-gap)] sm:grid-cols-3">
            {HOW_IT_WORKS.map((step, i) => (
              <div
                key={step.n}
                className="card group transition-all duration-300 hover:-translate-y-1"
                style={{ transitionDelay: `${i * 75}ms` }}
              >
                <div className="card__content">
                  {/* Big step number */}
                  <p
                    className="font-[family-name:var(--font-display)] leading-none mb-5 select-none"
                    style={{
                      fontSize: "3.5rem",
                      fontWeight: 700,
                      letterSpacing: "-0.03em",
                      color: "var(--brand-primary-light)",
                    }}
                  >
                    {step.n}
                  </p>
                  {/* Icon */}
                  <div
                    className="w-9 h-9 rounded-[10px] flex items-center justify-center mb-4"
                    style={{
                      background: "var(--color-background-info)",
                      border: "1px solid var(--color-border-info)",
                    }}
                  >
                    <step.icon size={16} style={{ color: "var(--primary)" }} />
                  </div>
                  <h3
                    className="text-[15px] font-semibold mb-2 leading-snug"
                    style={{ color: "var(--foreground)" }}
                  >
                    {step.title}
                  </h3>
                  <p style={{ fontSize: "0.84rem", color: "var(--muted-foreground)", lineHeight: 1.75 }}>
                    {step.desc}
                  </p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </Section>


      {/* ════════════════════════════════════════════════════════════════
          FINAL CTA — var(--secondary) soft green tint
      ════════════════════════════════════════════════════════════════ */}
      <Section
        className="border-t"
        style={{ background: "var(--secondary)", borderColor: "var(--border)" }}
      >
        <div className="section__inner section">
          <div
            className="relative overflow-hidden rounded-2xl border text-center"
            style={{
              background: "var(--background)",
              borderColor: "var(--card-border)",
              boxShadow: "0 4px 40px color-mix(in oklch, var(--primary) 7%, transparent)",
            }}
          >
            {/* Top brand stripe */}
            <div
              className="absolute top-0 left-0 right-0 h-[3px]"
              style={{ background: "linear-gradient(90deg, transparent, var(--primary), transparent)" }}
            />
            {/* Soft glow */}
            <div
              className="absolute -top-20 left-1/2 -translate-x-1/2 w-80 h-52 rounded-full pointer-events-none"
              style={{
                background:
                  "radial-gradient(ellipse, color-mix(in oklch, var(--primary) 6%, transparent), transparent 70%)",
              }}
            />

            <div className="relative px-8 py-14 sm:px-16 sm:py-16">
              <Eyebrow>Take the next step</Eyebrow>
              <h2
                className="font-[family-name:var(--font-display)] mb-5 mx-auto max-w-[22ch]"
                style={{
                  fontSize: "clamp(1.9rem,4vw,3.2rem)",
                  lineHeight: 1.1,
                  color: "var(--foreground)",
                }}
              >
                UMaT shaped you.{" "}
                <span style={{ color: "var(--primary)" }}>Now shape what comes next.</span>
              </h2>
              <p
                className="mx-auto max-w-[46ch] mb-9"
                style={{ fontSize: "1.025rem", lineHeight: 1.75, color: "var(--muted-foreground)" }}
              >
                Join thousands of alumni already using the portal to connect, contribute, and grow
                their network with trusted peers.
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
      <footer
        className="border-t py-9"
        style={{ background: "var(--background)", borderColor: "var(--border)" }}
      >
        <div className="section__inner flex flex-col sm:flex-row items-center justify-between gap-5">
          <Link href="/" className="flex items-center gap-3 min-h-0 min-w-0">
            <div
              className="w-8 h-8 rounded-xl flex items-center justify-center shrink-0"
              style={{ background: "var(--primary)" }}
            >
              <GraduationCap size={14} color="white" />
            </div>
            <span className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
              UMaT Alumni Portal
            </span>
          </Link>

          <p className="text-[11.5px] text-center" style={{ color: "var(--muted-foreground)", opacity: 0.75 }}>
            © {new Date().getFullYear()} University of Mines and Technology Alumni Association
          </p>

          <nav className="flex flex-wrap items-center justify-center gap-5" aria-label="Footer links">
            {[
              { label: "Sign in",   href: "/login"            },
              { label: "Join now",  href: "/register"         },
              { label: "Campaigns", href: "/payment-campaign" },
            ].map((link) => (
              <Link
                key={link.label}
                href={link.href}
                className="text-[12.5px] font-medium transition-colors hover:text-foreground"
                style={{ color: "var(--muted-foreground)" }}
              >
                {link.label}
              </Link>
            ))}
          </nav>
        </div>
      </footer>

    </div>
  );
}