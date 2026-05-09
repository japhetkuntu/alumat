"use client";

import { useState } from "react";
import { useInfiniteQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Bell, CheckCheck, Loader2, Briefcase, Megaphone, Calendar,
  Star, CreditCard, MessageSquare, Check, ChevronRight,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import {
  getNotifications,
  getUnreadNotificationCount,
  markNotificationRead,
  markAllNotificationsRead,
} from "@/lib/member-api";
import type { NotificationItem } from "@/types";
import { formatDistanceToNow } from "date-fns";
import { toast } from "sonner";
import { handleApiError } from "@/lib/api-client";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";

const TYPE_META: Record<string, { icon: React.ReactNode; color: string; label: string }> = {
  JobAlert:             { icon: <Briefcase size={16} />, color: "bg-blue-500", label: "Job Alert" },
  CampaignAlert:        { icon: <Megaphone size={16} />, color: "bg-violet-500", label: "Campaign" },
  EventReminder:        { icon: <Calendar size={16} />, color: "bg-orange-500", label: "Event" },
  SpotlightUpdate:      { icon: <Star size={16} />, color: "bg-yellow-500", label: "Spotlight" },
  ContributionConfirmed:{ icon: <CreditCard size={16} />, color: "bg-emerald-500", label: "Confirmed" },
  ContributionRejected: { icon: <CreditCard size={16} />, color: "bg-red-500", label: "Rejected" },
  ClassNoteAlert:       { icon: <MessageSquare size={16} />, color: "bg-teal-500", label: "Class Note" },
};

function NotifIcon({ type }: { type: string }) {
  const meta = TYPE_META[type] ?? { icon: <Bell size={16} />, color: "bg-primary", label: "Notification" };
  return (
    <div className={cn("w-9 h-9 rounded-full flex items-center justify-center text-white shrink-0", meta.color)}>
      {meta.icon}
    </div>
  );
}

function NotifRow({
  notif,
  onMarkRead,
  isPending,
}: {
  notif: NotificationItem;
  onMarkRead: (id: string) => void;
  isPending: boolean;
}) {
  const meta = TYPE_META[notif.type] ?? { label: "Notification" };
  const content = (
    <div
      className={cn(
        "group flex items-start gap-4 px-4 sm:px-6 py-4 transition-colors border-b border-border/30 last:border-0",
        !notif.isRead ? "bg-primary/[0.03] hover:bg-primary/[0.06]" : "hover:bg-muted/40",
      )}
    >
      <NotifIcon type={notif.type} />

      <div className="flex-1 min-w-0">
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-[10px] font-black uppercase tracking-widest text-muted-foreground/60">
              {meta.label}
            </span>
            {!notif.isRead && (
              <span className="w-1.5 h-1.5 rounded-full bg-primary shrink-0" />
            )}
          </div>
          <span className="text-[11px] text-muted-foreground/50 shrink-0 leading-none mt-0.5">
            {formatDistanceToNow(new Date(notif.createdAt), { addSuffix: true })}
          </span>
        </div>
        <p className={cn("text-[14px] leading-snug mt-1", !notif.isRead && "font-semibold text-foreground")}>
          {notif.title}
        </p>
        <p className="text-[13px] text-muted-foreground leading-relaxed mt-0.5 line-clamp-2">
          {notif.body}
        </p>
      </div>

      {!notif.isRead && (
        <button
          onClick={(e) => { e.preventDefault(); onMarkRead(notif.id); }}
          disabled={isPending}
          className="opacity-0 group-hover:opacity-100 shrink-0 p-2 rounded-lg hover:bg-primary/10 text-primary transition-all"
          title="Mark as read"
        >
          <Check size={14} />
        </button>
      )}
      {notif.actionUrl && (
        <ChevronRight size={16} className="shrink-0 text-muted-foreground/30 mt-0.5 group-hover:text-muted-foreground transition-colors" />
      )}
    </div>
  );

  if (notif.actionUrl) {
    return <Link href={notif.actionUrl}>{content}</Link>;
  }
  return content;
}

const FILTER_TABS = ["All", "Unread", "Jobs", "Events", "Campaigns", "Contributions", "Class Notes"];
const TAB_TYPES: Record<string, string[]> = {
  Jobs: ["JobAlert"],
  Events: ["EventReminder"],
  Campaigns: ["CampaignAlert"],
  Contributions: ["ContributionConfirmed", "ContributionRejected"],
  "Class Notes": ["ClassNoteAlert"],
};

export default function NotificationsPage() {
  const [tab, setTab] = useState("All");
  const qc = useQueryClient();
  const pageSize = 25;

  const unreadQuery = useQuery({
    queryKey: ["notifications-unread-count"],
    queryFn: getUnreadNotificationCount,
  });

  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading } = useInfiniteQuery({
    queryKey: ["notifications-infinite", tab],
    queryFn: ({ pageParam = 1 }) => getNotifications(pageParam as number, pageSize),
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
      toast.success("All notifications marked as read");
      qc.invalidateQueries({ queryKey: ["notifications-unread-count"] });
      qc.invalidateQueries({ queryKey: ["notifications-infinite"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const allNotifications = data?.pages.flatMap((p) => p.results) ?? [];

  const filtered = allNotifications.filter((n) => {
    if (tab === "Unread") return !n.isRead;
    const types = TAB_TYPES[tab];
    if (types) return types.includes(n.type);
    return true;
  });

  const unreadCount = unreadQuery.data ?? 0;

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[860px] mx-auto space-y-6 selection:bg-primary/20">
      {/* Header */}
      <header className="flex items-start justify-between gap-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div className="space-y-1">
          <h1 className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tight">Notifications</h1>
          <p className="text-muted-foreground text-sm font-medium">
            {unreadCount > 0 ? `${unreadCount} unread notification${unreadCount > 1 ? "s" : ""}` : "You're all caught up!"}
          </p>
        </div>
        {unreadCount > 0 && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => markAll.mutate()}
            disabled={markAll.isPending}
            className="shrink-0 font-semibold"
          >
            {markAll.isPending ? (
              <Loader2 size={13} className="animate-spin mr-1.5" />
            ) : (
              <CheckCheck size={13} className="mr-1.5" />
            )}
            Mark all read
          </Button>
        )}
      </header>

      {/* Filter tabs */}
      <div className="flex gap-1.5 flex-wrap animate-in fade-in duration-700 delay-100">
        {FILTER_TABS.map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={cn(
              "px-3 py-1.5 rounded-full text-[11px] font-black uppercase tracking-wider transition-all",
              tab === t
                ? "bg-primary text-primary-foreground shadow-md shadow-primary/20"
                : "bg-muted/60 text-muted-foreground hover:bg-muted",
            )}
          >
            {t}
            {t === "Unread" && unreadCount > 0 && (
              <span className="ml-1.5 bg-primary-foreground/20 text-inherit rounded-full px-1.5 py-0.5 text-[10px]">
                {unreadCount}
              </span>
            )}
          </button>
        ))}
      </div>

      {/* List card */}
      <div className="rounded-2xl border border-border/40 bg-background shadow-sm overflow-hidden animate-in fade-in slide-in-from-bottom-4 duration-700 delay-150">
        {isLoading ? (
          <div className="divide-y divide-border/30">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="flex items-start gap-4 px-4 sm:px-6 py-4 animate-pulse">
                <div className="w-9 h-9 rounded-full bg-muted shrink-0" />
                <div className="flex-1 space-y-2">
                  <div className="h-3 bg-muted rounded w-1/4" />
                  <div className="h-4 bg-muted rounded w-3/4" />
                  <div className="h-3 bg-muted rounded w-1/2" />
                </div>
              </div>
            ))}
          </div>
        ) : filtered.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center px-4">
            <div className="w-16 h-16 rounded-full bg-muted/50 flex items-center justify-center mb-4">
              <Bell size={28} className="text-muted-foreground/40" />
            </div>
            <p className="font-bold text-foreground">
              {tab === "Unread" ? "No unread notifications" : `No ${tab.toLowerCase()} notifications`}
            </p>
            <p className="text-sm text-muted-foreground mt-1">
              {tab === "Unread" ? "You're all caught up!" : "Check back later."}
            </p>
          </div>
        ) : (
          <div>
            {filtered.map((n) => (
              <NotifRow
                key={n.id}
                notif={n}
                onMarkRead={(id) => markRead.mutate(id)}
                isPending={markRead.isPending}
              />
            ))}
          </div>
        )}
      </div>

      {/* Load more */}
      {hasNextPage && (
        <div className="flex justify-center pb-4">
          <Button
            variant="outline"
            onClick={() => fetchNextPage()}
            disabled={isFetchingNextPage}
            className="font-semibold"
          >
            {isFetchingNextPage ? (
              <Loader2 size={14} className="animate-spin mr-2" />
            ) : null}
            Load more
          </Button>
        </div>
      )}
    </div>
  );
}
