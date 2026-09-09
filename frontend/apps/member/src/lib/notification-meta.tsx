import {
  Bell, Briefcase, Megaphone, Calendar, Star, CreditCard, MessageSquare,
  Radio, GraduationCap, Package,
} from "@alumni/ui";

/**
 * One place mapping every Notification.Type the backend actually dispatches
 * (see NotificationDispatcher.cs in both Member.Api and Institution.Api) to
 * an icon/color/label — shared by the bell dropdown (NotificationPanel) and
 * the full /notifications page, so the two never drift out of sync the way
 * they had before this file existed (the dropdown was falling back to a
 * generic bell for half of these).
 */
export const TYPE_META: Record<string, {
  icon: React.ElementType;
  bg: string;
  color: string;
  label: string;
}> = {
  JobAlert:                  { icon: Briefcase,     bg: "rgba(59,130,246,0.12)",  color: "#2563eb", label: "Job"        },
  CampaignAlert:              { icon: Megaphone,     bg: "rgba(139,92,246,0.12)",  color: "#7c3aed", label: "Fundraiser" },
  EventReminder:               { icon: Calendar,      bg: "rgba(245,158,11,0.12)",  color: "#d97706", label: "Event"      },
  SpotlightUpdate:             { icon: Star,          bg: "rgba(234,179,8,0.12)",   color: "#ca8a04", label: "Spotlight"  },
  ContributionConfirmed:       { icon: CreditCard,    bg: "rgba(16,185,129,0.12)",  color: "#059669", label: "Confirmed"  },
  ContributionRejected:        { icon: CreditCard,    bg: "rgba(239,68,68,0.12)",   color: "#dc2626", label: "Rejected"   },
  ClassNoteAlert:               { icon: MessageSquare, bg: "rgba(20,184,166,0.12)",  color: "#0d9488", label: "Class note" },
  ForumReply:                   { icon: MessageSquare, bg: "rgba(20,184,166,0.12)",  color: "#0d9488", label: "Forum"      },
  Broadcast:                    { icon: Radio,         bg: "rgba(220,38,38,0.12)",   color: "#dc2626", label: "Announcement" },
  MentorshipRequestReceived:    { icon: GraduationCap, bg: "rgba(99,102,241,0.12)",  color: "#4f46e5", label: "Mentorship" },
  MentorshipRequestDecision:    { icon: GraduationCap, bg: "rgba(99,102,241,0.12)",  color: "#4f46e5", label: "Mentorship" },
  StoreDeliveryStatusUpdated:   { icon: Package,       bg: "rgba(217,119,6,0.12)",   color: "#b45309", label: "Order"      },
};
export const DEFAULT_TYPE_META = { icon: Bell, bg: "var(--brand-primary-100, var(--color-background-info))", color: "var(--primary)", label: "Notification" };

export function getTypeMeta(type: string) {
  return TYPE_META[type] ?? DEFAULT_TYPE_META;
}

export function TypeIcon({ type, size = 16, boxPx = 36 }: { type: string; size?: number; boxPx?: number }) {
  const m = getTypeMeta(type);
  const Icon = m.icon;
  return (
    <div
      className="rounded-full flex items-center justify-center shrink-0"
      style={{ width: boxPx, height: boxPx, background: m.bg, border: `1.5px solid ${m.bg.replace("0.12", "0.25")}` }}
    >
      <Icon size={size} style={{ color: m.color }} />
    </div>
  );
}

/** Extracts the path from a full notification actionUrl — falls back to the string as-is if already a path. */
export function toNotificationPath(url: string | null | undefined): string | null {
  if (!url) return null;
  try {
    return new URL(url).pathname;
  } catch {
    return url.startsWith("/") ? url : `/${url}`;
  }
}
