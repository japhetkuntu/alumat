"use client";

import { Badge } from "@alumni/ui";
import type { PollState } from "@/lib/institution-api";

/** "12 Nov 2026, 9:00 am", in the viewer's own time zone. */
export function fmtWhen(iso: string): string {
  const d = new Date(iso);
  return isNaN(d.getTime()) ? "" : d.toLocaleString("en-GB", { day: "numeric", month: "short", year: "numeric", hour: "numeric", minute: "2-digit", hour12: true });
}

/** An ISO time as the value a datetime-local input wants, in the viewer's time zone. */
export function toLocalInput(iso: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return "";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function audienceText(p: { yearGroups?: number[] | null; communityName?: string | null; communityId?: string | null }, cohort = "Class"): string {
  if (p.communityId) return p.communityName ? `Members of ${p.communityName}` : "Members of a group";
  if (p.yearGroups && p.yearGroups.length > 0) {
    const years = [...p.yearGroups].sort((a, b) => a - b);
    return years.length <= 3 ? years.map((y) => `${cohort} of ${y}`).join(", ") : `${years.length} year groups (${years[0]} to ${years[years.length - 1]})`;
  }
  return "Every active member";
}

const VARIANT: Record<PollState, "neutral" | "info" | "success" | "secondary"> = { Draft: "neutral", Scheduled: "info", Open: "success", Closed: "secondary" };
const LABEL: Record<PollState, string> = { Draft: "Draft", Scheduled: "Scheduled", Open: "Voting open", Closed: "Closed" };

export function PollStateBadge({ state }: { state: PollState }) {
  return <Badge variant={VARIANT[state]}>{LABEL[state]}</Badge>;
}

export const RESULTS_MODE_LABEL: Record<string, { title: string; hint: string }> = {
  Live: { title: "Live", hint: "Members see the figures as votes come in." },
  AfterClose: { title: "After voting closes", hint: "Figures appear on their own once voting ends. The safest choice: nobody is swayed by a running count." },
  Manual: { title: "When I publish them", hint: "Members see nothing until you press Publish results, after voting has closed." },
};
