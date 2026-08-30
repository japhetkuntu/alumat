"use client";

import Link from "next/link";
import { useState, useEffect } from "react";
import type { LucideIcon } from "lucide-react";
import {
  Menu, X, ArrowRight, ChevronRight, ChevronDown,
  Briefcase, Users, CreditCard, Globe, Heart, ShoppingBag, Trophy, Bell,
  Images, Building2, Gift, ShieldCheck, Rocket, SlidersHorizontal,
  Mail, MapPin, CheckCircle2, MessageCircleOff, SearchX, ShieldAlert, UserX,
} from "lucide-react";
import { Button, Input, Label, Textarea, FormError, cn } from "@alumni/ui";
import { publicPlatformClient } from "@/lib/api-client";
import { handleApiError } from "@/lib/api-client";
import { Eyebrow, Section, scrollToSection, useFadeUp } from "./_marketing/primitives";
import { MarketingFooter } from "./_marketing/footer";

/* ─────────────────────────────────────────────────────────────────────────
   DATA
   ───────────────────────────────────────────────────────────────────────── */
const NAV_LINKS = [
  { label: "Features",    href: "#features"     },
  { label: "How it works", href: "#how-it-works" },
  { label: "FAQ",          href: "#faq"          },
];

const FEATURES: { icon: LucideIcon; label: string; title: string; desc: string; big?: boolean }[] = [
  { icon: Briefcase,   label: "Careers",       title: "A jobs board just for your alumni",  desc: "Alumni employers post roles straight to your community, before they hit public boards.", big: true },
  { icon: Users,       label: "Directory",     title: "Every member, one searchable list", desc: "Name, join year, location — members find each other in seconds." },
  { icon: CreditCard,  label: "Fundraising",   title: "Collect dues & fund projects",        desc: "Online payments for development projects, year-group dues, and welfare support." },
  { icon: Globe,       label: "Events",        title: "RSVPs for every gathering",           desc: "Speech days, AGMs, reunions, chapter meetups — all in one shared calendar." },
  { icon: Heart,       label: "Mentorship",    title: "Built-in mentor matching",             desc: "Alumni who've walked the path connect with those just starting out.", big: true },
  { icon: ShoppingBag, label: "Store",         title: "Sell branded merchandise",             desc: "An online store for association gear, with online payment and order tracking." },
  { icon: Images,      label: "Photo Albums",  title: "A living photo archive",                desc: "Staff add photos from every event; alumni browse them in a great gallery, forever." },
  { icon: Trophy,      label: "Spotlight",     title: "Celebrate your standout alumni",        desc: "Recognize members making waves globally, right on their community's home page." },
  { icon: Building2,   label: "Businesses",    title: "An alumni business directory",          desc: "Members list their businesses; the community discovers and supports each other." },
  { icon: Bell,        label: "Notifications", title: "Reach the right people, automatically", desc: "Jobs, fundraisers, events — members choose exactly what reaches them." },
];

const HOW_IT_WORKS = [
  { n: "01", icon: SlidersHorizontal, title: "Tell us about your institution", desc: "One short form — your institution's name and who we should talk to. Takes under two minutes." },
  { n: "02", icon: Rocket,            title: "We set your portal up",          desc: "Our team configures your branding, your subdomain, and your first admin account." },
  { n: "03", icon: Users,             title: "Your alumni join, free",         desc: "Share the link. Every member creates an account and steps into their new home." },
  { n: "04", icon: ShieldCheck,       title: "You stay in full control",       desc: "Your admin dashboard, your rules — approve members, manage content, run the show." },
];

const FAQS = [
  { q: "Is it really free?", a: "Yes. There's no setup fee, no monthly bill, and no cost to your institution to run your alumni portal. We handle the details on our side — you focus on your alumni community." },
  { q: "How long does setup take?", a: "Submit the form below and our team will typically reach out within one business day to get your institution's portal configured and ready to launch." },
  { q: "Can we use our own domain or subdomain?", a: "Yes — every institution gets a branded subdomain out of the box, and a custom domain can be configured for your institution as well." },
  { q: "What if our alumni currently coordinate over WhatsApp or spreadsheets?", a: "That's exactly what this replaces. Import your existing contact list, invite your alumni, and everything — directory, events, dues, jobs — moves into one place built for it." },
  { q: "Is our alumni data secure?", a: "Every institution's data is isolated from every other institution's on the platform, with role-based access control for your admin team." },
];

const WHATSAPP_PROBLEMS = [
  { icon: UserX,           title: "Caps at 1,024 members", desc: "WhatsApp groups max out at 1,024 people — a Community stretches to 5,000, but that's still a ceiling a growing alumni base will hit." },
  { icon: SearchX,         title: "No real search",        desc: "Find last year's fundraiser announcement? Good luck scrolling. WhatsApp only searches text in one chat at a time." },
  { icon: ShieldAlert,     title: "Real fraud risk",        desc: "UK Action Fraud logged 636 reports of WhatsApp group-chat scams in H1 2024 alone — often someone impersonating a member to solicit money." },
  { icon: MessageCircleOff, title: "No directory, no data", desc: "No member directory, no RSVP tracking, no dues collection, no engagement analytics — even in WhatsApp Communities." },
];

function FeatureCard({ feature, delay, tone = "primary" }: { feature: typeof FEATURES[number]; delay: string; tone?: "primary" | "accent" }) {
  const { ref, visible } = useFadeUp();
  const iconBg = tone === "accent" ? "var(--brand-accent-light, var(--color-background-info))" : "var(--color-background-info)";
  const iconBorder = tone === "accent" ? "var(--brand-accent, var(--color-border-info))" : "var(--color-border-info)";
  const iconColor = tone === "accent" ? "var(--brand-accent-dark, var(--brand-accent, var(--primary)))" : "var(--primary)";
  return (
    <div ref={ref} style={{ transitionDelay: delay }}
      className={cn(
        "card group transition-all duration-500 hover:-translate-y-1 hover:shadow-sm",
        feature.big && "sm:col-span-2",
        visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-5"
      )}>
      <div className={cn("card__content", feature.big && "sm:flex sm:items-center sm:gap-6")}>
        <div className={cn("w-10 h-10 rounded-[10px] flex items-center justify-center transition-colors duration-200 shrink-0",
          feature.big ? "sm:w-14 sm:h-14 mb-4 sm:mb-0" : "mb-4")}
          style={{ background: iconBg, border: `1px solid ${iconBorder}` }}>
          <feature.icon size={feature.big ? 20 : 17} className={feature.big ? "sm:size-6" : ""} style={{ color: iconColor }} />
        </div>
        <div>
          <p className="text-[10px] font-bold tracking-[0.12em] uppercase mb-1.5" style={{ color: iconColor }}>{feature.label}</p>
          <h3 className={cn("font-semibold leading-snug mb-2 group-hover:text-primary transition-colors duration-200", feature.big ? "text-[17px]" : "text-[14px]")} style={{ color: "var(--foreground)" }}>
            {feature.title}
          </h3>
          <p className={cn("leading-relaxed", feature.big ? "text-[13.5px] max-w-[42ch]" : "text-[13px]")} style={{ color: "var(--muted-foreground)" }}>
            {feature.desc}
          </p>
        </div>
      </div>
    </div>
  );
}

function WhatsAppProblemCard({ item, delay }: { item: typeof WHATSAPP_PROBLEMS[number]; delay: string }) {
  const { ref, visible } = useFadeUp();
  return (
    <div ref={ref} style={{ transitionDelay: delay }}
      className={cn("card transition-all duration-500 hover:-translate-y-1 hover:shadow-sm", visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-5")}>
      <div className="card__content">
        <div className="w-10 h-10 rounded-[10px] flex items-center justify-center mb-4 shrink-0"
          style={{ background: "color-mix(in oklch, var(--destructive) 12%, transparent)", border: "1px solid color-mix(in oklch, var(--destructive) 35%, transparent)" }}>
          <item.icon size={17} style={{ color: "var(--destructive)" }} />
        </div>
        <h3 className="text-[14px] font-semibold leading-snug mb-2" style={{ color: "var(--foreground)" }}>{item.title}</h3>
        <p className="text-[13px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>{item.desc}</p>
      </div>
    </div>
  );
}

function HowItWorksStep({ step, delay }: { step: typeof HOW_IT_WORKS[number]; delay: string }) {
  const { ref, visible } = useFadeUp();
  return (
    <div ref={ref}
      className={cn("relative transition-all duration-500", visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-4")}
      style={{ transitionDelay: delay }}>
      <p className="font-[family-name:var(--font-display)] leading-none select-none mb-3"
        style={{ fontSize: "3.75rem", fontWeight: 700, color: "var(--primary)", opacity: 0.14 }} aria-hidden="true">
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

function FaqItem({ item, open, onToggle }: { item: typeof FAQS[number]; open: boolean; onToggle: () => void }) {
  return (
    <div className="card overflow-hidden">
      <button
        type="button"
        onClick={onToggle}
        className="w-full flex items-center justify-between gap-4 text-left px-5 py-4 sm:px-6 sm:py-5"
        aria-expanded={open}
      >
        <span className="text-[14.5px] font-semibold" style={{ color: "var(--foreground)" }}>{item.q}</span>
        <ChevronDown size={16} className="shrink-0 transition-transform duration-300" style={{ color: "var(--muted-foreground)", transform: open ? "rotate(180deg)" : "none" }} />
      </button>
      <div className="grid transition-all duration-300 ease-out" style={{ gridTemplateRows: open ? "1fr" : "0fr" }}>
        <div className="overflow-hidden">
          <p className="px-5 pb-4 sm:px-6 sm:pb-5 text-[13.5px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>{item.a}</p>
        </div>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   ONBOARDING FORM
   ───────────────────────────────────────────────────────────────────────── */
interface LeadForm {
  institutionName: string;
  contactName: string;
  contactEmail: string;
  contactPhone: string;
  country: string;
  estimatedMemberCount: string;
  message: string;
}

const EMPTY_LEAD: LeadForm = {
  institutionName: "", contactName: "", contactEmail: "", contactPhone: "",
  country: "Ghana", estimatedMemberCount: "", message: "",
};

function OnboardingForm() {
  const [form, setForm] = useState<LeadForm>(EMPTY_LEAD);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  const set = <K extends keyof LeadForm>(key: K, value: LeadForm[K]) => setForm((f) => ({ ...f, [key]: value }));

  const validate = (): boolean => {
    if (!form.institutionName.trim()) { setError("Your institution's name is required."); return false; }
    if (!form.contactName.trim()) { setError("A contact name is required."); return false; }
    if (!form.contactEmail.trim() || !/^\S+@\S+\.\S+$/.test(form.contactEmail.trim())) { setError("A valid contact email is required."); return false; }
    setError(null);
    return true;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;
    setSubmitting(true);
    try {
      await publicPlatformClient.post("/public/onboarding-leads", {
        institutionName: form.institutionName.trim(),
        contactName: form.contactName.trim(),
        contactEmail: form.contactEmail.trim(),
        contactPhone: form.contactPhone.trim() || undefined,
        country: form.country.trim() || undefined,
        estimatedMemberCount: form.estimatedMemberCount.trim() || undefined,
        message: form.message.trim() || undefined,
      });
      setSubmitted(true);
    } catch (err) {
      setError(handleApiError(err));
    } finally {
      setSubmitting(false);
    }
  };

  if (submitted) {
    return (
      <div className="card p-8 sm:p-12 text-center flex flex-col items-center">
        <div className="w-14 h-14 rounded-full flex items-center justify-center mb-5" style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}>
          <CheckCircle2 size={26} style={{ color: "var(--primary)" }} />
        </div>
        <h3 className="font-[family-name:var(--font-display)] mb-2.5" style={{ fontSize: "1.35rem", color: "var(--foreground)" }}>Thanks — we&apos;ve got it.</h3>
        <p className="max-w-[42ch]" style={{ color: "var(--muted-foreground)", fontSize: "0.925rem", lineHeight: 1.7 }}>
          Our team will reach out to <strong style={{ color: "var(--foreground)" }}>{form.contactEmail}</strong> within one business day to get {form.institutionName} set up — free, as always.
        </p>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="card p-6 sm:p-8 space-y-5">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <Label required>Institution name</Label>
          <Input value={form.institutionName} onChange={(e) => set("institutionName", e.target.value)} placeholder="e.g. St. Roses Senior High School" />
        </div>
        <div>
          <Label>Country</Label>
          <Input value={form.country} onChange={(e) => set("country", e.target.value)} placeholder="e.g. Ghana" />
        </div>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <Label required>Your name</Label>
          <Input value={form.contactName} onChange={(e) => set("contactName", e.target.value)} placeholder="Who should we talk to?" />
        </div>
        <div>
          <Label required>Your email</Label>
          <Input type="email" value={form.contactEmail} onChange={(e) => set("contactEmail", e.target.value)} placeholder="you@institution.edu" />
        </div>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <Label>Phone (optional)</Label>
          <Input value={form.contactPhone} onChange={(e) => set("contactPhone", e.target.value)} placeholder="+233 ..." />
        </div>
        <div>
          <Label>Roughly how many alumni? (optional)</Label>
          <Input value={form.estimatedMemberCount} onChange={(e) => set("estimatedMemberCount", e.target.value)} placeholder="e.g. 2,000+" />
        </div>
      </div>
      <div>
        <Label>Tell us a bit more (optional)</Label>
        <Textarea value={form.message} onChange={(e) => set("message", e.target.value)} placeholder="What are you hoping your alumni portal will do for your community?" rows={4} />
      </div>
      <FormError message={error} />
      <Button type="submit" className="w-full h-12 text-[15px] font-semibold gap-2" isLoading={submitting} loadingText="Sending your request...">
        Get onboarded — free <ArrowRight size={15} />
      </Button>
      <p className="text-center text-[11.5px]" style={{ color: "var(--muted-foreground)" }}>No cost, no obligation. We&apos;ll be in touch shortly.</p>
    </form>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PAGE
   ───────────────────────────────────────────────────────────────────────── */
export default function PlatformMarketingPage() {
  const [menuOpen, setMenuOpen] = useState(false);
  const [openFaq, setOpenFaq] = useState<number | null>(0);

  useEffect(() => {
    document.body.style.overflow = menuOpen ? "hidden" : "";
    return () => { document.body.style.overflow = ""; };
  }, [menuOpen]);

  return (
    <div className="min-h-screen overflow-x-hidden" style={{ background: "var(--background)", color: "var(--foreground)" }}>

      {/* ════════════════════════════════════════════════════════════════
          NAVBAR
      ════════════════════════════════════════════════════════════════ */}
      <header className="sticky top-0 z-50 border-b backdrop-blur-xl"
        style={{ background: "color-mix(in oklch, var(--background) 86%, transparent)", borderColor: "var(--border)" }}>
        <div className="section__inner flex items-center justify-between h-16 gap-4">
          <Link href="/" className="flex items-center gap-3 shrink-0">
            <img src="/alumunion-mark.svg" alt="" className="w-9 h-9 rounded-xl shrink-0" />
            <p className="text-[13.5px] font-semibold tracking-tight" style={{ color: "var(--foreground)" }}>AlumUnion</p>
          </Link>

          <nav className="hidden md:flex items-center gap-0.5" aria-label="Primary">
            {NAV_LINKS.map((link) => (
              <button key={link.label} onClick={() => scrollToSection(link.href)}
                className="rounded-lg px-4 py-2 text-[13.5px] font-medium transition-colors hover:bg-secondary"
                style={{ color: "var(--muted-foreground)" }}>
                {link.label}
              </button>
            ))}
          </nav>

          <div className="hidden md:flex items-center gap-2">
            <Button size="sm" className="text-[13px] font-semibold gap-1.5" onClick={() => scrollToSection("#onboard")}>
              Get onboarded — free <ArrowRight size={12} />
            </Button>
          </div>

          <button className="md:hidden flex items-center justify-center w-9 h-9 rounded-lg border transition-colors hover:bg-secondary"
            style={{ borderColor: "var(--border)" }}
            onClick={() => setMenuOpen((o) => !o)} aria-label="Toggle navigation" aria-expanded={menuOpen}>
            {menuOpen ? <X size={16} /> : <Menu size={16} />}
          </button>
        </div>

        {menuOpen && (
          <div className="md:hidden fixed inset-x-0 top-16 z-40 flex flex-col px-5 py-8 gap-5 overflow-y-auto h-[calc(100dvh-4rem)]"
            style={{ background: "var(--background)", borderTop: "1px solid var(--border)" }}>
            {NAV_LINKS.map((link) => (
              <button key={link.label} onClick={() => { scrollToSection(link.href); setMenuOpen(false); }}
                className="w-full text-left font-[family-name:var(--font-display)] text-[26px] font-semibold transition-colors hover:text-primary"
                style={{ color: "var(--foreground)" }}>
                {link.label}
              </button>
            ))}
            <div className="mt-4 pt-4 flex flex-col gap-2.5" style={{ borderTop: "1px solid var(--border)" }}>
              <Button className="w-full font-semibold gap-2" onClick={() => { scrollToSection("#onboard"); setMenuOpen(false); }}>
                Get onboarded — free <ArrowRight size={14} />
              </Button>
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

        <div className="section__inner--wide relative pt-16 pb-20 text-center">

          <h1 className="font-[family-name:var(--font-display)] mb-7 max-w-[24ch] mx-auto section__inner--wide "
            style={{ fontSize: "clamp(2.5rem,5.2vw,4.1rem)", fontWeight: 700, lineHeight: 1.1, letterSpacing: "-0.025em", color: "var(--foreground)" }}>
            Give your alumni a home —{" "}
            <span className="relative whitespace-nowrap" style={{ color: "var(--primary)" }}>
              at no cost
              <span className="absolute left-0 right-0 -bottom-1 h-[3px] rounded-full" style={{ background: "var(--brand-primary-light)" }} />
            </span>{" "}
            to your institution.
          </h1>
          <p className="mb-10 max-w-[52ch] mx-auto"
            style={{ fontSize: "clamp(1rem,1.5vw,1.125rem)", lineHeight: 1.75, color: "var(--muted-foreground)" }}>
            One home for your alumni — jobs, fundraising, mentorship, and events, all in one place.{" "}
            Built for schools, universities, and any community that wants to stay connected.
          </p>
          <div className="flex flex-col sm:flex-row gap-3 justify-center mb-14">
            <Button size="lg" className="h-12 px-8 text-[15px] font-semibold gap-2" onClick={() => scrollToSection("#onboard")}>
              Get your institution onboarded <ArrowRight size={15} />
            </Button>
            <Button size="lg" variant="outline" className="h-12 px-8 text-[15px] font-medium" onClick={() => scrollToSection("#how-it-works")}>
              See how it works
            </Button>
          </div>

          <div className="flex flex-wrap items-center justify-center gap-x-8 gap-y-3">
            {[
              { icon: ShieldCheck, text: "Free, no hidden costs" },
              { icon: Rocket,      text: "Live in days, not months" },
              { icon: Users,       text: "Built for every alumni community" },
            ].map((item, i) => (
              <div key={item.text} className="flex items-center gap-2 cursor-default">
                <item.icon size={15} style={{ color: i % 2 === 0 ? "var(--primary)" : "var(--brand-accent, var(--primary))" }} />
                <p className="text-[12.5px] font-semibold" style={{ color: "var(--foreground)" }}>{item.text}</p>
              </div>
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
            <Eyebrow>Everything included</Eyebrow>
            <h2 className="font-[family-name:var(--font-display)] mb-4" style={{ color: "var(--foreground)" }}>
              One portal. Every alumni need.
            </h2>
            <p style={{ color: "var(--muted-foreground)", fontSize: "1.025rem", lineHeight: 1.75 }}>
              Everything your alumni association needs to stay connected — none of it costs your institution anything.
            </p>
          </div>
          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-[var(--space-gap)]">
            {FEATURES.map((feature, i) => (
              <FeatureCard key={feature.title} feature={feature} delay={`${(i % 4) * 65}ms`} tone={i % 2 === 0 ? "primary" : "accent"} />
            ))}
            <button onClick={() => scrollToSection("#onboard")}
              className="sm:col-span-2 card group flex items-center justify-between gap-4 p-6 text-left transition-all duration-500 hover:-translate-y-1"
              style={{ background: "var(--primary)", borderColor: "var(--primary)" }}>
              <div>
                <p className="text-[14px] font-semibold text-white mb-1">Ready to bring this to your alumni?</p>
                <p className="text-[12.5px]" style={{ color: "color-mix(in oklch, white 75%, transparent)" }}>Tell us about your institution — it's free to get started.</p>
              </div>
              <ArrowRight size={18} className="text-white shrink-0 transition-transform group-hover:translate-x-1" />
            </button>
          </div>
        </div>
      </Section>

      {/* ════════════════════════════════════════════════════════════════
          "SOUND FAMILIAR?" — WhatsApp teaser, links to /why-not-whatsapp
      ════════════════════════════════════════════════════════════════ */}
      <Section className="border-b" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="mb-12 max-w-[60ch]">
            <Eyebrow>Sound familiar?</Eyebrow>
            <h2 className="font-[family-name:var(--font-display)] mb-4" style={{ color: "var(--foreground)" }}>
              You&apos;re already running this over WhatsApp. It shows.
            </h2>
            <p style={{ color: "var(--muted-foreground)", fontSize: "1.025rem", lineHeight: 1.75 }}>
              It&apos;s free and everyone already has it — but a chat app was never built to run a community. Here&apos;s
              what that actually costs you.
            </p>
          </div>
          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-[var(--space-gap)] mb-8">
            {WHATSAPP_PROBLEMS.map((item, i) => (
              <WhatsAppProblemCard key={item.title} item={item} delay={`${i * 65}ms`} />
            ))}
          </div>
          <Link href="/why-not-whatsapp">
            <Button variant="outline" className="h-11 px-6 font-semibold gap-2">
              See the full comparison <ArrowRight size={14} />
            </Button>
          </Link>
        </div>
      </Section>

      {/* ════════════════════════════════════════════════════════════════
          HOW IT WORKS
      ════════════════════════════════════════════════════════════════ */}
      <Section id="how-it-works" className="border-b" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="text-center mb-14">
            <Eyebrow>Getting started</Eyebrow>
            <h2 className="font-[family-name:var(--font-display)] max-w-[30ch]" style={{ color: "var(--foreground)", margin: "0 auto" }}>
              From a form to a live portal, in a few simple steps.
            </h2>
          </div>
          <div className="max-w-6xl mx-auto grid gap-10 sm:gap-8 sm:grid-cols-2 lg:grid-cols-4 lg:divide-x" style={{ borderColor: "var(--border)" }}>
            {HOW_IT_WORKS.map((step, i) => (
              <div key={step.n} className={i > 0 ? "lg:pl-8" : undefined}>
                <HowItWorksStep step={step} delay={`${i * 100}ms`} />
              </div>
            ))}
          </div>
        </div>
      </Section>

      {/* ════════════════════════════════════════════════════════════════
          "IT'S FREE" — dedicated reassurance section, no pricing table
      ════════════════════════════════════════════════════════════════ */}
      <Section style={{ background: "var(--brand-primary-dark, var(--primary))" }}>
        <div className="relative overflow-hidden">
          <div className="absolute -top-24 -left-24 w-96 h-96 rounded-full pointer-events-none"
            style={{ background: "radial-gradient(circle, rgba(255,255,255,0.08), transparent 70%)" }} />
          <div className="absolute -bottom-32 -right-16 w-[420px] h-[420px] rounded-full pointer-events-none"
            style={{ background: "radial-gradient(circle, rgba(255,255,255,0.06), transparent 70%)" }} />
          <div className="section__inner--wide relative py-20 sm:py-24 text-center">
            <p className="text-[11px] font-semibold tracking-[0.12em] uppercase mb-4" style={{ color: "rgba(255,255,255,0.65)" }}>
              What it costs
            </p>
            <h2 className="font-[family-name:var(--font-display)] mb-6 max-w-[24ch]"
              style={{ fontSize: "clamp(2rem,4vw,3rem)", lineHeight: 1.12, color: "white", margin: "0 auto 1.5rem" }}>
              Completely free for your institution. Full stop.
            </h2>
            <p className="max-w-[52ch] mb-9" style={{ fontSize: "1.05rem", lineHeight: 1.75, color: "rgba(255,255,255,0.82)", margin: "0 auto 2.25rem" }}>
              No setup fee, no monthly bill, no per-member charge. Your alumni get a modern portal, and your institution
              never sees an invoice for it.
            </p>
            <Button size="lg" className="h-12 px-10 text-[15px] font-semibold gap-2"
              style={{ background: "white", color: "var(--primary)" }} onClick={() => scrollToSection("#onboard")}>
              Get your institution onboarded <ChevronRight size={16} />
            </Button>
          </div>
        </div>
      </Section>

      {/* ════════════════════════════════════════════════════════════════
          ONBOARDING FORM
      ════════════════════════════════════════════════════════════════ */}
      <Section id="onboard" className="border-b" style={{ background: "var(--muted)", borderColor: "var(--border)" }}>
        <div className="section__inner section">
          <div className="grid gap-12 lg:grid-cols-[1fr_1.15fr] items-start">
            <div className="lg:sticky lg:top-24">
              <Eyebrow>Get started</Eyebrow>
              <h2 className="font-[family-name:var(--font-display)] mb-4 max-w-[18ch]" style={{ color: "var(--foreground)" }}>
                Let&apos;s get your institution onboarded.
              </h2>
              <p className="max-w-[46ch] mb-8" style={{ color: "var(--muted-foreground)", fontSize: "1rem", lineHeight: 1.75 }}>
                Tell us a little about your institution and where to reach you. Our team will follow up to configure
                your portal, your branding, and your first admin account — free, from day one.
              </p>
              <div className="space-y-4">
                {[
                  { icon: Mail,     text: "We'll reply within one business day" },
                  { icon: ShieldCheck, text: "No cost, no obligation, no catch" },
                  { icon: MapPin,   text: "Built for any alumni community, anywhere" },
                ].map((item) => (
                  <div key={item.text} className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0" style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}>
                      <item.icon size={14} style={{ color: "var(--primary)" }} />
                    </div>
                    <p className="text-[13.5px] font-medium" style={{ color: "var(--foreground)" }}>{item.text}</p>
                  </div>
                ))}
              </div>
            </div>
            <OnboardingForm />
          </div>
        </div>
      </Section>

      {/* ════════════════════════════════════════════════════════════════
          FAQ
      ════════════════════════════════════════════════════════════════ */}
      <Section id="faq" style={{ background: "var(--background)" }}>
        <div className="section__inner section">
          <div className="mb-12 max-w-[50ch] mx-auto text-center">
            <Eyebrow>Questions</Eyebrow>
            <h2 className="font-[family-name:var(--font-display)] mb-4" style={{ color: "var(--foreground)" }}>
              Frequently asked questions.
            </h2>
          </div>
          <div className="max-w-2xl mx-auto space-y-3">
            {FAQS.map((item, i) => (
              <FaqItem key={item.q} item={item} open={openFaq === i} onToggle={() => setOpenFaq((v) => (v === i ? null : i))} />
            ))}
          </div>
        </div>
      </Section>

      <MarketingFooter />

    </div>
  );
}
