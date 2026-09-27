import { toCsv } from "./csv";
import type { ActivationScorecardItem } from "./platform-api";

const DAY_MS = 86_400_000;

/** Whole days from now until <paramref name="iso"/> (negative once past). */
export function daysUntil(iso: string, now = new Date()): number {
  return Math.ceil((new Date(iso).getTime() - now.getTime()) / DAY_MS);
}

/**
 * Trial wording for an institution, or null when there's nothing worth showing.
 * Every institution gets a default trial end date at creation and nothing enforces
 * it, so trials that ended more than a week ago are hidden rather than shouting
 * "trial ended" on every long-standing customer.
 */
export function trialLabel(trialEndsAt: string | null | undefined, isActivated: boolean, now = new Date()): string | null {
  if (!trialEndsAt || isActivated) return null;
  const days = daysUntil(trialEndsAt, now);
  if (days < -7) return null;
  if (days < 0) return `Trial ended ${Math.abs(days)} day${days === -1 ? "" : "s"} ago`;
  if (days === 0) return "Trial ends today";
  return `Trial ends in ${days} day${days === 1 ? "" : "s"}`;
}

export function activationStatus(item: ActivationScorecardItem): "Activated" | "Overdue" | "Stalled" | "In progress" {
  if (item.isActivated) return "Activated";
  if (item.isOverdue) return "Overdue";
  if (item.isStalled) return "Stalled";
  return "In progress";
}

/** Board-report export: one row per institution, one column per criterion. */
export function scorecardToCsv(items: ActivationScorecardItem[]): string {
  const keys = ["branding", "payouts", "members", "payments", "staff"] as const;
  const labels = ["Branding", "Payouts", "Members", "Payments", "Staff weekly"];
  return toCsv([
    ["Institution", "Status", "Days live", "Criteria met", ...labels.flatMap((l) => [l, `${l} detail`]), "Next step", "Activated on"],
    ...items.map((i) => [
      i.name,
      activationStatus(i),
      i.daysLive,
      `${i.metCount}/5`,
      ...keys.flatMap((k) => {
        const c = i.criteria.find((x) => x.key === k);
        return [c ? (c.met ? "Yes" : "No") : "", c?.detail ?? ""];
      }),
      i.nextStep ?? "",
      i.activatedAt ? i.activatedAt.slice(0, 10) : "",
    ]),
  ]);
}
