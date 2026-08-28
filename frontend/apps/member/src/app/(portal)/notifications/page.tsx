"use client";

import { useState } from "react";
import { useInfiniteQuery, useMutation, useQueryClient, useQuery } from "@tanstack/react-query";
import {
  Bell, CheckCheck, Loader2, Briefcase, Megaphone, Calendar,
  Star, CreditCard, MessageSquare, Check,
} from "lucide-react";
import { Button } from "@alumni/ui";
import { cn } from "@alumni/ui";
import {
  getNotifications, getUnreadNotificationCount,
  markNotificationRead, markAllNotificationsRead,
} from "@/lib/member-api";
import type { NotificationItem } from "@/types";
import { formatDistanceToNow } from "date-fns";
import { toast } from "sonner";
import { handleApiError } from "@/lib/api-client";
import Link from "next/link";
import { PageHeader } from "@alumni/ui";

/* Extract path from a full URL — falls back to the string as-is if already a path */
function toPath(url: string | null | undefined): string | null {
  if (!url) return null;
  try {
    return new URL(url).pathname;
  } catch {
    // Already a relative path
    return url.startsWith("/") ? url : `/${url}`;
  }
}

/* Safe relative time */
function relativeTime(dateStr: string | null | undefined): string {
  if (!dateStr) return "";
  const d = new Date(dateStr);
  if (isNaN(d.getTime())) return "";
  return formatDistanceToNow(d, { addSuffix: true });
}

/* ─────────────────────────────────────────────────────────────────────────
   NOTIFICATION TYPE META — inline styles, no hardcoded Tailwind color classes
   ───────────────────────────────────────────────────────────────────────── */
const TYPE_META: Record<string, {
  icon: React.ElementType;
  bg: string;
  color: string;
  label: string;
}> = {
  JobAlert:              { icon: Briefcase,     bg: "rgba(59,130,246,0.12)",  color: "#2563eb", label: "Job"          },
  CampaignAlert:         { icon: Megaphone,     bg: "rgba(139,92,246,0.12)",  color: "#7c3aed", label: "Fundraiser"   },
  EventReminder:         { icon: Calendar,      bg: "rgba(245,158,11,0.12)",  color: "#d97706", label: "Event"        },
  SpotlightUpdate:       { icon: Star,          bg: "rgba(234,179,8,0.12)",   color: "#ca8a04", label: "Spotlight"    },
  ContributionConfirmed: { icon: CreditCard,    bg: "rgba(16,185,129,0.12)",  color: "#059669", label: "Confirmed"    },
  ContributionRejected:  { icon: CreditCard,    bg: "rgba(239,68,68,0.12)",   color: "#dc2626", label: "Rejected"     },
  ClassNoteAlert:        { icon: MessageSquare, bg: "rgba(20,184,166,0.12)",  color: "#0d9488", label: "Class note"   },
};
const DEFAULT_META = { icon: Bell, bg: "var(--color-background-info)", color: "var(--primary)", label: "Notification" };

function TypeIcon({ type }: { type: string }) {
  const m = TYPE_META[type] ?? DEFAULT_META;
  const Icon = m.icon;
  return (
    <div
      className="w-9 h-9 rounded-full flex items-center justify-center shrink-0"
      style={{ background: m.bg, border: `1.5px solid ${m.bg.replace("0.12", "0.25")}` }}
    >
      <Icon size={16} style={{ color: m.color }} />
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   NOTIFICATION ROW
   ───────────────────────────────────────────────────────────────────────── */
function NotifRow({
  notif, onMarkRead, isPending,
}: {
  notif: NotificationItem;
  onMarkRead: (id: string) => void;
  isPending: boolean;
}) {
  const meta = TYPE_META[notif.type] ?? DEFAULT_META;
  const path = toPath(notif.actionUrl);
  const time = relativeTime(notif.createdAt);
  const [expanded, setExpanded] = useState(false);

  // A notification with a real destination navigates there on click; one
  // without a destination expands its full body in place instead of being
  // permanently clipped to two lines.
  const toggle = () => {
    if (path) return;
    setExpanded((v) => !v);
    if (!notif.isRead) onMarkRead(notif.id);
  };

  const inner = (
    <div
      role={path ? undefined : "button"}
      tabIndex={path ? undefined : 0}
      onClick={toggle}
      onKeyDown={(e) => { if (!path && (e.key === "Enter" || e.key === " ")) { e.preventDefault(); toggle(); } }}
      className={cn("flex items-start gap-3 sm:gap-4 px-4 sm:px-5 py-4 border-b transition-colors", !path && "cursor-pointer")}
      style={{
        borderColor: "var(--border)",
        background: notif.isRead ? "var(--background)" : "var(--color-background-info)",
      }}
    >
      <TypeIcon type={notif.type} />

      <div className="flex-1 min-w-0">
        {/* Label row */}
        <div className="flex items-center justify-between gap-2 mb-0.5">
          <div className="flex items-center gap-1.5">
            <span className="text-[11px] font-semibold uppercase tracking-wide" style={{ color: "var(--muted-foreground)", opacity: 0.6 }}>
              {meta.label}
            </span>
            {!notif.isRead && (
              <span className="w-1.5 h-1.5 rounded-full shrink-0" style={{ background: "var(--primary)" }} />
            )}
          </div>
          {time && (
            <span className="text-[11px] shrink-0" style={{ color: "var(--muted-foreground)", opacity: 0.5 }}>
              {time}
            </span>
          )}
        </div>

        {/* Title */}
        <p
          className="text-[14px] leading-snug"
          style={{ color: "var(--foreground)", fontWeight: notif.isRead ? 400 : 600 }}
        >
          {notif.title}
        </p>

        {/* Body */}
        {notif.body && (
          <p className={cn("text-[13px] leading-relaxed mt-0.5", !expanded && "line-clamp-2")} style={{ color: "var(--muted-foreground)" }}>
            {notif.body}
          </p>
        )}

        {/* Mark read — always visible, not hover-only */}
        {!notif.isRead && (
          <button
            onClick={e => { e.preventDefault(); e.stopPropagation(); onMarkRead(notif.id); }}
            disabled={isPending}
            className="flex items-center gap-1 mt-2 text-[12px] font-semibold transition-opacity hover:opacity-70"
            style={{ color: "var(--primary)" }}
          >
            {isPending
              ? <Loader2 size={11} className="animate-spin" />
              : <Check size={11} />}
            Mark as read
          </button>
        )}
      </div>
    </div>
  );

  if (path) return <Link href={path}>{inner}</Link>;
  return inner;
}

/* ─────────────────────────────────────────────────────────────────────────
   FILTER TABS
   ───────────────────────────────────────────────────────────────────────── */
const FILTER_TABS = ["All", "Unread", "Jobs", "Events", "Fundraisers", "Contributions"] as const;
const TAB_TYPES: Record<string, string[]> = {
  Jobs:          ["JobAlert"],
  Events:        ["EventReminder"],
  Fundraisers:   ["CampaignAlert"],
  Contributions: ["ContributionConfirmed", "ContributionRejected"],
};

/* ─────────────────────────────────────────────────────────────────────────
   PAGE
   ───────────────────────────────────────────────────────────────────────── */
export default function NotificationsPage() {
  const [tab, setTab] = useState("All");
  const qc       = useQueryClient();
  const pageSize = 25;

  const unreadQuery = useQuery({
    queryKey: ["notifications-unread-count"],
    queryFn:  getUnreadNotificationCount,
  });

  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading } = useInfiniteQuery({
    queryKey:       ["notifications-infinite", tab],
    queryFn:        ({ pageParam = 1 }) => getNotifications(pageParam as number, pageSize),
    getNextPageParam: (last, pages) =>
      last.totalPages > pages.length ? pages.length + 1 : undefined,
    initialPageParam: 1,
  });

  const markRead = useMutation({
    mutationFn: markNotificationRead,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["notifications-unread-count"] });
      qc.invalidateQueries({ queryKey: ["notifications-infinite"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const markAll = useMutation({
    mutationFn: markAllNotificationsRead,
    onSuccess: () => {
      toast.success("All notifications marked as read.");
      qc.invalidateQueries({ queryKey: ["notifications-unread-count"] });
      qc.invalidateQueries({ queryKey: ["notifications-infinite"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const allNotifications = data?.pages.flatMap(p => p.results) ?? [];
  const filtered = allNotifications.filter(n => {
    if (tab === "Unread") return !n.isRead;
    const types = TAB_TYPES[tab];
    if (types) return types.includes(n.type);
    return true;
  });

  const unreadCount = unreadQuery.data ?? 0;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[860px] mx-auto space-y-6">

      {/* ── Header ── */}
      <PageHeader
        eyebrow="Stay in the loop"
        title="Notifications"
        description={unreadCount > 0
          ? `${unreadCount} unread notification${unreadCount !== 1 ? "s" : ""}`
          : "You're all caught up."}
      >
        {unreadCount > 0 && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => markAll.mutate()}
            disabled={markAll.isPending}
            className="shrink-0 gap-1.5 font-semibold text-[13px]"
            style={{ height: 38 }}
          >
            {markAll.isPending
              ? <Loader2 size={13} className="animate-spin" />
              : <CheckCheck size={13} />}
            Mark all read
          </Button>
        )}
      </PageHeader>

      {/* ── Filter tabs ── */}
      <div className="flex flex-wrap gap-2">
        {FILTER_TABS.map(t => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={cn(
              "px-3.5 py-1.5 text-[12.5px] font-semibold border transition-colors",
              tab === t ? "text-white border-transparent" : "border-border hover:border-primary/40",
            )}
            style={tab === t
              ? { background: "var(--primary)", color: "white" }
              : { background: "var(--background)", color: "var(--muted-foreground)" }}
          >
            {t}
            {t === "Unread" && unreadCount > 0 && (
              <span
                className="ml-1.5 px-1.5 py-0.5 text-[10px] font-bold"
                style={{
                  background: tab === "Unread" ? "rgba(255,255,255,0.2)" : "var(--color-background-info)",
                  color:      tab === "Unread" ? "white" : "var(--primary)",
                }}
              >
                {unreadCount}
              </span>
            )}
          </button>
        ))}
      </div>

      {/* ── Notification list ── */}
      <div className="card overflow-hidden">
        {isLoading ? (
          <div>
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="flex items-start gap-4 px-4 sm:px-5 py-4 border-b animate-pulse"
                style={{ borderColor: "var(--border)" }}>
                <div className="w-9 h-9 rounded-full shrink-0" style={{ background: "var(--secondary)" }} />
                <div className="flex-1 space-y-2">
                  <div className="h-3 rounded w-1/4" style={{ background: "var(--secondary)" }} />
                  <div className="h-4 rounded w-3/4" style={{ background: "var(--secondary)" }} />
                  <div className="h-3 rounded w-1/2" style={{ background: "var(--secondary)" }} />
                </div>
              </div>
            ))}
          </div>
        ) : filtered.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-14 text-center px-4 gap-3">
            <div className="w-12 h-12 rounded-full flex items-center justify-center"
              style={{ background: "var(--secondary)" }}>
              <Bell size={22} style={{ color: "var(--muted-foreground)", opacity: 0.35 }} />
            </div>
            <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
              {tab === "All"
                ? "No notifications yet"
                : tab === "Unread"
                  ? "No unread notifications"
                  : `No ${tab.toLowerCase()} notifications`}
            </p>
            <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
              {tab === "Unread" ? "You're all caught up." : "Check back later."}
            </p>
          </div>
        ) : (
          filtered.map(n => (
            <NotifRow
              key={n.id}
              notif={n}
              onMarkRead={id => markRead.mutate(id)}
              isPending={markRead.isPending && markRead.variables === n.id}
            />
          ))
        )}
      </div>

      {/* ── Load more ── */}
      {hasNextPage && (
        <div className="flex justify-center">
          <Button
            variant="outline"
            onClick={() => fetchNextPage()}
            disabled={isFetchingNextPage}
            className="gap-2 font-semibold text-[13.5px]"
            style={{ height: 42 }}
          >
            {isFetchingNextPage && <Loader2 size={14} className="animate-spin" />}
            Load more
          </Button>
        </div>
      )}
    </div>
  );
}
