"use client";

import Link from "next/link";
import { getMarketingShareId } from "@/lib/marketing-attribution";
import { useState } from "react";
import type { IconType as LucideIcon } from "@alumni/ui";
import { Menu, X, ArrowRight, ChevronRight, ChevronDown, Briefcase, Users, CreditCard, Globe, Heart, ShoppingBag, Trophy, Bell, FileText, Images, Building2, ShieldCheck, Rocket, Layer, Mail, MapPin, MessageCircleOff, SearchX, ShieldAlert, UserX, Wallet, CheckCircle2, PartyPopper, Crown, UserCheck, Upload, BookOpen, Megaphone, Users2, Stamp, Receipt, UsersRound, HandCoins, Button, Input, Label, Textarea, FormError, cn, Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@alumni/ui";
import { memberClient, handleApiError } from "@/lib/api-client";
import { MarketingFooter } from "./_marketing/footer";
import { ProductTour } from "./_marketing/product-tour";
import { WalkthroughForm } from "./_marketing/walkthrough-form";
import { FAQS } from "./_marketing/faqs";
import { INSTITUTION_AGREEMENT_VERSION } from "@alumni/ui";
import { JobsIllustration, MentorshipIllustration, ScatteredChatIllustration, DirectoryIllustration, FundraisingIllustration, EventsIllustration, StoreIllustration, AlbumsIllustration, SpotlightIllustration, BusinessIllustration, NotificationsIllustration, ServicesIllustration, UnknownAlumniIllustration, ManualReconciliationIllustration } from "./_marketing/product-panels";
import "./marketing-page.css";

const NAV_LINKS = [
  { label: "The problem", href: "#problems" },
  { label: "How we help", href: "#features" },
  { label: "How it works", href: "#how-it-works" },
  { label: "FAQ", href: "#faq" },
];

type Feature = { icon: LucideIcon; label: string; title: string; desc: string; big?: boolean; illustration: React.ComponentType<{ className?: string; tone?: "primary" | "accent" }> };

// Grouped into three jobs-to-be-done instead of one flat 11-card list — a
// visitor scanning the page gets a mental model ("oh, it does connection,
// growth, and money") in three headings before ever reading a single card,
// rather than needing to read all 11 titles to notice a pattern themselves.
const FEATURE_GROUPS: { label: string; blurb: string; items: Feature[] }[] = [
  {
    label: "Stay connected",
    blurb: "Your institution's community finds its way back to each other and to what's happening now.",
    items: [
      { icon: Users,       label: "Directory",     title: "Every member, one searchable list",    desc: "Name, class year, location, and contact details: members find each other in seconds.", illustration: DirectoryIllustration },
      { icon: Globe,       label: "Events",        title: "RSVPs for every gathering",             desc: "Reunions, dinners, chapter meetups, speech days, and programs, all in one shared calendar.", illustration: EventsIllustration },
      { icon: Images,      label: "Photo Albums",  title: "A living photo archive",                desc: "Staff upload photos from every event; members, supporters, and community leaders revisit the stories in one place.", illustration: AlbumsIllustration },
      { icon: Bell,        label: "Notifications", title: "Reach the right people, automatically", desc: "Jobs, campaigns, events, and updates: members choose exactly what reaches them.", illustration: NotificationsIllustration },
    ],
  },
  {
    label: "Grow together",
    blurb: "A community people want to stay in because opportunity brings them back.",
    items: [
      { icon: Briefcase,   label: "Careers",    title: "A jobs board for your community",      desc: "Employers post roles for members and supporters before they ever hit public boards.", illustration: JobsIllustration },
      { icon: Heart,       label: "Mentorship", title: "Built-in mentor matching",              desc: "Former students and experienced members connect with those just starting out.", illustration: MentorshipIllustration },
      { icon: Building2,   label: "Businesses", title: "A member business directory",            desc: "Members list their businesses; the community discovers and supports each other.", illustration: BusinessIllustration },
      { icon: Trophy,      label: "Spotlight",  title: "Celebrate your standout members",       desc: "Recognize members, leaders, supporters, and changemakers right on your community home page.", illustration: SpotlightIllustration },
    ],
  },
  {
    label: "Raise funds & offer services",
    blurb: "Money moves online with a record, instead of screenshots and trust.",
    items: [
      { icon: CreditCard,  label: "Fundraising", title: "Collect dues & fund projects", desc: "Online payments for campaigns, membership dues, renewals, and community support.", illustration: FundraisingIllustration },
      { icon: ShoppingBag, label: "Store",       title: "Sell branded merchandise",     desc: "An online store for association gear, with payment and order tracking built in.", illustration: StoreIllustration },
      { icon: FileText,    label: "Services",    title: "Offer any paid service, your way", desc: "Transcripts, letters, certificate reissues, or other requests: configure forms, collect payment online, and fulfill them from one place.", illustration: ServicesIllustration },
    ],
  },
];

// The pipeline behind building a community from scratch — flexible enough for
// alumni groups, churches, associations, nonprofits, and other organizations.
const NETWORK_PIPELINE: { icon: LucideIcon; label: string }[] = [
  { icon: Building2,     label: "Organization" },
  { icon: Layer,         label: "Groups & Chapters" },
  { icon: Crown,         label: "Community Leaders" },
  { icon: Users,         label: "Members" },
  { icon: UserCheck,     label: "Trusted Profiles" },
  { icon: Users2,        label: "Community" },
];

// The concrete, no-friction version of "we'll build it for you" — every
// step your team actually does for an institution that signs up, not just the
// software's part of it.
const WHITE_GLOVE_STEPS: { icon: LucideIcon; title: string; desc: string }[] = [
  { icon: Globe,        title: "Create the community portal",   desc: "Your own branded subdomain, live and ready." },
  { icon: Stamp,        title: "Add institutional branding",    desc: "Colors, logo, and identity, applied throughout." },
  { icon: Receipt,      title: "Configure the community",       desc: "Membership dues, campaigns, and policies set up for you." },
  { icon: UsersRound,   title: "Organize groups and chapters",  desc: "Your community's groups, chapters, or cohorts, structured and ready to grow." },
  { icon: ShieldCheck,  title: "Set up administrators",         desc: "Your executives get accounts and the right access from day one." },
  { icon: Upload,       title: "Import existing member data",   desc: "Spreadsheets, old records, and contact lists, brought in for you." },
  { icon: BookOpen,     title: "Train the community team",      desc: "A walkthrough for whoever will run the portal day to day." },
  { icon: Megaphone,    title: "Help launch to your members",   desc: "Guidance and materials for announcing the community to your network." },
  { icon: HandCoins,    title: "Help run the first campaign",   desc: "We help you plan and launch your first dues drive or fundraiser." },
];

// The three problems community organizations commonly face — sold first,
// before a single feature is named. Each ties to real feature labels used in
// FEATURE_GROUPS below, so a visitor who reads this section and then scrolls
// into "Here's how" sees the same names come back, not a new vocabulary.
type ProblemItem = {
  n: string; icon: LucideIcon; eyebrow: string; title: string; desc: string;
  fix: string; chips: string[];
  illustration: React.ComponentType<{ className?: string; tone?: "primary" | "accent" }> | typeof ScatteredChatIllustration;
};

const PROBLEMS: ProblemItem[] = [
  {
    n: "01", icon: MessageCircleOff, eyebrow: "Problem one", title: "Your community is scattered",
    desc: "A WhatsApp group for one chapter. A Facebook group nobody moderates. A spreadsheet that is a year out of date. Different groups, channels, and lists, with no single place where your community actually lives.",
    fix: "One searchable directory organized by group, chapter, and role, plus notifications that reach people instead of disappearing into chat.",
    chips: ["Directory", "Notifications", "Events"],
    illustration: ScatteredChatIllustration,
  },
  {
    n: "02", icon: SearchX, eyebrow: "Problem two", title: "Member records are incomplete",
    desc: "Ask “how many members do we have, and who are they?” and the honest answer is a guess, an old headcount, or a folder of screenshots. There's no reliable, verified record of your community.",
    fix: "A member database your institution approves and owns, searchable by name, year and location.",
    chips: ["Directory", "Businesses"],
    illustration: UnknownAlumniIllustration,
  },
  {
    n: "03", icon: Wallet, eyebrow: "Problem three", title: "Contributions are still tracked by hand",
    desc: "You need GH₵100 from 500 members. Someone drafts a broadcast message. People pay however they can and send screenshots as proof. Someone reconciles every one by hand. Two weeks in, someone asks “how much have we raised?” and the honest answer is “let me check.”",
    fix: "Built-in campaigns and dues with online payments through a licensed provider, a record of every payment, and a running total from the payments recorded.",
    chips: ["Fundraising", "Store", "Services"],
    illustration: ManualReconciliationIllustration,
  },
];

const WHATSAPP_PROBLEMS = [
  { icon: UserX,           title: "Caps at 1,024 members", desc: "WhatsApp groups max out at 1,024 people. Even larger community spaces eventually become difficult to organize and grow." },
  { icon: SearchX,         title: "Search finds words, not people",        desc: "Looking for last year's fundraiser announcement, or a member by chapter or city? WhatsApp can search messages, but there's no directory to filter, so you scroll and ask around." },
  { icon: ShieldAlert,     title: "Real fraud risk",        desc: "UK Action Fraud logged 636 reports of WhatsApp group-chat scams in H1 2024 alone, often someone impersonating a member to solicit money." },
  { icon: MessageCircleOff, title: "No directory, no data", desc: "No member directory, no RSVP tracking, no dues collection, and no engagement analytics in one place." },
];

interface LeadForm {
  institutionName: string;
  website: string;
  contactName: string;
  contactRole: string;
  contactEmail: string;
  contactPhone: string;
  country: string;
  estimatedMemberCount: string;
  organizationType: string;
  primaryGoals: string[];
  currentMemberManagement: string;
  dataImportStatus: string;
  preferredContactChannel: string;
  preferredContactTime: string;
  timeZone: string;
  message: string;
  agreementAccepted: boolean;
}

const MEMBER_COUNT_RANGES = ["0 – 100", "101 – 500", "501 – 999", "1,000+"];
const ORGANIZATION_TYPES = ["Alumni association", "University or college", "School network", "Professional association", "Nonprofit or NGO", "Faith-based organization", "Membership organization", "Other"];
const PRIMARY_GOALS = ["Member directory", "Member registration and approvals", "Membership dues", "Contributions and fundraising", "Events and RSVPs", "Communities or chapters", "Mentorship", "Jobs and opportunities", "News and announcements", "Digital resources", "Merchandise/store", "Official document or service requests"];
const CONTACT_ROLES = ["Executive leadership", "Community or membership office", "IT or digital transformation", "Finance", "Communications or marketing", "Programs or member services", "Other"];
const MANAGEMENT_OPTIONS = ["Spreadsheet", "WhatsApp groups", "Existing alumni or community software", "CRM", "Student information system", "Website or custom system", "Mostly manual processes", "Other"];
const CONTACT_CHANNELS = ["Email", "Phone call", "WhatsApp", "Video call"];
const CONTACT_TIMES = ["Morning", "Afternoon", "Evening", "Flexible"];

const EMPTY_LEAD: LeadForm = {
  institutionName: "", website: "", contactName: "", contactRole: "", contactEmail: "", contactPhone: "",
  country: "Ghana", estimatedMemberCount: "", organizationType: "", primaryGoals: [],
  currentMemberManagement: "", dataImportStatus: "", preferredContactChannel: "",
  preferredContactTime: "", timeZone: "", message: "", agreementAccepted: false,
};

type StoryPhotoData = { alt:string; url:string };

const STORY_PHOTOS: Record<"hero" | "collaboration" | "onboarding", StoryPhotoData> = {
  hero: {
    alt: "A diverse circle of hands brought together in solidarity",
    url: "https://images.unsplash.com/photo-1740065592719-052d3e5ec6fb?auto=format&fit=crop&w=1600&q=84",
  },
  collaboration: {
    alt: "Black professionals working together across a laptop and phone",
    url: "https://images.unsplash.com/photo-1573164574230-db1d5e960238?auto=format&fit=crop&w=1800&q=84",
  },
  onboarding: {
    alt: "Hands raised together as a symbol of trust and partnership",
    url: "https://images.unsplash.com/photo-1682353213514-81a621a39d8b?auto=format&fit=crop&w=1400&q=84",
  },
};

function StoryPhoto({ photo, className = "", priority = false }: { photo?: StoryPhotoData; className?: string; priority?: boolean }) {
  if (!photo) return <div className={`mk-story-photo mk-story-photo-loading ${className}`} aria-hidden="true" />;
  return (
    <figure className={`mk-story-photo ${className}`}>
      <img src={photo.url} alt={photo.alt} loading={priority ? "eager" : "lazy"} fetchPriority={priority ? "high" : "auto"} />
      <div className="mk-story-photo-overlay" aria-hidden="true" />
    </figure>
  );
}

function OnboardingForm() {
  const [form, setForm] = useState<LeadForm>(EMPTY_LEAD);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [step, setStep] = useState(1);

  const set = <K extends keyof LeadForm>(key: K, value: LeadForm[K]) => setForm((f) => ({ ...f, [key]: value }));
  const toggleGoal = (goal: string) => set("primaryGoals", form.primaryGoals.includes(goal)
    ? form.primaryGoals.filter((value) => value !== goal)
    : [...form.primaryGoals, goal]);

  const validateBasics = (): boolean => {
    if (!form.institutionName.trim()) { setError("Your institution's name is required."); return false; }
    if (!form.contactName.trim()) { setError("A contact name is required."); return false; }
    if (!form.contactEmail.trim() || !/^\S+@\S+\.\S+$/.test(form.contactEmail.trim())) { setError("A valid contact email is required."); return false; }
    setError(null);
    return true;
  };

  const goToNextStep = () => {
    if (step === 1 && !validateBasics()) return;
    setError(null);
    setStep((current) => Math.min(3, current + 1));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validateBasics()) {
      setStep(1);
      return;
    }
    if (!form.contactRole) { setError("Please choose your role at the institution. It is recorded with your acceptance of the agreement."); setStep(2); return; }
    if (!form.agreementAccepted) { setError("Please accept the Institution Agreement to send your request."); return; }
    setSubmitting(true);
    try {
      await memberClient.post("/public/onboarding-leads", {
        marketingShareId: getMarketingShareId(),
        agreementAccepted: true,
        agreementVersion: INSTITUTION_AGREEMENT_VERSION,
        institutionName: form.institutionName.trim(),
        website: form.website.trim() || undefined,
        contactName: form.contactName.trim(),
        contactRole: form.contactRole || undefined,
        contactEmail: form.contactEmail.trim(),
        contactPhone: form.contactPhone.trim() || undefined,
        country: form.country.trim() || undefined,
        estimatedMemberCount: form.estimatedMemberCount.trim() || undefined,
        organizationType: form.organizationType || undefined,
        primaryGoals: form.primaryGoals,
        currentMemberManagement: form.currentMemberManagement || undefined,
        dataImportStatus: form.dataImportStatus || undefined,
        preferredContactChannel: form.preferredContactChannel || undefined,
        preferredContactTime: form.preferredContactTime || undefined,
        timeZone: form.timeZone.trim() || undefined,
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
      <div className="card p-8 sm:p-10 text-center flex flex-col items-center">
        <div className="w-16 h-16 rounded-2xl flex items-center justify-center mb-5" style={{ background: "var(--card)", border: "1px solid var(--border-emphasis, var(--border))" }}>
          <PartyPopper size={28} style={{ color: "var(--primary)" }} />
        </div>
        <p className="text-[13px] font-bold uppercase tracking-[0.12em] mb-2" style={{ color: "var(--primary)" }}>You&apos;re on your way</p>
        <h3 className="font-[family-name:var(--font-display)] mb-2.5" style={{ fontSize: "1.5rem", color: "var(--foreground)" }}>Let&apos;s build something your community will love.</h3>
        <p className="max-w-[42ch]" style={{ color: "var(--muted-foreground)", fontSize: "0.925rem", lineHeight: 1.7 }}>
          We&apos;ll reach out to <strong style={{ color: "var(--foreground)" }}>{form.contactEmail}</strong> within one business day with a thoughtful next step for {form.institutionName}.
        </p>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-2.5 w-full max-w-[560px] mt-7 text-left">
          {[
            ["01", "We review your goals", "So the first conversation starts with what matters to you."],
            ["02", "We shape your setup", "Your community gets a clear, practical starting point."],
            ["03", "You get a next step", "No pressure, no handoff maze, no obligation."],
          ].map(([number, title, description]) => (
            <div key={number} className="rounded-lg border p-3.5" style={{ borderColor: "var(--border)", background: "var(--secondary)" }}>
              <span className="text-[13px] font-bold" style={{ color: "var(--primary)" }}>{number}</span>
              <p className="text-[15px] font-semibold mt-1" style={{ color: "var(--foreground)" }}>{title}</p>
              <p className="text-[14px] leading-relaxed mt-1" style={{ color: "var(--muted-foreground)" }}>{description}</p>
            </div>
          ))}
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="card overflow-hidden">
      <div className="px-6 pt-6 sm:px-8 sm:pt-8">
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-[13px] font-bold uppercase tracking-[0.12em]" style={{ color: "var(--primary)" }}>Start your institution journey</p>
            <h3 className="font-[family-name:var(--font-display)] text-[1.35rem] sm:text-[1.55rem] font-bold mt-1" style={{ color: "var(--foreground)" }}>
              {step === 1 ? "Let’s start with you" : step === 2 ? "Make it yours" : "How should we connect?"}
            </h3>
            <p className="text-[16px] mt-1 max-w-[48ch]" style={{ color: "var(--muted-foreground)" }}>
              {step === 1 ? "Tell us who you are and which community you represent." : step === 2 ? "Pick what matters most. We’ll tailor the first conversation around it." : "A few final details help us make your welcome personal."}
            </p>
          </div>
          <span className="shrink-0 text-[14px] font-semibold" style={{ color: "var(--muted-foreground)" }}>{step} of 3</span>
        </div>
        <div className="flex gap-1.5 mt-5" aria-label={`Step ${step} of 3`}>
          {[1, 2, 3].map((item) => <span key={item} className="h-1.5 flex-1 rounded-full transition-colors duration-300" style={{ background: item <= step ? "var(--primary)" : "var(--border)" }} />)}
        </div>
      </div>

      <div className="p-6 sm:p-8 pt-5 sm:pt-6 space-y-5">
      {step === 1 && (
        <>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <Label required>Institution name</Label>
          <Input value={form.institutionName} onChange={(e) => set("institutionName", e.target.value)} placeholder="e.g. Grace Community Church" />
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
        </>
      )}
      {step === 2 && (
        <>
      <div className="pt-2 border-t" style={{ borderColor: "var(--border)" }}>
        <p className="text-[16px] font-semibold" style={{ color: "var(--foreground)" }}>Help us understand your community</p>
        <p className="text-[14px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>Choose your role, then add any optional details to help us prepare a more useful first conversation.</p>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <Label required>Your role</Label>
          <Select value={form.contactRole || undefined} onValueChange={(v) => set("contactRole", v)}>
            <SelectTrigger className="w-full"><SelectValue placeholder="What is your role?" /></SelectTrigger>
            <SelectContent className="mk-select-menu">{CONTACT_ROLES.map((role) => <SelectItem className="mk-select-option" key={role} value={role}>{role}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div>
          <Label>Organization website (optional)</Label>
          <Input type="url" value={form.website} onChange={(e) => set("website", e.target.value)} placeholder="https://yourorganization.org" />
        </div>
      </div>
      <div>
        <Label>What type of community are you building? (optional)</Label>
        <Select value={form.organizationType || undefined} onValueChange={(v) => set("organizationType", v)}>
          <SelectTrigger className="w-full"><SelectValue placeholder="Choose the closest fit" /></SelectTrigger>
          <SelectContent className="mk-select-menu">{ORGANIZATION_TYPES.map((type) => <SelectItem className="mk-select-option" key={type} value={type}>{type}</SelectItem>)}</SelectContent>
        </Select>
      </div>
      <fieldset>
        <legend className="text-[16px] font-semibold mb-2" style={{ color: "var(--foreground)" }}>What do you want to accomplish? <span className="font-normal" style={{ color: "var(--muted-foreground)" }}>(optional)</span></legend>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
          {PRIMARY_GOALS.map((goal) => (
            <label key={goal} className={cn("flex items-center gap-2.5 rounded-md border px-3 py-2.5 text-[15px] cursor-pointer transition-colors", form.primaryGoals.includes(goal) ? "border-primary bg-primary/5" : "border-border hover:bg-muted/50")}>
              <input type="checkbox" checked={form.primaryGoals.includes(goal)} onChange={() => toggleGoal(goal)} className="accent-primary" />
              <span>{goal}</span>
            </label>
          ))}
        </div>
      </fieldset>
      <div className="rounded-lg px-4 py-3 text-[15px]" style={{ background: "var(--secondary)", color: "var(--muted-foreground)" }}>
        You can choose as many as you like — this helps us show up with relevant ideas, not a generic demo.
      </div>
        </>
      )}
      {step === 3 && (
        <>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <Label>How do you manage members today? (optional)</Label>
          <Select value={form.currentMemberManagement || undefined} onValueChange={(v) => set("currentMemberManagement", v)}>
            <SelectTrigger className="w-full"><SelectValue placeholder="Choose your current process" /></SelectTrigger>
            <SelectContent className="mk-select-menu">{MANAGEMENT_OPTIONS.map((option) => <SelectItem className="mk-select-option" key={option} value={option}>{option}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div>
          <Label>Do you need to import existing records? (optional)</Label>
          <Select value={form.dataImportStatus || undefined} onValueChange={(v) => set("dataImportStatus", v)}>
            <SelectTrigger className="w-full"><SelectValue placeholder="Choose one" /></SelectTrigger>
            <SelectContent className="mk-select-menu">{["Yes, organized and ready", "Yes, but it needs cleaning", "No", "Not sure yet"].map((option) => <SelectItem className="mk-select-option" key={option} value={option}>{option}</SelectItem>)}</SelectContent>
          </Select>
        </div>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <Label>Phone (optional)</Label>
          <Input type="tel" value={form.contactPhone} onChange={(e) => set("contactPhone", e.target.value)} placeholder="+233 ..." />
        </div>
        <div>
          <Label>Roughly how many members? (optional)</Label>
          <Select value={form.estimatedMemberCount || undefined} onValueChange={(v) => set("estimatedMemberCount", v)}>
            <SelectTrigger className="w-full">
              <SelectValue placeholder="Select a range" />
            </SelectTrigger>
            <SelectContent className="mk-select-menu">
              {MEMBER_COUNT_RANGES.map((range) => (
                <SelectItem className="mk-select-option" key={range} value={range}>{range}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div>
          <Label>Preferred follow-up (optional)</Label>
          <Select value={form.preferredContactChannel || undefined} onValueChange={(v) => set("preferredContactChannel", v)}>
            <SelectTrigger className="w-full"><SelectValue placeholder="How should we reach you?" /></SelectTrigger>
            <SelectContent className="mk-select-menu">{CONTACT_CHANNELS.map((channel) => <SelectItem className="mk-select-option" key={channel} value={channel}>{channel}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div>
          <Label>Best time (optional)</Label>
          <Select value={form.preferredContactTime || undefined} onValueChange={(v) => set("preferredContactTime", v)}>
            <SelectTrigger className="w-full"><SelectValue placeholder="When works best?" /></SelectTrigger>
            <SelectContent className="mk-select-menu">{CONTACT_TIMES.map((time) => <SelectItem className="mk-select-option" key={time} value={time}>{time}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div>
          <Label>Time zone (optional)</Label>
          <Input value={form.timeZone} onChange={(e) => set("timeZone", e.target.value)} placeholder="e.g. GMT" />
        </div>
      </div>
      <div>
        <Label>Tell us a bit more (optional)</Label>
        <Textarea value={form.message} onChange={(e) => set("message", e.target.value)} placeholder="Anything else you want us to know? A launch goal, a challenge, or a big idea." rows={4} />
      </div>
        </>
      )}
      {step === 3 && (
        <label className="flex cursor-pointer items-start gap-3 border p-4 text-[16px] leading-relaxed" style={{ borderColor: form.agreementAccepted ? "var(--primary)" : "var(--border)", background: "var(--card)" }}>
          <input type="checkbox" className="mt-1 h-4 w-4 shrink-0 accent-primary" checked={form.agreementAccepted} onChange={(e) => set("agreementAccepted", e.target.checked)} />
          <span style={{ color: "var(--foreground)" }}>
            I confirm I am authorised to act for {form.institutionName.trim() || "this institution"}, and I accept the{" "}
            <Link href="/institution-agreement" target="_blank" className="font-semibold underline" style={{ color: "var(--primary)" }}>Institution Agreement</Link>.
            <span className="mt-1 block text-[14px]" style={{ color: "var(--muted-foreground)" }}>
              This covers how members&apos; personal data is handled, who is responsible for what, and that the platform is free for your institution. We record who accepted, when, and the version.
            </span>
          </span>
        </label>
      )}
      <FormError message={error} />
      <div className="flex items-center justify-between gap-3 pt-1">
        {step > 1 ? <Button type="button" variant="outline" onClick={() => { setError(null); setStep((current) => current - 1); }}>Back</Button> : <span />}
        {step < 3 ? (
          <Button type="button" className="font-semibold gap-2" onClick={goToNextStep}>{step === 1 ? "Next: choose your focus" : "Next: contact details"} <ArrowRight size={15} /></Button>
        ) : (
          <Button type="submit" className="font-semibold gap-2" isLoading={submitting} loadingText="Sending your request...">Get started, free <ArrowRight size={15} /></Button>
        )}
      </div>
      <p className="text-center text-[14px]" style={{ color: "var(--muted-foreground)" }}><span style={{ color: "var(--primary)" }}>Free for your institution.</span> Your request includes acceptance of the Institution Agreement.</p>
      </div>
    </form>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PAGE
   ───────────────────────────────────────────────────────────────────────── */

export default function PlatformMarketingPage() {
  const [menuOpen, setMenuOpen] = useState(false);
  const [enquiry, setEnquiry] = useState<"onboarding" | "walkthrough">("onboarding");
  return (
    <div className="marketing-site">
      <header className="mk-nav">
        <div className="mk-wrap mk-nav-inner">
          <Link href="/" className="mk-logo"><img src="/alumunion-logo-horizontal.svg" alt="AlumUnion" width={1870} height={420} /></Link>
          <nav aria-label="Primary" className="mk-desktop-nav">{NAV_LINKS.map(link => <a key={link.href} href={link.href}>{link.label}</a>)}</nav>
          <a href="#onboard" className="mk-button mk-nav-cta">Get started, free <ArrowRight size={14} /></a>
          <button type="button" className="mk-menu" aria-label={menuOpen ? "Close navigation" : "Open navigation"} aria-expanded={menuOpen} aria-controls="marketing-navigation" onClick={() => setMenuOpen(!menuOpen)}>{menuOpen ? <X size={20} /> : <Menu size={20} />}</button>
        </div>
        {menuOpen && <nav id="marketing-navigation" aria-label="Mobile navigation" className="mk-mobile-nav mk-wrap">{NAV_LINKS.map(link => <a key={link.href} href={link.href} onClick={() => setMenuOpen(false)}>{link.label}</a>)}<a href="#onboard" onClick={() => setMenuOpen(false)}>Get started, free →</a></nav>}
      </header>
      <main>
        <section className="mk-hero">
          <div className="mk-hero-rings" aria-hidden="true"><span /><span /><span /></div>
          <div className="mk-wrap mk-hero-content">
            <div className="mk-hero-copy"><p className="mk-eyebrow">Community infrastructure, built with you</p><h1>Give your community a home, <span>at no cost</span> to your institution.</h1>
            <p className="mk-hero-description">AlumUnion helps institutions build, organize, and grow thriving communities, including alumni, former students, members, supporters, and stakeholders.</p>
            <div className="mk-hero-actions"><a href="#onboard" className="mk-button mk-button-large">Build my community — free <ArrowRight size={17} /></a><a href="#product" className="mk-text-link">See the member experience <ChevronDown size={14} /></a></div>
            <div className="mk-hero-assurances"><span><ShieldCheck size={16} />Free for your institution</span><span><Rocket size={16} />We handle setup</span><span><Users size={16} />Your branding, your community</span></div></div>
            <StoryPhoto photo={STORY_PHOTOS.hero} className="mk-hero-photo" priority />
          </div>
        </section>
        <section id="product" className="mk-wrap mk-product-section">
          <div className="mk-product-heading"><div><p className="mk-eyebrow">The member experience</p><h2>See what your community actually gets.</h2></div><span className="mk-demo-label">Interactive preview · example data</span></div>
          <div className="mk-product-frame"><ProductTour /></div>
        </section>
        <section id="problems" className="mk-section mk-problems">
          <div className="mk-wrap">
            <div className="mk-section-heading"><p className="mk-eyebrow">The daily reality</p><h2>Your community should not run on scattered chats, stale spreadsheets and payment screenshots.</h2><p>The problem is not effort. Your team simply lacks one trusted place for people, activity and money.</p></div>
            <div className="mk-problem-list">{PROBLEMS.map((item,index) => <article key={item.n} className={`mk-problem ${index % 2 ? "mk-problem-reverse" : ""}`}>
              <div className="mk-problem-visual"><span className="mk-problem-number" aria-hidden="true">{item.n}</span><item.illustration className="w-full" /><p>Illustrative product example</p></div>
              <div className="mk-problem-copy"><p className="mk-eyebrow">{item.eyebrow}</p><h3>{item.title}</h3><p>{item.desc}</p><div className="mk-problem-fix"><ArrowRight size={17} /><p>{item.fix}</p></div><div className="mk-chips">{item.chips.map(chip => <span key={chip}>{chip}</span>)}</div></div>
            </article>)}</div>
            <details className="mk-whatsapp" open><summary>Specifically, if you’re running this over WhatsApp</summary><div className="mk-whatsapp-grid">{WHATSAPP_PROBLEMS.map(item => <div key={item.title}><item.icon size={18} /><h3>{item.title}</h3><p>{item.desc}</p></div>)}</div><Link href="/why-not-whatsapp" className="mk-text-link">See the full comparison <ArrowRight size={14} /></Link></details>
          </div>
        </section>
        <section id="features" className="mk-section">
          <div className="mk-wrap">
            <div className="mk-section-heading"><p className="mk-eyebrow">One connected system</p><h2>Everything your community needs to connect and participate.</h2><p>People find each other, discover what matters and take action. Your team gets reliable records without chasing updates across multiple tools.</p></div>
            <div className="mk-feature-groups">{FEATURE_GROUPS.map((group,index) => <section key={group.label} className="mk-feature-group">
              <div className="mk-group-heading"><span>0{index+1}</span><div><h3>{group.label}</h3><p>{group.blurb}</p></div></div>
              <div className={`mk-feature-grid ${group.items.length === 3 ? "mk-feature-grid-three" : ""}`}>{group.items.map(feature => <article key={feature.title} className="mk-feature">
                <div className="mk-feature-visual"><feature.illustration className="w-full" /></div>
                <div className="mk-feature-copy"><p className="mk-eyebrow"><feature.icon size={14} />{feature.label}</p><h4>{feature.title}</h4><p>{feature.desc}</p></div>
              </article>)}</div>
            </section>)}</div>
            <p className="mk-example-note">Interface examples illustrate the features. Names, dates and amounts shown are sample data.</p>
            <a href="#onboard" className="mk-feature-cta"><div><h3>Give your community a place worth returning to.</h3><p>We will help you set it up, at no cost to your institution.</p></div><span>Start building <ArrowRight size={17} /></span></a>
          </div>
        </section>
        <section id="how-it-works" className="mk-section mk-setup">
          <div className="mk-wrap">
            <div className="mk-section-heading mk-heading-centred"><p className="mk-eyebrow">Start from where you are</p><h2>Begin with the people you have. Grow from there.</h2><p>We help you bring existing records together, organise the groups that matter and build trust one step at a time.</p></div>
            <div className="mk-pipeline" aria-label="How your community is organised">{NETWORK_PIPELINE.map((node,index) => <div key={node.label} className="mk-pipeline-node"><span className={index===2 ? "mk-pipeline-icon mk-pipeline-highlight" : "mk-pipeline-icon"}><node.icon size={23} /></span><p>{node.label}</p>{index < NETWORK_PIPELINE.length-1 && <ChevronRight size={17} className="mk-pipeline-arrow" />}</div>)}</div>
            <p className="mk-pipeline-description">Each group or chapter can have its own leader, responsible for bringing people in and keeping them connected, so the work spreads across your community instead of landing on one overworked administrator.</p>
            <StoryPhoto photo={STORY_PHOTOS.collaboration} className="mk-wide-story-photo" />
            <div className="mk-offer-heading"><h3>We do the setup with you.</h3><p>From your first imported record to your first live campaign, our team helps at every step.</p></div>
            <div className="mk-setup-grid">{WHITE_GLOVE_STEPS.map((step,index) => <article key={step.title} className="mk-setup-step"><div className="mk-step-top"><span>0{index+1}</span><step.icon size={20} /></div><h4>{step.title}</h4><p>{step.desc}</p></article>)}</div>
          </div>
        </section>
        <section id="costs" className="mk-free-section">
          <div className="mk-wrap mk-free-grid">
            <div><p className="mk-eyebrow">Simple pricing</p><h2>Your institution pays GH₵0.</h2><p className="mk-free-description">No setup fee. No subscription. No charge per member. Your institution gets the complete platform without another software bill.</p><a href="#onboard" className="mk-button mk-button-white">Build my community <ArrowRight size={16} /></a></div>
            <div className="mk-zero"><div>GH₵<span>0</span></div><p>to set up and run your portal</p><ul><li><CheckCircle2 size={17} />No setup fee</li><li><CheckCircle2 size={17} />No subscription</li><li><CheckCircle2 size={17} />No per-member charge</li></ul></div>
          </div>
          <div className="mk-wrap"><p className="mk-payment-note">Your portal is free. Fees apply to online payments and are shown before payment.</p></div>
        </section>
        <section id="onboard" className="mk-section mk-enquiry">
          <div className="mk-wrap mk-enquiry-grid">
            <div className="mk-enquiry-copy"><StoryPhoto photo={STORY_PHOTOS.onboarding} className="mk-enquiry-photo" /><p className="mk-eyebrow">Tell us what you want to build</p><h2>Your community can have a better home.</h2><p>Share where you are today and what you want to improve. We will respond with a practical setup plan for your institution.</p><div className="mk-contact-points"><span><Mail size={17} />A reply within one business day</span><span><ShieldCheck size={17} />No setup or subscription fee</span><span><MapPin size={17} />Built for communities everywhere</span></div><a href="mailto:hello@alumunion.com" className="mk-text-link">hello@alumunion.com <ArrowRight size={14} /></a></div>
            <div className="mk-form-panel"><div className="mk-form-choice" aria-label="Choose your request type"><button type="button" aria-pressed={enquiry === "onboarding"} onClick={() => setEnquiry("onboarding")}>Onboard my institution</button><button type="button" aria-pressed={enquiry === "walkthrough"} onClick={() => setEnquiry("walkthrough")}>See a walkthrough first</button></div>{enquiry === "onboarding" ? <OnboardingForm /> : <WalkthroughForm />}</div>
          </div>
        </section>
        <section id="faq" className="mk-section"><div className="mk-wrap mk-faq-grid"><div><p className="mk-eyebrow">A few things you might ask</p><h2>Frequently asked questions.</h2><p className="mk-faq-intro">Still have a question? Our team can walk you through it.</p><a href="mailto:hello@alumunion.com" className="mk-text-link">Talk to us <ArrowRight size={14} /></a></div><div>{FAQS.map(item => <details key={item.q} className="mk-faq"><summary>{item.q}</summary><p>{item.a}</p></details>)}</div></div></section>
      </main>
      <MarketingFooter />
    </div>
  );
}
