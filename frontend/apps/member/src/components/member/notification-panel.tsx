"use client";

import { useState, useRef, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Bell, Check, CheckCheck, Loader2, X } from "lucide-react";
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

function useNotifications() {
  const qc = useQueryClient();

  const unreadQuery = useQuery({
    queryKey: ["notifications-unread-count"],
    queryFn: getUnreadNotificationCount,
    refetchInterval: 30_000,
    staleTime: 20_000,
  });

  const listQuery = useQuery({
    queryKey: ["notifications-list"],
    queryFn: () => getNotifications(1, 20),
    staleTime: 20_000,
  });

  const markRead = useMutation({
    mutationFn: markNotificationRead,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["notifications-unread-count"] });
      qc.invalidateQueries({ queryKey: ["notifications-list"] });
    },
  });

  const markAll = useMutation({
    mutationFn: markAllNotificationsRead,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["notifications-unread-count"] });
      qc.invalidateQueries({ queryKey: ["notifications-list"] });
    },
  });

  return { unreadQuery, listQuery, markRead, markAll };
}

function NotificationRow({
  notif,
  onMarkRead,
}: {
  notif: NotificationItem;
  onMarkRead: (id: string) => void;
}) {
  return (
    <div
      className={cn(
        "flex gap-3 px-4 py-3 hover:bg-muted/50 transition-colors cursor-default group",
        !notif.isRead && "bg-primary/5"
      )}
    >
      <div className="mt-1 shrink-0">
        <div
          className={cn(
            "w-2 h-2 rounded-full mt-1",
            notif.isRead ? "bg-transparent" : "bg-primary"
          )}
        />
      </div>
      <div className="flex-1 min-w-0">
        <p className={cn("text-[13px] leading-snug", !notif.isRead && "font-semibold")}>
          {notif.title}
        </p>
        <p className="text-[12px] text-muted-foreground leading-snug mt-0.5 line-clamp-2">
          {notif.body}
        </p>
        <p className="text-[10px] text-muted-foreground/60 mt-1">
          {formatDistanceToNow(new Date(notif.createdAt), { addSuffix: true })}
        </p>
      </div>
      {!notif.isRead && (
        <button
          onClick={() => onMarkRead(notif.id)}
          className="opacity-0 group-hover:opacity-100 shrink-0 mt-1 p-1 rounded hover:bg-primary/10 text-primary transition-all"
          aria-label="Mark as read"
        >
          <Check size={12} />
        </button>
      )}
    </div>
  );
}

export function NotificationPanel() {
  const [open, setOpen] = useState(false);
  const panelRef = useRef<HTMLDivElement>(null);
  const { unreadQuery, listQuery, markRead, markAll } = useNotifications();

  const unreadCount = unreadQuery.data ?? 0;
  const notifications = listQuery.data?.results ?? [];

  // Close on outside click
  useEffect(() => {
    if (!open) return;
    function handle(e: MouseEvent) {
      if (panelRef.current && !panelRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handle);
    return () => document.removeEventListener("mousedown", handle);
  }, [open]);

  return (
    <div className="relative" ref={panelRef}>
      <Button
        variant="ghost"
        size="sm"
        className="relative h-10 w-10 p-0 rounded-xl hover:bg-black/5 dark:hover:bg-white/5 transition-all active:scale-95"
        onClick={() => setOpen((v) => !v)}
        aria-label="Notifications"
      >
        <Bell size={20} className="text-muted-foreground" />
        {unreadCount > 0 && (
          <span className="absolute top-1.5 right-1.5 min-w-[16px] h-[16px] rounded-full bg-primary text-primary-foreground text-[9px] font-bold flex items-center justify-center px-0.5 leading-none">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </Button>

      {open && (
        <>
          {/* Mobile backdrop */}
          <div
            className="fixed inset-0 z-40 bg-black/20 backdrop-blur-sm sm:hidden"
            onClick={() => setOpen(false)}
          />
          <div className="fixed inset-x-3 top-[4.5rem] z-50 rounded-2xl border border-border/50 bg-background shadow-2xl shadow-black/10 dark:shadow-black/40 overflow-hidden sm:absolute sm:inset-x-auto sm:right-0 sm:top-full sm:mt-2 sm:w-96">
          {/* Header */}
          <div className="flex items-center justify-between px-4 py-3 border-b border-border/40 bg-muted/30">
            <div className="flex items-center gap-2">
              <Bell size={14} className="text-muted-foreground" />
              <span className="font-semibold text-[13px]">Notifications</span>
              {unreadCount > 0 && (
                <span className="text-[10px] font-bold bg-primary text-primary-foreground rounded-full px-1.5 py-0.5 leading-none">
                  {unreadCount}
                </span>
              )}
            </div>
            <div className="flex items-center gap-1">
              {unreadCount > 0 && (
                <button
                  onClick={() => markAll.mutate()}
                  disabled={markAll.isPending}
                  className="flex items-center gap-1 text-[11px] font-semibold text-primary hover:text-primary/80 transition-colors px-2 py-1 rounded-lg hover:bg-primary/10 disabled:opacity-50"
                >
                  {markAll.isPending ? (
                    <Loader2 size={11} className="animate-spin" />
                  ) : (
                    <CheckCheck size={11} />
                  )}
                  Mark all read
                </button>
              )}
              <button
                onClick={() => setOpen(false)}
                className="p-1 rounded-lg hover:bg-muted transition-colors text-muted-foreground"
                aria-label="Close"
              >
                <X size={13} />
              </button>
            </div>
          </div>

          {/* List */}
          <div className="max-h-[60vh] sm:max-h-[380px] overflow-y-auto">
            {listQuery.isLoading ? (
              <div className="flex items-center justify-center py-10">
                <Loader2 size={20} className="animate-spin text-muted-foreground" />
              </div>
            ) : notifications.length === 0 ? (
              <div className="text-center py-10">
                <Bell size={28} className="mx-auto mb-2 text-muted-foreground/30" />
                <p className="text-[13px] font-semibold text-muted-foreground">No notifications yet</p>
                <p className="text-[11px] text-muted-foreground/60">
                  You&apos;ll be notified about events, jobs, and more.
                </p>
              </div>
            ) : (
              <div className="divide-y divide-border/30">
                {notifications.map((n) => (
                  <NotificationRow
                    key={n.id}
                    notif={n}
                    onMarkRead={(id) => markRead.mutate(id)}
                  />
                ))}
              </div>
            )}
          </div>
        </div>
        </>
      )}
    </div>
  );
}
