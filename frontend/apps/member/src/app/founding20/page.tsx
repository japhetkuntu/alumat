import Link from "next/link";
import { ArrowLeft, ArrowRight } from "@alumni/ui";
import { MarketingFooter } from "../_marketing/footer";
import { FoundingForm } from "./founding-form";
import { StickyApply } from "./sticky-apply";

const WHAT_YOU_GET = [
  { title: "Your own digital community", body: "Branded around your institution." },
  { title: "Know your members", body: "Build a structured, continuously growing community directory." },
  { title: "Connect generations", body: "Bring year groups, chapters and generations into one network." },
  { title: "Mobilize your community", body: "Events, announcements, fundraising and dues." },
  { title: "Create opportunities", body: "Jobs, mentorship, professional networking and member opportunities." },
  { title: "Grow from scratch", body: "Use cohort ambassadors to rebuild communities even when no central member database exists." },
];

const BENEFITS = [
  "Free institutional setup",
  "Custom institution branding",
  "Member-data migration assistance",
  "Admin onboarding",
  "Year-group and chapter configuration",
  "Community-growth consultation",
  "Fundraising and dues configuration",
  "Priority onboarding support",
  "Direct access to the AlumUnion team",
  "Founding Institution recognition",
];

const STEPS = ["Express interest", "A 20-minute discovery call and demo", "We confirm your place", "Setup, done with you", "Launch to your members"];

/** Places already taken by live Founding institutions. Raise this as each one launches. */
const FILLED_PLACES = 0;

const eyebrow = "text-[13px] font-semibold uppercase tracking-[0.14em] text-primary";

export default function Founding20Page() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <header className="border-b border-border">
        <div className="section__inner flex h-16 items-center justify-between gap-4">
          <Link href="/" className="flex shrink-0 items-center">
            <img src="/alumunion-logo-horizontal.svg" alt="AlumUnion" width={1490} height={405} className="h-11 w-auto object-contain dark:rounded-sm dark:bg-white dark:px-2" />
          </Link>
          <Link href="/" className="flex items-center gap-1.5 text-[15px] font-medium text-muted-foreground hover:text-foreground">
            <ArrowLeft size={14} /> Back to home
          </Link>
        </div>
      </header>

      <main>
        <section id="f20-hero" className="border-b border-border">
          <div className="section__inner--wide grid gap-12 pb-10 pt-14 sm:pt-20 lg:grid-cols-[1.35fr_1fr] lg:items-center lg:gap-20 lg:pb-14 lg:pt-28">
            <div>
              <p className={eyebrow}>AlumUnion Founding 20</p>
              <h1 className="mt-6 text-[clamp(36px,5.6vw,76px)] font-bold leading-[1.04] tracking-[-0.03em] text-balance">
                Become one of AlumUnion&apos;s Founding 20 Institutions
              </h1>
              <p className="mt-7 max-w-[52ch] text-[18px] leading-relaxed text-muted-foreground sm:text-[21px]">
                We&apos;re partnering with 20 institutions and organized communities in Ghana to build stronger, more connected digital communities.
              </p>
              <a href="#apply" className="mt-10 inline-flex min-h-12 w-full items-center justify-center gap-2.5 bg-primary px-8 text-[16px] font-semibold text-primary-foreground hover:opacity-90 sm:w-auto">
                Apply for Founding 20 <ArrowRight size={17} />
              </a>
            </div>

            {/* One cell per place. Fill them in (FILLED_PLACES) as institutions go live. */}
            <figure aria-label={`${FILLED_PLACES} of 20 Founding places taken`} className="mx-auto w-full max-w-[460px] lg:max-w-none">
              <ol className="grid grid-cols-5 gap-2 sm:gap-3">
                {Array.from({ length: 20 }, (_, i) => (
                  <li
                    key={i}
                    className={`flex aspect-square items-end p-1.5 text-[11px] tabular-nums sm:p-2.5 sm:text-[13px] ${i < FILLED_PLACES ? "bg-primary text-primary-foreground" : "border border-foreground/25 text-muted-foreground"}`}
                  >
                    {String(i + 1).padStart(2, "0")}
                  </li>
                ))}
              </ol>
              <figcaption className="mt-4 text-[14px] text-muted-foreground sm:text-[15px]">
                <span className="font-semibold text-foreground">{20 - FILLED_PLACES} of 20</span> places open
              </figcaption>
            </figure>
          </div>

          <div className="section__inner--wide pb-10 lg:pb-12">
            <dl className="grid grid-cols-1 border-t border-border sm:grid-cols-3">
              {[["20 institutions", "A small first group, selected by us"], ["No setup cost", "We set it up with you"], ["Applications close", "30 October 2026"]].map(([t, d], i) => (
                <div key={t} className={`py-5 sm:py-6 ${i > 0 ? "border-t border-border sm:border-l sm:border-t-0 sm:pl-8" : ""} ${i < 2 ? "sm:pr-8" : ""}`}>
                  <dt className="text-[19px] font-semibold tracking-tight sm:text-[22px]">{t}</dt>
                  <dd className="mt-1 text-[14px] text-muted-foreground sm:text-[15px]">{d}</dd>
                </div>
              ))}
            </dl>
          </div>
        </section>

        <section className="border-b border-border bg-muted/40">
          <div className="section__inner py-14 sm:py-20">
            <h2 className="max-w-[24ch] text-[clamp(28px,3.4vw,44px)] font-bold leading-tight tracking-tight text-balance">
              Your community already exists. It just isn&apos;t connected.
            </h2>
            <p className="mt-6 max-w-[62ch] text-lg leading-relaxed text-muted-foreground">
              Members are spread across WhatsApp groups, spreadsheets, year groups, chapters and personal networks. AlumUnion brings that community together.
            </p>
          </div>
        </section>

        <section className="border-b border-border">
          <div className="section__inner py-14 sm:py-20">
            <p className={eyebrow}>01 / What your community gets</p>
            <dl className="mt-8 grid gap-x-12 sm:grid-cols-2">
              {WHAT_YOU_GET.map((x) => (
                <div key={x.title} className="border-t border-border py-6">
                  <dt className="text-xl font-semibold">{x.title}</dt>
                  <dd className="mt-1.5 text-[17px] leading-relaxed text-muted-foreground">{x.body}</dd>
                </div>
              ))}
            </dl>
          </div>
        </section>

        <section className="border-b border-border bg-muted/40">
          <div className="section__inner grid gap-10 py-14 sm:py-20 lg:grid-cols-[1fr_1.2fr] lg:gap-16">
            <div>
              <p className={eyebrow}>02 / What Founding 20 receive</p>
              <h2 className="mt-4 text-[clamp(28px,3.2vw,40px)] font-bold leading-tight tracking-tight text-balance">Hands-on, from the people who built it.</h2>
              <p className="mt-5 max-w-[44ch] text-[17px] leading-relaxed text-muted-foreground">
                Founding 20 institutions will also have an opportunity to influence AlumUnion&apos;s institutional roadmap through direct feedback.
              </p>
            </div>
            <ul className="grid gap-x-10 sm:grid-cols-2">
              {BENEFITS.map((b) => (
                <li key={b} className="border-t border-border py-3.5 text-[17px] font-medium">{b}</li>
              ))}
            </ul>
          </div>
        </section>

        <section className="border-b border-border">
          <div className="section__inner py-14 sm:py-20">
            <p className={eyebrow}>03 / How it works</p>
            <ol className="mt-10 grid gap-0 sm:grid-cols-2 lg:grid-cols-5">
              {STEPS.map((s, i) => (
                <li key={s} className="relative flex gap-4 pb-8 last:pb-0 lg:block lg:pb-0 lg:pr-6">
                  <span aria-hidden className="absolute left-[15px] top-9 bottom-0 w-px bg-border lg:hidden last:hidden" />
                  <span aria-hidden className="absolute left-9 right-3 top-[15px] hidden h-px bg-border lg:block last:hidden" />
                  <span className="relative z-10 flex h-8 w-8 shrink-0 items-center justify-center border border-foreground/70 bg-background text-[13px] font-semibold tabular-nums">{i + 1}</span>
                  <p className="pt-1 text-[17px] font-semibold leading-snug lg:mt-4 lg:pt-0">{s}</p>
                </li>
              ))}
            </ol>
          </div>
        </section>

        <section id="apply" className="scroll-mt-8 bg-muted/40">
          <div className="section__inner grid gap-10 py-14 sm:py-24 lg:grid-cols-[1fr_1.3fr] lg:gap-16">
            <div>
              <p className={eyebrow}>04 / Apply</p>
              <h2 className="mt-4 text-[clamp(28px,3.2vw,40px)] font-bold leading-tight tracking-tight text-balance">A few details, then we&apos;ll talk.</h2>
              <p className="mt-5 max-w-[40ch] text-[17px] leading-relaxed text-muted-foreground">
                It takes about two minutes. Once you apply, a member of our team will contact you personally. Applications close October 30, 2026.
              </p>
            </div>
            <FoundingForm />
          </div>
        </section>
      </main>
      <MarketingFooter />
      <StickyApply />
    </div>
  );
}
