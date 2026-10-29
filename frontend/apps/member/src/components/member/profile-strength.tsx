"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, useSyncExternalStore } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, Progress, X } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { useFeatures } from "@/components/member/member-layout";
import { memberClient } from "@/lib/api-client";
import { getMyProfile, type MemberProfileResponse } from "@/lib/member-api";

export interface ProfileStep {
  id: string;
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
    { id: "photo", target: "profile-photo", weight: 20, action: "Add a photo", why: inDirectory ? "So people you studied with recognise you in the directory." : "So people recognise you.", done: !!p.profilePictureUrl },
    { id: "bio", target: "bio", weight: 15, action: "Write a line about you", why: "It is the first thing people read when they open your profile.", done: !!p.bio?.trim() },
    { id: "work", target: p.company?.trim() ? "jobTitle" : "company", weight: 15, action: "Say where you work", why: "Helps members and the institution connect you with the right people and opportunities.", done: !!(p.company?.trim() && p.jobTitle?.trim()) },
    { id: "location", target: "location", weight: 10, action: "Add your city", why: features.enabled("AlumniMap") ? "Members near you can find you, and you them." : "So people nearby can find you.", done: !!p.location?.trim() },
    { id: "skills", target: "skills", weight: 15, action: "Add skills and interests", why: "Get matched with people, jobs and mentors that fit.", done: (p.skills?.length ?? 0) + (p.interests?.length ?? 0) > 0 },
    { id: "phone", target: "phone", weight: 10, action: "Add your phone number", why: "So you get reminders and updates that matter. You choose whether anyone else sees it.", done: !!p.phone?.trim() },
    { id: "linkedin", target: "linkedInUrl", weight: 5, action: "Link your LinkedIn", why: "One tap for others to connect with you.", done: !!p.linkedInUrl?.trim() },
    ...(theme.data?.programOfStudyEnabled
      ? [{ id: "program", target: "program", weight: 10, action: "Choose your program", why: "Find people who studied what you did.", done: !!p.program?.trim() } satisfies ProfileStep]
      : []),
  ];
  const total = steps.reduce((n, s) => n + s.weight, 0);
  const got = steps.filter((s) => s.done).reduce((n, s) => n + s.weight, 0);
  return {
    ready: true as const,
    firstName: p.firstName,
    percent: Math.round((got / total) * 100),
    open: steps.filter((s) => !s.done).sort((a, b) => b.weight - a.weight),
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
  const { firstName, percent, open, bonus } = strength;
  const [next, ...rest] = open;
  const headline = percent < 40 ? `${firstName}, let your community find you` : percent < 80 ? `${firstName}, you are ${percent}% of the way there` : `Almost done, ${firstName}`;

  function snooze() {
    setAway(true);
    if (!user?.id) return;
    try { localStorage.setItem(snoozeKey(user.id), String(Date.now() + SNOOZE_DAYS * 86_400_000)); } catch { /* it just comes back next visit */ }
    window.dispatchEvent(new Event("profile-strength-snoozed"));
  }

  return (
    <section aria-labelledby="profile-strength-heading" className="relative border border-border bg-card p-5 sm:p-6">
      <button type="button" onClick={snooze} aria-label="Hide for now" className="absolute right-3 top-3 flex size-8 items-center justify-center text-muted-foreground hover:bg-muted hover:text-foreground">
        <X size={14} />
      </button>
      <div className="flex flex-wrap items-center gap-x-6 gap-y-4 pr-8">
        <div className="min-w-0 flex-1 basis-64">
          <h2 id="profile-strength-heading" className="font-[family-name:var(--font-display)] text-[19px] font-semibold leading-snug">{headline}</h2>
          <p className="mt-1 text-[13.5px] text-muted-foreground">
            A fuller profile means classmates, mentors and your institution can actually find and reach you. It takes about a minute.
          </p>
          <div className="mt-3 flex items-center gap-3">
            <Progress value={percent} tone="accent" aria-label={`Profile ${percent}% complete`} className="h-2 max-w-[260px] flex-1" />
            <span className="text-[13px] font-semibold tabular-nums">{percent}%</span>
          </div>
        </div>
        <div className="w-full shrink-0 sm:w-auto sm:max-w-[280px]">
          <p className="text-[12.5px] text-muted-foreground">{next.why}</p>
          <Button className="mt-2 w-full sm:w-auto" onClick={() => router.push(`/profile#${next.target}`)}>
            {next.action} <span className="ml-1.5 opacity-80">+{bonus(next)}%</span>
          </Button>
        </div>
      </div>
      {rest.length > 0 && (
        <ul className="mt-4 flex flex-wrap gap-2 border-t border-border pt-4" aria-label="Other things to add">
          {rest.slice(0, 4).map((s) => (
            <li key={s.id}>
              <Link href={`/profile#${s.target}`} title={s.why} className="inline-flex items-center gap-1.5 border border-border px-3 py-1.5 text-[12.5px] font-medium hover:border-primary hover:text-primary">
                {s.action} <span className="text-muted-foreground">+{bonus(s)}%</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
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
