"use client";

import Link from "next/link";
import { useState, useEffect, useRef, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import type { IconType as LucideIcon } from "@alumni/ui";
import {
  GraduationCap, Users, Briefcase, Heart, Globe,
  Menu, X, ArrowRight, ChevronRight,
  BookOpen, Trophy, CreditCard, Bell,
  MapPin, Zap, Shield, Star, Award, ShoppingBag,
  Images, Building2, Newspaper, Clock,
} from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { publicMemberClient } from "@/lib/api-client";
import {
  JobsIllustration, MentorshipIllustration, DirectoryIllustration, FundraisingIllustration,
  EventsIllustration, StoreIllustration, AlbumsIllustration, SpotlightIllustration,
  BusinessIllustration, NotificationsIllustration,
} from "./_marketing/illustrations";

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
  landingPageStories?: DynamicLandingStory[] | null;
  newsBanner?: DynamicNewsBanner | null;
  displayName?: string | null;
  logoUrl?: string | null;
  /** Overrides the hero photo(s), shown as a carousel when there's more than one — falls back to IMG.heroPanel (generic stock photo) when empty. */
  heroImageUrls?: string[] | null;
  /** Overrides the short headline overlaid on the hero photo. */
  heroHeadline?: string | null;
  disabledFeatures?: string[] | null;
}

function useLandingContent(initialContent?: LandingContent | null) {
  const { data } = useQuery({
    queryKey: ["member-landing-content"],
    queryFn: async () => {
      const res = await publicMemberClient.get<{ data: LandingContent }>("/public/institution/theme");
      return res.data.data;
    },
    // Server-rendered by the root page (see RootPage in page.tsx) — the very
    // first paint already has the real institution's branding baked in, so
    // there's nothing to flash generic placeholder content while this
    // client-side fetch (which still runs, to pick up any edit made after
    // that server render) is in flight.
    initialData: initialContent ?? undefined,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
  return data;
}

/* ─────────────────────────────────────────────────────────────────────────
   REAL INSTITUTION CONTENT — news, events, and a spotlight, all pulled from
   this tenant's own actual data (institution-wide items only — nothing
   community/year-restricted, since an anonymous visitor has no membership
   context). This is what makes the homepage read as this institution's own
   site rather than a generic product marketing page.
   ───────────────────────────────────────────────────────────────────────── */
interface PublicNewsItem { id: string; title: string; excerpt: string; imageUrl?: string | null; publishedAt?: string | null; category: string; }
interface PublicEventItem { id: string; title: string; startDate: string; venue: string; bannerImageUrl?: string | null; }
interface PublicSpotlightItem { id: string; title: string; story: string; imageUrl?: string | null; memberName: string; featuredMonth?: string | null; }

function usePublicNews() {
  const { data, isPending } = useQuery({
    queryKey: ["public-news"],
    queryFn: async () => (await publicMemberClient.get<{ data: PublicNewsItem[] }>("/public/news", { params: { take: 3 } })).data.data,
    staleTime: 5 * 60 * 1000, retry: false,
  });
  return { items: data ?? [], isPending };
}
function usePublicEvents() {
  const { data, isPending } = useQuery({
    queryKey: ["public-events"],
    queryFn: async () => (await publicMemberClient.get<{ data: PublicEventItem[] }>("/public/events", { params: { take: 3 } })).data.data,
    staleTime: 5 * 60 * 1000, retry: false,
  });
  return { items: data ?? [], isPending };
}
function usePublicSpotlight() {
  const { data, isPending } = useQuery({
    queryKey: ["public-spotlight"],
    queryFn: async () => (await publicMemberClient.get<{ data: PublicSpotlightItem[] }>("/public/spotlights", { params: { take: 1 } })).data.data,
    staleTime: 5 * 60 * 1000, retry: false,
  });
  return { item: data?.[0], isPending };
}

function formatNewsDate(iso?: string | null) {
  if (!iso) return "";
  return new Date(iso).toLocaleDateString("en-US", { day: "numeric", month: "short", year: "numeric" });
}
function formatEventDate(iso: string) {
  const d = new Date(iso);
  return { day: d.getDate(), month: d.toLocaleDateString("en-US", { month: "short" }).toUpperCase(), time: d.toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit" }) };
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

/** Auto-advancing hero photo carousel — falls back to a single static image when there's nothing (or only one photo) to rotate through. */
function HeroCarousel({ images }: { images: string[] }) {
  const [index, setIndex] = useState(0);

  useEffect(() => {
    if (images.length < 2) return;
    const timer = setInterval(() => setIndex((i) => (i + 1) % images.length), 5000);
    return () => clearInterval(timer);
  }, [images.length]);

  return (
    <>
      {images.map((src, i) => (
        <img
          key={src + i}
          src={src}
          alt="Alumni community"
          className="absolute inset-0 w-full h-full object-cover transition-opacity duration-700"
          style={{ opacity: i === index ? 1 : 0 }}
        />
      ))}
      {images.length > 1 && (
        <div className="absolute top-3 sm:top-4 left-0 right-0 flex items-center justify-center gap-1.5 z-10">
          {images.map((_, i) => (
            <button
              key={i}
              type="button"
              aria-label={`Show photo ${i + 1}`}
              onClick={() => setIndex(i)}
              className="h-1.5 rounded-full transition-all"
              style={{
                width: i === index ? "18px" : "6px",
                background: i === index ? "white" : "rgba(255,255,255,0.5)",
              }}
            />
          ))}
        </div>
      )}
    </>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   DATA
   ───────────────────────────────────────────────────────────────────────── */
const NAV_LINKS = [
  { label: "News",         href: "#news"         },
  { label: "Features",     href: "#features"     },
  { label: "Stories",      href: "#stories"      },
  { label: "How it works", href: "#how-it-works" },
];

const FEATURES: { icon: LucideIcon; label: string; title: string; desc: string; big?: boolean; featureKey: string | undefined; illustration: React.ComponentType<{ className?: string; tone?: "primary" | "accent" }>; tone?: "primary" | "accent" }[] = [
  { icon: Briefcase,    label: "Careers",       title: "Jobs inside the network",       desc: "Roles posted by alumni employers before they reach public boards: first look, before LinkedIn.", big: true, featureKey: "Jobs", illustration: JobsIllustration },
  { icon: Users,        label: "Directory",     title: "Find any old student in seconds", desc: "Search by name, graduation year, or location, from local chapters to the diaspora.", featureKey: "Directory", illustration: DirectoryIllustration },
  { icon: CreditCard,   label: "Contributions", title: "Fund projects & welfare",       desc: "Easy payments for school development fundraisers, year-group dues, and member welfare support.", featureKey: "Contributions", illustration: FundraisingIllustration },
  { icon: Globe,        label: "Events",        title: "Never miss a Speech Day or AGM", desc: "RSVP for annual dinners, speech and prize-giving days, chapter meetings, and reunions.", featureKey: "Events", illustration: EventsIllustration },
  // Explicitly accent (not left to the grid's primary/accent alternation
  // below) — one of the three "big", more-visible cards should always carry
  // the secondary color, or an institution's secondary color ends up
  // confined to small cards where it's easy to miss entirely.
  { icon: Heart,        label: "Mentorship",    title: "Give back. Get ahead.",         desc: "Connect with alumni who've already done what you're trying to do, one conversation at a time.", big: true, featureKey: "Mentorship", illustration: MentorshipIllustration, tone: "accent" },
  { icon: ShoppingBag,  label: "Store",         title: "Shop alumni merchandise",      desc: "Buy branded gear and support the association, pay online, pick up or receive your order.", featureKey: "Store", illustration: StoreIllustration },
  { icon: Images,       label: "Photo Albums",  title: "Relive it, one album at a time", desc: "Browse photos from reunions, Speech Day, and every gathering in between, added by the school, viewed by everyone.", big: true, featureKey: "PhotoAlbums", illustration: AlbumsIllustration },
  { icon: Trophy,       label: "Spotlight",     title: "Celebrate the wins",           desc: "A spotlight recognizing old students making waves globally and giving back to the school.", featureKey: "Spotlights", illustration: SpotlightIllustration },
  { icon: Building2,    label: "Businesses",    title: "Support alumni-owned business", desc: "Browse businesses run by fellow graduates, or list your own and get discovered by the network.", featureKey: "BusinessDirectory", illustration: BusinessIllustration },
  { icon: Bell,         label: "Notifications", title: "Hear about what you care about", desc: "Jobs, fundraisers, event invites: you choose what reaches you.", featureKey: undefined, illustration: NotificationsIllustration },
];

const STATS = [
  { end: 5000, suffix: "+",    label: "Alumni registered",     desc: "Verified graduates"          },
  { end: 120,  suffix: "+",    label: "Countries represented", desc: "A truly global network"            },
  // The one stat that gets the secondary color — blended toward white so it
  // stays legible on the dark, solid primary band regardless of how light
  // or dark the institution's own accent happens to be, unlike raw accent
  // text (which is only contrast-checked against a light/white background
  // elsewhere, not this one's colored backdrop).
  { end: 2,    prefix: "GHS ", suffix: "M+", label: "Raised for projects & welfare", desc: "Funding school development and member support", highlight: true },
  { end: 300,  suffix: "+",    label: "Jobs posted",           desc: "Roles shared by alumni employers"  },
];

const USE_CASES = [
  {
    icon: Briefcase,
    eyebrow: "Career",
    image: IMG.storyJobs,
    scenario: "The job that never reached a public board",
    desc: "Alumni employers post directly to the portal first, before LinkedIn, before agencies. Being in the network means seeing those roles first.",
  },
  {
    icon: CreditCard,
    eyebrow: "Giving",
    image: IMG.storyGiving,
    scenario: "The dormitory project that needed 200 people",
    desc: "From school development projects to member welfare support, year-group fundraisers pool contributions from graduates across the world.",
  },
  {
    icon: Heart,
    eyebrow: "Mentorship",
    image: IMG.storyMentor,
    scenario: "The mentor who's already done it",
    desc: "Every programme, every career path: there's a graduate ahead of you on that road. The mentorship feature is how you find them.",
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
function StatRow({ stat, active, index }: { stat: typeof STATS[number]; active: boolean; index: number }) {
  const count = useCounter(stat.end, active);
  // Counting up is driven entirely by the parent band's own single
  // IntersectionObserver (`active`, tied to `statsRef`) — each row used to run
  // a second, per-item observer purely for its own fade-in, but that redundant
  // observer could get "stuck" (never firing) on a fast/instant scroll, leaving
  // some stats permanently invisible. Rows render immediately now; only the
  // count-up animation is deferred until the band is on screen.
  return (
    <div
      className={cn(
        "px-4 py-5 sm:px-8 sm:py-1 flex flex-col items-center text-center sm:items-start sm:text-left",
        index === 0 && "sm:pl-0",
      )}>
      <p className="font-[family-name:var(--font-display)] leading-none tabular-nums break-words mb-2"
        style={{
          fontSize: "clamp(1.6rem,3vw,2.1rem)", fontWeight: 700, letterSpacing: "-0.02em",
          color: stat.highlight ? "color-mix(in oklch, var(--brand-accent, white) 65%, white)" : "white",
        }}>
        {stat.prefix}{count.toLocaleString()}{stat.suffix}
      </p>
      <p className="text-[12px] sm:text-[12.5px] font-medium leading-snug max-w-[16ch]" style={{ color: "color-mix(in oklch, white 65%, transparent)" }}>{stat.label}</p>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   FEATURE CARD
   ───────────────────────────────────────────────────────────────────────── */
function FeatureCard({ feature, delay, tone = "primary" }: { feature: typeof FEATURES[number]; delay: string; tone?: "primary" | "accent" }) {
  const { ref, visible } = useFadeUp();
  const big = "big" in feature && feature.big;
  // Alternates primary/accent across the grid — the same flat, solid-fill
  // pattern as the dashboard stat cards, so a real secondary color shows up
  // as a genuinely distinct tone, never blended into a background.
  const iconColor = tone === "accent" ? "var(--brand-accent-dark, var(--brand-accent, var(--primary)))" : "var(--primary)";
  return (
    <div ref={ref} style={{ transitionDelay: delay }}
      className={cn(
        "card group transition-all duration-500 hover:-translate-y-1 hover:shadow-sm",
        big && "sm:col-span-2",
        visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-5"
      )}>
      <div className={cn("card__content", big && "sm:flex sm:items-center sm:gap-6")}>
        <feature.illustration
          tone={tone}
          className={cn("transition-transform duration-500 group-hover:scale-105 mb-4", big ? "w-24 h-24 sm:w-32 sm:h-32 sm:mb-0" : "w-20 h-20")}
        />
        <div>
          <p className="text-[10px] font-bold tracking-[0.12em] uppercase mb-1.5" style={{ color: iconColor }}>
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
   NEWS · EVENTS · SPOTLIGHT
   Editorial feed (news + upcoming events) with a spotlight sidebar card —
   the section that makes this read as the institution's own site instead
   of a generic product page. Each piece is independently optional: a brand
   new tenant with nothing published yet simply doesn't render this section.
   ───────────────────────────────────────────────────────────────────────── */
function NewsCard({ item, big }: { item: PublicNewsItem; big?: boolean }) {
  return (
    <Link href={`/news/${item.id}`} className="group block">
      <article>
        <div className="relative overflow-hidden rounded-lg mb-3.5" style={{ aspectRatio: big ? "16/9" : "4/3" }}>
          {item.imageUrl ? (
            <img src={item.imageUrl} alt="" className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105" />
          ) : (
            <div className="w-full h-full flex items-center justify-center"
              style={{ background: "linear-gradient(135deg, color-mix(in oklch, var(--primary) 14%, var(--muted)) 0%, color-mix(in oklch, var(--brand-accent, var(--primary)) 10%, var(--muted)) 100%)" }}>
              <Newspaper size={big ? 30 : 20} style={{ color: "var(--primary)", opacity: 0.4 }} />
            </div>
          )}
        </div>
        <p className="text-[10px] font-bold tracking-[0.1em] uppercase mb-1.5" style={{ color: "var(--primary)" }}>{item.category || "News"}</p>
        <h3 className={cn("font-semibold leading-snug mb-2 transition-colors group-hover:text-primary", big ? "text-[19px]" : "text-[14.5px]")}
          style={{ color: "var(--foreground)" }}>
          {item.title}
        </h3>
        {big && (
          <p className="text-[13.5px] leading-relaxed mb-2.5 max-w-[58ch]" style={{ color: "var(--muted-foreground)" }}>{item.excerpt}</p>
        )}
        <div className="flex items-center gap-3">
          <p className="text-[11.5px] font-medium" style={{ color: "var(--muted-foreground)", opacity: 0.75 }}>{formatNewsDate(item.publishedAt)}</p>
          <span className="flex items-center gap-1 text-[11.5px] font-semibold transition-transform group-hover:translate-x-0.5" style={{ color: "var(--primary)" }}>
            Sign in to read <ArrowRight size={10} />
          </span>
        </div>
      </article>
    </Link>
  );
}

function EventRow({ item }: { item: PublicEventItem }) {
  const d = formatEventDate(item.startDate);
  return (
    <Link href={`/events/${item.id}`} className="group flex items-start gap-3.5 py-3.5" style={{ borderBottom: "1px solid var(--border)" }}>
      <div className="shrink-0 w-12 rounded-lg overflow-hidden text-center" style={{ border: "1px solid var(--border)" }}>
        <div className="text-[9.5px] font-bold uppercase py-0.5" style={{ background: "var(--primary)", color: "white" }}>{d.month}</div>
        <div className="text-[16px] font-bold py-1 font-[family-name:var(--font-display)]" style={{ color: "var(--foreground)" }}>{d.day}</div>
      </div>
      {item.bannerImageUrl && (
        <img src={item.bannerImageUrl} alt="" className="shrink-0 w-12 h-12 rounded-lg object-cover" style={{ border: "1px solid var(--border)" }} />
      )}
      <div className="min-w-0 pt-0.5">
        <h4 className="text-[13.5px] font-semibold leading-snug mb-1 truncate transition-colors group-hover:text-primary" style={{ color: "var(--foreground)" }}>{item.title}</h4>
        <div className="flex items-center gap-3 flex-wrap">
          <span className="flex items-center gap-1 text-[11.5px]" style={{ color: "var(--muted-foreground)" }}>
            <Clock size={11} /> {d.time}
          </span>
          <span className="flex items-center gap-1 text-[11.5px] truncate" style={{ color: "var(--muted-foreground)" }}>
            <MapPin size={11} /> {item.venue}
          </span>
        </div>
      </div>
    </Link>
  );
}

/** Fills the same visual slot the real thing would occupy — so a brand-new institution with nothing published yet still reads as a finished, intentional page instead of a gap where content should be. */
function EmptyPanel({ icon: Icon, title, desc, big }: { icon: LucideIcon; title: string; desc: string; big?: boolean }) {
  return (
    <div className={cn("rounded-xl flex flex-col items-center justify-center text-center gap-2.5 px-6", big ? "py-16" : "py-10")}
      style={{ background: "var(--muted)", border: "1px dashed var(--border)" }}>
      <Icon size={big ? 26 : 20} style={{ color: "var(--muted-foreground)", opacity: 0.5 }} />
      <p className={cn("font-semibold", big ? "text-[14px]" : "text-[12.5px]")} style={{ color: "var(--foreground)" }}>{title}</p>
      <p className={cn("leading-relaxed max-w-[32ch]", big ? "text-[12.5px]" : "text-[11.5px]")} style={{ color: "var(--muted-foreground)" }}>{desc}</p>
    </div>
  );
}

/** Shimmer stand-in for a NewsCard, same proportions so nothing shifts when the real card swaps in. */
function NewsCardSkeleton({ big }: { big?: boolean }) {
  return (
    <div>
      <Skeleton className="w-full rounded-lg mb-3.5" style={{ aspectRatio: big ? "16/9" : "4/3" }} />
      <Skeleton className="h-2.5 w-16 mb-2" variant="text" />
      <Skeleton className={cn("mb-2", big ? "h-5 w-4/5" : "h-4 w-full")} variant="text" />
      {big && <Skeleton className="h-3 w-3/4 mb-2.5" variant="text" />}
      <Skeleton className="h-2.5 w-20" variant="text" />
    </div>
  );
}

function EventRowSkeleton() {
  return (
    <div className="flex items-start gap-3.5 py-3.5" style={{ borderBottom: "1px solid var(--border)" }}>
      <Skeleton className="shrink-0 w-12 h-12 rounded-lg" />
      <div className="min-w-0 pt-0.5 flex-1 space-y-2">
        <Skeleton className="h-3.5 w-3/4" variant="text" />
        <Skeleton className="h-2.5 w-1/2" variant="text" />
      </div>
    </div>
  );
}

function SpotlightCardSkeleton() {
  return (
    <div className="card overflow-hidden">
      <Skeleton className="w-full" style={{ aspectRatio: "16/10" }} />
      <div className="card__content space-y-2">
        <Skeleton className="h-3.5 w-2/3" variant="text" />
        <Skeleton className="h-3 w-1/3" variant="text" />
        <Skeleton className="h-2.5 w-full" variant="text" />
        <Skeleton className="h-2.5 w-4/5" variant="text" />
      </div>
    </div>
  );
}

function NewsEventsSpotlight() {
  const { items: news, isPending: newsPending } = usePublicNews();
  const { items: events, isPending: eventsPending } = usePublicEvents();
  const { item: spotlight, isPending: spotlightPending } = usePublicSpotlight();

  return (
    <Section id="news" className="border-b" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
      <div className="section__inner--wide section">
        <p className="text-[11px] font-semibold tracking-[0.12em] uppercase mb-6" style={{ color: "var(--brand-accent-dark, var(--brand-accent, var(--primary)))" }}>
          From the community
        </p>
        <div className="grid gap-12 lg:grid-cols-[1.6fr_1fr]">

          {/* News feed */}
          <div>
            <div className="flex items-end justify-between mb-8">
              <h2 className="font-[family-name:var(--font-display)]" style={{ color: "var(--foreground)", fontSize: "1.6rem" }}>Latest news</h2>
              {news.length > 0 && (
                <Link href="/login" className="hidden sm:flex items-center gap-1 text-[12.5px] font-semibold" style={{ color: "var(--primary)" }}>
                  See all <ArrowRight size={12} />
                </Link>
              )}
            </div>
            {newsPending ? (
              <div className="grid gap-x-8 gap-y-9 sm:grid-cols-2">
                <div className="sm:col-span-2"><NewsCardSkeleton big /></div>
                <NewsCardSkeleton />
                <NewsCardSkeleton />
              </div>
            ) : news.length > 0 ? (
              <div className="grid gap-x-8 gap-y-9 sm:grid-cols-2">
                <div className="sm:col-span-2"><NewsCard item={news[0]} big /></div>
                {news.slice(1).map((n) => <NewsCard key={n.id} item={n} />)}
              </div>
            ) : (
              <EmptyPanel icon={Newspaper} big title="News is on its way" desc="Updates from the association will be featured here as soon as they're posted." />
            )}
          </div>

          {/* Sidebar — upcoming events + spotlight */}
          <div className="flex flex-col gap-10">
            <div>
              <h2 className="font-[family-name:var(--font-display)] mb-4" style={{ color: "var(--foreground)", fontSize: "1.15rem" }}>Upcoming events</h2>
              {eventsPending ? (
                <div>{[0, 1, 2].map((i) => <EventRowSkeleton key={i} />)}</div>
              ) : events.length > 0 ? (
                <>
                  <div>{events.map((e) => <EventRow key={e.id} item={e} />)}</div>
                  <Link href="/login" className="mt-4 flex items-center gap-1 text-[12.5px] font-semibold" style={{ color: "var(--primary)" }}>
                    See full calendar <ArrowRight size={12} />
                  </Link>
                </>
              ) : (
                <EmptyPanel icon={Clock} title="No events scheduled yet" desc="Reunions, chapter meetups, and Speech Day will show up here." />
              )}
            </div>

            {spotlightPending ? (
              <SpotlightCardSkeleton />
            ) : spotlight ? (
              <Link href="/spotlights" className="card overflow-hidden group block">
                <div className="relative w-full" style={{ aspectRatio: "16/10", background: "var(--muted)" }}>
                  {spotlight.imageUrl ? (
                    <>
                      {/* Blurred fill behind an object-contain copy — see the identical
                          pattern (and its rationale) in apps/member's spotlights page. */}
                      <img src={spotlight.imageUrl} alt="" aria-hidden="true" className="absolute inset-0 w-full h-full object-cover scale-125 blur-2xl opacity-50" />
                      <img src={spotlight.imageUrl} alt={spotlight.memberName} className="absolute inset-0 w-full h-full object-contain transition-transform duration-500 group-hover:scale-105" />
                    </>
                  ) : (
                    <div className="w-full h-full flex items-center justify-center"
                      style={{ background: "linear-gradient(135deg, color-mix(in oklch, var(--primary) 14%, var(--muted)) 0%, color-mix(in oklch, var(--brand-accent, var(--primary)) 10%, var(--muted)) 100%)" }}>
                      <Trophy size={28} style={{ color: "var(--primary)", opacity: 0.4 }} />
                    </div>
                  )}
                  <div className="absolute top-3 left-3 rounded-full px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide flex items-center gap-1"
                    style={{ background: "var(--primary)", color: "white" }}>
                    <Trophy size={10} /> Spotlight
                  </div>
                </div>
                <div className="card__content">
                  <h3 className="text-[14px] font-semibold leading-snug mb-1 transition-colors group-hover:text-primary" style={{ color: "var(--foreground)" }}>{spotlight.memberName}</h3>
                  <p className="text-[12.5px] font-medium mb-2" style={{ color: "var(--primary)" }}>{spotlight.title}</p>
                  <p className="text-[12.5px] leading-relaxed line-clamp-3 mb-2.5" style={{ color: "var(--muted-foreground)" }}>{spotlight.story}</p>
                  <span className="flex items-center gap-1 text-[11.5px] font-semibold" style={{ color: "var(--primary)" }}>
                    Sign in to read the full story <ArrowRight size={10} />
                  </span>
                </div>
              </Link>
            ) : (
              <EmptyPanel icon={Trophy} title="No spotlight yet" desc="A featured alumni story will appear here once one's approved." />
            )}
          </div>

        </div>
      </div>
    </Section>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PAGE
   ───────────────────────────────────────────────────────────────────────── */
export default function LandingPage({ initialContent }: { initialContent?: LandingContent | null }) {
  const [menuOpen, setMenuOpen] = useState(false);
  const statsRef = useRef<HTMLDivElement>(null);
  const [statsActive, setStatsActive] = useState(false);
  const content = useLandingContent(initialContent);

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
            <img src={content?.logoUrl || "/alumunion-mark.svg"} alt={content?.displayName ?? "Logo"} className="w-9 h-9 rounded-xl object-cover shrink-0" />
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
          HERO — a single full-bleed banner photo with a masthead-style
          overlay, the way an institution's own website leads with a campus
          photo rather than a product screenshot.
      ════════════════════════════════════════════════════════════════ */}
      <div className="relative w-full overflow-hidden h-[62vh] min-h-[420px] max-h-[640px]">
        <HeroCarousel images={content?.heroImageUrls?.length ? content.heroImageUrls : [IMG.heroPanel]} />
        <div className="absolute inset-0" style={{ background: "linear-gradient(0deg, rgba(0,0,0,0.78) 0%, rgba(0,0,0,0.4) 46%, rgba(0,0,0,0.15) 100%)" }} />

        <div className="absolute inset-x-0 bottom-0">
          <div className="section__inner--wide pb-10 sm:pb-14 pt-10">
            <span className="inline-flex items-center gap-2 mb-4 px-3 py-1 rounded-full text-[11px] font-bold"
              style={{ background: "rgba(15,23,42,0.5)", color: "white", border: "1px solid rgba(255,255,255,0.18)" }}>
              <span className="w-1.5 h-1.5 rounded-full shrink-0" style={{ background: "var(--brand-accent, var(--primary))" }} />
              {STATS[0].end.toLocaleString()}{STATS[0].suffix} alumni already home
            </span>
            <p className="text-[11px] font-semibold tracking-[0.16em] uppercase mb-3" style={{ color: "rgba(255,255,255,0.7)" }}>
              {content?.displayName || "Alumni Association"}
            </p>
            <h1 className="font-[family-name:var(--font-display)] mb-5 max-w-[24ch]"
              style={{ fontSize: "clamp(2rem,4.4vw,3.4rem)", fontWeight: 700, lineHeight: 1.14, letterSpacing: "-0.02em", color: "white" }}>
              {content?.heroHeadline || "Every graduate, one network, wherever they are."}
            </h1>
            <div className="flex flex-col sm:flex-row gap-3">
              <Link href="/register">
                <Button size="lg" className="w-full sm:w-auto h-12 px-8 text-[15px] font-semibold gap-2">
                  Join the network <ArrowRight size={15} />
                </Button>
              </Link>
              <Link href="/login">
                <Button size="lg" variant="outline" className="w-full sm:w-auto h-12 px-8 text-[15px] font-medium"
                  style={{ borderColor: "var(--brand-accent, rgba(255,255,255,0.5))", color: "white", background: "color-mix(in oklch, var(--brand-accent, transparent) 22%, rgba(255,255,255,0.08))" }}>
                  Already a member
                </Button>
              </Link>
            </div>
          </div>
        </div>
      </div>

      {/* ════════════════════════════════════════════════════════════════
          NEWS · EVENTS · SPOTLIGHT — this institution's own real content,
          in an editorial layout (masthead feed + sidebar), the way an
          actual association website leads rather than a product pitch.
          Hidden per-section when the institution has nothing published yet.
      ════════════════════════════════════════════════════════════════ */}
      <NewsEventsSpotlight />

      {/* ════════════════════════════════════════════════════════════════
          STATS — full-bleed banded row, not a card grid
      ════════════════════════════════════════════════════════════════ */}
      <div className="border-b" style={{ background: "var(--brand-primary-dark, var(--primary))", borderColor: "var(--border)" }}>
        <div className="section__inner--wide">
          <div className="flex flex-col lg:flex-row lg:items-center gap-10 lg:gap-0 py-14">
            <div className="lg:w-[280px] lg:pr-10 shrink-0">
              <p className="text-[11px] font-semibold tracking-[0.12em] uppercase mb-3" style={{ color: "color-mix(in oklch, white 55%, transparent)" }}>
                Alumni impact
              </p>
              <h2 className="font-[family-name:var(--font-display)]" style={{ color: "white", fontSize: "clamp(1.5rem,2.4vw,2rem)", lineHeight: 1.15 }}>
                What alumni are already doing here.
              </h2>
            </div>
            <div ref={statsRef} className="flex-1 grid grid-cols-2 gap-y-2 sm:gap-y-0 rounded-xl sm:rounded-none divide-x divide-y sm:divide-y-0 md:grid-cols-4" style={{ borderColor: "color-mix(in oklch, white 15%, transparent)" }}>
              {STATS.map((stat, i) => (
                <StatRow key={stat.label} stat={stat} active={statsActive} index={i} />
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
            <p className="text-[11px] font-semibold tracking-[0.12em] uppercase mb-3" style={{ color: "var(--primary)" }}>
              What&apos;s inside
            </p>
            <h2 className="font-[family-name:var(--font-display)] mb-4" style={{ color: "var(--foreground)" }}>
              One portal for every alumni need.
            </h2>
            <p style={{ color: "var(--muted-foreground)", fontSize: "1.025rem", lineHeight: 1.75 }}>
              From jobs and mentorship to fundraisers, events, and community connections.
            </p>
          </div>
          <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-[var(--space-gap)]">
            {FEATURES
              .filter((feature) => !feature.featureKey || !content?.disabledFeatures?.includes(feature.featureKey))
              .map((feature, i) => (
                <FeatureCard key={feature.title} feature={feature} delay={`${(i % 4) * 65}ms`} tone={feature.tone ?? (i % 2 === 0 ? "primary" : "accent")} />
              ))}
            {/* Filler tile — closes out the bento row instead of leaving a gap */}
            <Link href="/register" className="sm:col-span-2 card group flex items-center justify-between gap-4 p-6 transition-all duration-500 hover:-translate-y-1"
              style={{ background: "var(--primary)", borderColor: "var(--primary)" }}>
              <div>
                <p className="text-[14px] font-semibold text-white mb-1">That&apos;s everything, see it live</p>
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
            <p className="text-[11px] font-semibold tracking-[0.12em] uppercase mb-3" style={{ color: "var(--primary)" }}>
              Why they join
            </p>
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
            <p className="text-[11px] font-semibold tracking-[0.12em] uppercase mb-3" style={{ color: "var(--primary)" }}>
              Getting started
            </p>
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
            <img src={content?.logoUrl || "/alumunion-mark.svg"} alt={content?.displayName ?? "Logo"} className="w-8 h-8 rounded-xl object-cover shrink-0" />
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
