"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, useSyncExternalStore } from "react";
import { useQuery } from "@tanstack/react-query";
import { Briefcase, Button, Camera, Check, ChevronRight, FileText, GraduationCap, Linkedin, MapPin, Progress, Smartphone, Sparkles, X, type IconType } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { useFeatures } from "@/components/member/member-layout";
import { memberClient } from "@/lib/api-client";
import { getMyProfile, type MemberProfileResponse } from "@/lib/member-api";

export interface ProfileStep {
  id: string;
  /** One or two words, for the "done" line. */
  label: string;
  /** The field on the profile page this step takes the member to. */
  target: string;
  weight: number;
  /** What to do, short enough for a button. */
  action: string;
  /** Why it is worth doing, in the member's terms. */
  why: string;
  done: boolean;
}

/** What is filled in on a member's profile and what is not, as a percentage plus the steps still open (biggest first). Only steps that apply to this community are counted. */
export function useProfileStrength() {
  const features = useFeatures();
  const profile = useQuery({ queryKey: ["m-profile"], queryFn: getMyProfile });
  const theme = useQuery({
    queryKey: ["m-programs-of-study"],
    queryFn: async () => (await memberClient.get<{ data: { programOfStudyEnabled: boolean; programsOfStudy: string[] } }>("/public/institution/theme")).data.data,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
  const p: MemberProfileResponse | undefined = profile.data;
  if (!p) return { ready: false as const };

  const inDirectory = features.enabled("Directory");
  const steps: ProfileStep[] = [
    { id: "photo", label: "Photo", target: "profile-photo", weight: 20, action: "Add a photo", why: inDirectory ? "So people you studied with recognise you in the directory." : "So people recognise you.", done: !!p.profilePictureUrl },
    { id: "bio", label: "Short bio", target: "bio", weight: 15, action: "Write a line about you", why: "It is the first thing people read when they open your profile.", done: !!p.bio?.trim() },
    { id: "work", label: "Work", target: p.company?.trim() ? "jobTitle" : "company", weight: 15, action: "Say where you work", why: "Helps members and the institution connect you with the right people and opportunities.", done: !!(p.company?.trim() && p.jobTitle?.trim()) },
    { id: "location", label: "City", target: "location", weight: 10, action: "Add your city", why: features.enabled("AlumniMap") ? "Members near you can find you, and you them." : "So people nearby can find you.", done: !!p.location?.trim() },
    { id: "skills", label: "Skills", target: "skills", weight: 15, action: "Add skills and interests", why: "Get matched with people, jobs and mentors that fit.", done: (p.skills?.length ?? 0) + (p.interests?.length ?? 0) > 0 },
    { id: "phone", label: "Phone", target: "phone", weight: 10, action: "Add your phone number", why: "So you get reminders and updates that matter. You choose whether anyone else sees it.", done: !!p.phone?.trim() },
    { id: "linkedin", label: "LinkedIn", target: "linkedInUrl", weight: 5, action: "Link your LinkedIn", why: "One tap for others to connect with you.", done: !!p.linkedInUrl?.trim() },
    ...(theme.data?.programOfStudyEnabled
      ? [{ id: "program", label: "Program", target: "program", weight: 10, action: "Choose your program", why: "Find people who studied what you did.", done: !!p.program?.trim() } satisfies ProfileStep]
      : []),
  ];
  const total = steps.reduce((n, s) => n + s.weight, 0);
  const got = steps.filter((s) => s.done).reduce((n, s) => n + s.weight, 0);
  return {
    ready: true as const,
    firstName: p.firstName,
    percent: Math.round((got / total) * 100),
    open: steps.filter((s) => !s.done).sort((a, b) => b.weight - a.weight),
    done: steps.filter((s) => s.done),
    count: steps.length,
    bonus: (s: ProfileStep) => Math.round((s.weight / total) * 100),
  };
}

function snoozeKey(userId: string) { return `profile-strength-snoozed:${userId}`; }
const SNOOZE_DAYS = 3;

function subscribeSnooze(notify: () => void) {
  window.addEventListener("storage", notify);
  window.addEventListener("profile-strength-snoozed", notify);
  return () => { window.removeEventListener("storage", notify); window.removeEventListener("profile-strength-snoozed", notify); };
}

/**
 * The dashboard's nudge to finish a profile. It says how far along the member is, what each missing piece is worth to them,
 * and gives one obvious next step. Gone at 100%, and a member who is not in the mood can put it away for a few days.
 */
export function ProfileStrengthCard() {
  const { user } = useAuth();
  const router = useRouter();
  const strength = useProfileStrength();
  const snoozed = useSyncExternalStore(subscribeSnooze, () => {
    if (!user?.id) return false;
    try {
      const until = Number(localStorage.getItem(snoozeKey(user.id)) ?? 0);
      return until > Date.now();
    } catch { return false; }
  }, () => false);
  const [away, setAway] = useState(false);

  if (!strength.ready || strength.open.length === 0 || snoozed || away) return null;
  const { firstName, percent, open, done, count, bonus } = strength;
  const [next, ...rest] = open;
  const headline = percent < 40 ? `${firstName}, let your community find you` : percent < 80 ? `${firstName}, you are ${percent}% of the way there` : `Almost done, ${firstName}`;
  const NextIcon = STEP_ICON[next.id] ?? Sparkles;

  function snooze() {
    setAway(true);
    if (!user?.id) return;
    try { localStorage.setItem(snoozeKey(user.id), String(Date.now() + SNOOZE_DAYS * 86_400_000)); } catch { /* it just comes back next visit */ }
    window.dispatchEvent(new Event("profile-strength-snoozed"));
  }

  return (
    <section aria-labelledby="profile-strength-heading" className="relative border border-border bg-card p-5 sm:p-6">
      <button type="button" onClick={snooze} aria-label="Hide for now" className="absolute right-3 top-3 grid size-9 place-items-center rounded-full text-muted-foreground transition-colors hover:bg-muted hover:text-foreground">
        <X size={14} />
      </button>

      <div className="flex items-center gap-4 pr-9">
        <Ring percent={percent} />
        <div className="min-w-0">
          <h2 id="profile-strength-heading" className="font-[family-name:var(--font-display)] text-[18px] font-semibold leading-snug sm:text-[19px]">{headline}</h2>
          <p className="mt-0.5 text-[13px] text-muted-foreground">{done.length} of {count} done. A fuller profile helps classmates and mentors find you.</p>
        </div>
      </div>

      {/* The one thing to do next, said plainly, with the way to do it. */}
      <div className="mt-5 flex flex-col gap-3 border border-primary/25 bg-primary/[0.05] p-4 sm:flex-row sm:items-center sm:gap-4">
        <div className="flex min-w-0 flex-1 items-start gap-3">
          <span className="grid size-10 shrink-0 place-items-center rounded-full bg-primary text-primary-foreground"><NextIcon size={16} /></span>
          <div className="min-w-0">
            <p className="text-[11.5px] font-semibold uppercase tracking-wide text-primary">Next up</p>
            <p className="text-[14.5px] font-semibold leading-snug">{next.action}</p>
            <p className="mt-0.5 text-[13px] leading-snug text-muted-foreground">{next.why}</p>
          </div>
        </div>
        <Button className="h-11 w-full shrink-0 rounded-full px-5 font-semibold sm:w-auto" onClick={() => router.push(`/profile#${next.target}`)}>
          Start now
          <span className="ml-2 rounded-full bg-white/20 px-2 py-0.5 text-[12px] font-bold">+{bonus(next)}%</span>
        </Button>
      </div>

      {rest.length > 0 && (
        <ul className="mt-3 grid gap-2 sm:grid-cols-2" aria-label="Other things to add">
          {rest.map((st) => {
            const Icon = STEP_ICON[st.id] ?? Sparkles;
            return (
              <li key={st.id}>
                <Link href={`/profile#${st.target}`} title={st.why}
                  className="group flex min-h-[52px] items-center gap-3 border border-border px-3.5 py-2 transition-colors hover:border-primary/50 hover:bg-muted/40">
                  <span className="grid size-8 shrink-0 place-items-center rounded-full bg-muted text-muted-foreground transition-colors group-hover:bg-primary/10 group-hover:text-primary"><Icon size={14} /></span>
                  <span className="min-w-0 flex-1 truncate text-[13.5px] font-medium">{st.action}</span>
                  <span className="shrink-0 rounded-full bg-accent/15 px-2 py-0.5 text-[12px] font-semibold tabular-nums">+{bonus(st)}%</span>
                  <ChevronRight size={13} className="shrink-0 text-muted-foreground/60" />
                </Link>
              </li>
            );
          })}
        </ul>
      )}

      {done.length > 0 && (
        <p className="mt-4 flex flex-wrap items-center gap-x-3 gap-y-1 text-[12.5px] text-muted-foreground" aria-label="Already done">
          {done.map((st) => (
            <span key={st.id} className="inline-flex items-center gap-1"><Check size={11} className="text-success" />{st.label}</span>
          ))}
        </p>
      )}
    </section>
  );
}

const STEP_ICON: Record<string, IconType> = {
  photo: Camera, bio: FileText, work: Briefcase, location: MapPin, skills: Sparkles, phone: Smartphone, linkedin: Linkedin, program: GraduationCap,
};

/** The percentage as a ring, so how far along it is can be read at a glance. */
function Ring({ percent }: { percent: number }) {
  const r = 26, c = 2 * Math.PI * r;
  return (
    <div className="relative size-16 shrink-0" role="img" aria-label={`Profile ${percent}% complete`}>
      <svg viewBox="0 0 64 64" className="size-16 -rotate-90">
        <circle cx="32" cy="32" r={r} fill="none" strokeWidth="6" style={{ stroke: "var(--muted)" }} />
        <circle cx="32" cy="32" r={r} fill="none" strokeWidth="6" strokeLinecap="round" strokeDasharray={c} strokeDashoffset={c * (1 - percent / 100)}
          className="transition-[stroke-dashoffset] duration-700 ease-out" style={{ stroke: "var(--accent)" }} />
      </svg>
      <span className="absolute inset-0 grid place-items-center text-[14px] font-bold tabular-nums">{percent}%</span>
    </div>
  );
}

/** A one-line version for the top of the profile page: how complete it is and the next thing worth adding. */
export function ProfileStrengthBar() {
  const strength = useProfileStrength();
  if (!strength.ready) return null;
  if (strength.open.length === 0) return <p className="mt-6 text-[13.5px] font-medium text-primary">Your profile is complete. Thank you, people will find it easier to know you.</p>;
  const next = strength.open[0];
  return (
    <div className="mt-6 flex flex-wrap items-center gap-x-5 gap-y-2 border border-border bg-card px-4 py-3">
      <div className="flex min-w-[200px] flex-1 items-center gap-3">
        <Progress value={strength.percent} tone="accent" aria-label={`Profile ${strength.percent}% complete`} className="h-2 max-w-[200px] flex-1" />
        <span className="text-[13px] font-semibold tabular-nums">{strength.percent}% complete</span>
      </div>
      <a href={`#${next.target}`} className="text-[13px] font-semibold text-primary underline underline-offset-4">{next.action} (+{strength.bonus(next)}%)</a>
    </div>
  );
}
