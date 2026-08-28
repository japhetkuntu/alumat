"use client";

import { useState } from "react";
import { useInfiniteQuery, useMutation, useQueryClient, useQuery } from "@tanstack/react-query";
import {
  Bell, CheckCheck, Loader2, CreditCard, Check, ChevronRight, LifeBuoy,
} from "lucide-react";
import { Button } from "@alumni/ui";
import { cn } from "@alumni/ui";
import {
  getNotifications,
  getUnreadNotificationCount,
  markNotificationRead,
  markAllNotificationsRead,
} from "@/lib/institution-api";
import type { NotificationItem } from "@/types";
import { formatDistanceToNow } from "date-fns";
import { toast } from "sonner";
import { handleApiError } from "@/lib/api-client";
import Link from "next/link";

const TYPE_META: Record<string, { color: string; label: string; icon: React.ElementType }> = {
  PaymentReceived:       { color: "bg-success",  label: "Payment",   icon: CreditCard },
  ContributionConfirmed: { color: "bg-blue-500", label: "Confirmed", icon: CreditCard },
  ContributionRejected:  { color: "bg-red-500",  label: "Rejected",  icon: CreditCard },
  SupportTicketResolved: { color: "bg-primary",  label: "Support",   icon: LifeBuoy },
};

function NotifIcon({ type }: { type: string }) {
  const meta = TYPE_META[type] ?? { color: "bg-primary", label: "Notification", icon: CreditCard };
  const Icon = meta.icon;
  return (
    <div className={cn("w-9 h-9 rounded-full flex items-center justify-center text-white shrink-0", meta.color)}>
      <Icon size={16} />
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
  const [expanded, setExpanded] = useState(false);
  const meta = TYPE_META[notif.type] ?? { label: "Notification" };

  // A notification with a real destination navigates there on click; one
  // without a destination expands its full body in place instead of being
  // permanently clipped to two lines.
  const toggle = () => {
    if (notif.actionUrl) return;
    setExpanded((v) => !v);
    if (!notif.isRead) onMarkRead(notif.id);
  };

  const content = (
    <div
      role={notif.actionUrl ? undefined : "button"}
      tabIndex={notif.actionUrl ? undefined : 0}
      onClick={toggle}
      onKeyDown={(e) => { if (!notif.actionUrl && (e.key === "Enter" || e.key === " ")) { e.preventDefault(); toggle(); } }}
      className={cn(
        "group flex items-start gap-4 px-4 sm:px-6 py-4 transition-colors border-b border-border/30 last:border-0",
        !notif.actionUrl && "cursor-pointer",
        !notif.isRead ? "bg-primary/[0.03] hover:bg-primary/[0.06]" : "hover:bg-muted/40",
      )}
    >
      <NotifIcon type={notif.type} />

      <div className="flex-1 min-w-0">
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-center gap-2">
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
        <p className={cn("text-[13px] text-muted-foreground leading-relaxed mt-0.5", !expanded && "line-clamp-2")}>
          {notif.body}
        </p>
      </div>

      {!notif.isRead && (
        <button
          onClick={(e) => { e.preventDefault(); e.stopPropagation(); onMarkRead(notif.id); }}
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

const FILTER_TABS = ["All", "Unread", "Payments"];

export default function AdminNotificationsPage() {
  const [tab, setTab] = useState("All");
  const qc = useQueryClient();
  const pageSize = 25;

  const unreadQuery = useQuery({
    queryKey: ["admin-notifications-unread-count"],
    queryFn: getUnreadNotificationCount,
  });

  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading } = useInfiniteQuery({
    queryKey: ["admin-notifications-infinite", tab],
    queryFn: ({ pageParam = 1 }) => getNotifications(pageParam as number, pageSize),
    getNextPageParam: (last, pages) =>
      last.totalPages > pages.length ? pages.length + 1 : undefined,
    initialPageParam: 1,
  });

  const markRead = useMutation({
    mutationFn: markNotificationRead,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-notifications-unread-count"] });
      qc.invalidateQueries({ queryKey: ["admin-notifications-infinite"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const markAll = useMutation({
    mutationFn: markAllNotificationsRead,
    onSuccess: () => {
      toast.success("All notifications marked as read");
      qc.invalidateQueries({ queryKey: ["admin-notifications-unread-count"] });
      qc.invalidateQueries({ queryKey: ["admin-notifications-infinite"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const allNotifications = data?.pages.flatMap((p) => p.results) ?? [];

  const filtered = allNotifications.filter((n) => {
    if (tab === "Unread") return !n.isRead;
    if (tab === "Payments") return ["PaymentReceived", "ContributionConfirmed", "ContributionRejected"].includes(n.type);
    return true;
  });

  const unreadCount = unreadQuery.data ?? 0;

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      {/* Header */}
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Notifications</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">
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
      <div className="flex gap-2 flex-wrap">
        {FILTER_TABS.map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={cn(
              "px-4 py-1.5 rounded-full text-[11px] font-black uppercase tracking-widest transition-all",
              tab === t
                ? "bg-primary text-primary-foreground shadow-md shadow-primary/20"
                : "bg-muted/50 text-muted-foreground hover:bg-muted",
            )}
          >
            {t}
            {t === "Unread" && unreadCount > 0 && (
              <span className="ml-1.5 bg-white/20 rounded-full px-1.5 py-0.5 text-[10px]">
                {unreadCount}
              </span>
            )}
          </button>
        ))}
      </div>

      {/* List */}
      <div className="rounded-2xl border border-border/40 bg-background shadow-sm overflow-hidden">
        {isLoading ? (
          <div className="divide-y divide-border/30">
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="flex items-start gap-4 px-6 py-4 animate-pulse">
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
          <div className="flex flex-col items-center justify-center py-16 text-center">
            <div className="w-16 h-16 rounded-full bg-muted/50 flex items-center justify-center mb-4">
              <Bell size={28} className="text-muted-foreground/40" />
            </div>
            <p className="font-bold">
              {tab === "Unread" ? "No unread notifications" : "No notifications yet"}
            </p>
            <p className="text-sm text-muted-foreground mt-1">
              You&apos;ll be notified when members make payments.
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

      {hasNextPage && (
        <div className="flex justify-center">
          <Button
            variant="outline"
            onClick={() => fetchNextPage()}
            disabled={isFetchingNextPage}
            className="font-semibold"
          >
            {isFetchingNextPage && <Loader2 size={14} className="animate-spin mr-2" />}
            Load more
          </Button>
        </div>
      )}
    </div>
  );
}
