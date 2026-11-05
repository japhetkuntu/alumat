"use client";

import { useState, useRef, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { Bell, Check, CheckCheck, ChevronRight, Dialog, DialogContent, DialogDescription, DialogTitle, Loader2, X, useSwipeDownToClose } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { ZoomableImage } from "@alumni/ui";
import { GPU_LAYER_STYLE } from "@/lib/gpu-layer-style";
import {
  getNotifications,
  getUnreadNotificationCount,
  markNotificationRead,
  markAllNotificationsRead,
} from "@/lib/member-api";
import type { NotificationItem } from "@/types";
import { formatDistanceToNow, isToday } from "date-fns";
import { getTypeMeta, TypeIcon, toNotificationPath } from "@/lib/notification-meta";

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
      // The full /notifications page keeps its own separate infinite-query cache.
      qc.invalidateQueries({ queryKey: ["notifications-infinite"] });
    },
  });

  const markAll = useMutation({
    mutationFn: markAllNotificationsRead,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["notifications-unread-count"] });
      qc.invalidateQueries({ queryKey: ["notifications-list"] });
      qc.invalidateQueries({ queryKey: ["notifications-infinite"] });
    },
  });

  return { unreadQuery, listQuery, markRead, markAll };
}

function NotificationRow({
  notif,
  onMarkRead,
  onNavigate,
  roomy,
}: {
  notif: NotificationItem;
  onMarkRead: (id: string) => void;
  onNavigate: () => void;
  /** Phone sheet: larger type and touch-sized spacing. */
  roomy?: boolean;
}) {
  const [expanded, setExpanded] = useState(false);
  const path = toNotificationPath(notif.actionUrl);
  const meta = getTypeMeta(notif.type);

  const toggle = () => {
    setExpanded((v) => !v);
    if (!notif.isRead) onMarkRead(notif.id);
  };

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={toggle}
      onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); toggle(); } }}
      className={cn(
        "flex gap-3 hover:bg-muted/50 transition-colors cursor-pointer group",
        roomy ? "px-5 py-3.5" : "px-4 py-3",
        !notif.isRead && "bg-accent/5"
      )}
    >
      <TypeIcon type={notif.type} size={roomy ? 16 : 14} boxPx={roomy ? 38 : 30} />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1.5">
          <span className="text-[12px] font-semibold uppercase tracking-wide" style={{ color: meta.color, opacity: 0.85 }}>
            {meta.label}
          </span>
          {!notif.isRead && <span className="w-1.5 h-1.5 rounded-full shrink-0 bg-accent" />}
        </div>
        <p className={cn("leading-snug", roomy ? "text-[14.5px]" : "text-[13px]", !notif.isRead && "font-semibold")}>
          {notif.title}
        </p>
        <p className={cn("text-muted-foreground leading-snug mt-0.5", roomy ? "text-[13px]" : "text-[12px]", !expanded && "line-clamp-2")}>
          {notif.body}
        </p>
        {notif.imageUrl && (
          <div className="mt-1.5" onClick={(e) => e.stopPropagation()}>
            <ZoomableImage
              src={notif.imageUrl}
              alt=""
              wrapperClassName="w-full aspect-[16/9] rounded-lg bg-muted/40"
              className="w-full h-full object-cover"
            />
          </div>
        )}
        <div className="flex items-center gap-3 mt-1">
          <p className="text-[12px] text-muted-foreground/60">
            {formatDistanceToNow(new Date(notif.createdAt), { addSuffix: true })}
          </p>
          {expanded && path && (
            <Link
              href={path}
              onClick={(e) => { e.stopPropagation(); onNavigate(); }}
              className="text-[12px] font-semibold text-accent hover:underline inline-flex items-center gap-0.5"
            >
              View <ChevronRight size={11} />
            </Link>
          )}
        </div>
      </div>
      {!notif.isRead && (
        <button
          onClick={(e) => { e.stopPropagation(); onMarkRead(notif.id); }}
          className="shrink-0 mt-0.5 w-7 h-7 flex items-center justify-center rounded-lg hover:bg-accent/10 text-accent transition-all sm:opacity-0 sm:group-hover:opacity-100"
          aria-label="Mark as read"
        >
          <Check size={13} />
        </button>
      )}
    </div>
  );
}

function useIsPhone() {
  const [phone, setPhone] = useState(false);
  useEffect(() => {
    const m = window.matchMedia("(max-width: 639px)");
    const on = () => setPhone(m.matches);
    on();
    m.addEventListener("change", on);
    return () => m.removeEventListener("change", on);
  }, []);
  return phone;
}

export function NotificationPanel() {
  const [open, setOpen] = useState(false);
  const phone = useIsPhone();
  const swipe = useSwipeDownToClose(() => setOpen(false));
  const panelRef = useRef<HTMLDivElement>(null);
  const { unreadQuery, listQuery, markRead, markAll } = useNotifications();

  const unreadCount = unreadQuery.data ?? 0;
  const notifications = listQuery.data?.results ?? [];

  // Close on outside click
  useEffect(() => {
    if (!open || phone) return;   // the phone sheet is a modal with its own backdrop
    function handle(e: MouseEvent) {
      if (panelRef.current && !panelRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handle);
    return () => document.removeEventListener("mousedown", handle);
  }, [open, phone]);

  const todays = notifications.filter((n) => isToday(new Date(n.createdAt)));
  const earlier = notifications.filter((n) => !isToday(new Date(n.createdAt)));

  return (
    <div className="relative" ref={panelRef}>
      <button
        type="button"
        className="relative grid h-10 w-10 shrink-0 place-items-center rounded-full text-muted-foreground outline-none transition-[background-color,color,transform] duration-150 hover:bg-muted hover:text-foreground active:scale-95 focus-visible:ring-2 focus-visible:ring-primary"
        onClick={() => setOpen((v) => !v)}
        aria-label={unreadCount > 0 ? `Notifications, ${unreadCount} unread` : "Notifications"}
        aria-expanded={open}
      >
        <Bell size={18} />
        {unreadCount > 0 && (
          <span className="absolute right-1 top-1 flex h-[17px] min-w-[17px] items-center justify-center rounded-full bg-accent px-1 text-[11px] font-bold leading-none text-accent-foreground ring-2 ring-background">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </button>

      {phone && (
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogContent ref={swipe.ref} className="flex max-h-[88dvh] flex-col gap-0 overflow-hidden rounded-t-[24px] p-0 pt-4 sm:rounded-[20px]">
            <div className="flex touch-none items-end justify-between gap-3 px-5 pb-3 pr-16" {...swipe.bind}>
              <div className="min-w-0">
                <DialogTitle className="font-[family-name:var(--font-display)] text-[20px] font-semibold tracking-tight">Notifications</DialogTitle>
                <DialogDescription className="mt-0.5 text-[13px]">{unreadCount > 0 ? `${unreadCount} unread` : "You're all caught up"}</DialogDescription>
              </div>
            </div>
            {unreadCount > 0 && (
              <div className="px-5 pb-3">
                <button type="button" onClick={() => markAll.mutate()} disabled={markAll.isPending}
                  className="inline-flex items-center gap-1.5 rounded-full bg-accent/10 px-3.5 py-1.5 text-[13px] font-semibold text-accent transition-colors hover:bg-accent/15 disabled:opacity-50">
                  {markAll.isPending ? <Loader2 size={12} className="animate-spin" /> : <CheckCheck size={12} />} Mark all as read
                </button>
              </div>
            )}
            <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain border-t border-border/50">
              {listQuery.isLoading ? (
                <div className="divide-y divide-border/30">
                  {Array.from({ length: 4 }).map((_, i) => (
                    <div key={i} className="flex animate-pulse gap-3 px-5 py-4">
                      <div className="h-[38px] w-[38px] shrink-0 rounded-full bg-muted" />
                      <div className="min-w-0 flex-1 space-y-2"><div className="h-3 w-3/4 rounded bg-muted" /><div className="h-2.5 w-full rounded bg-muted" /></div>
                    </div>
                  ))}
                </div>
              ) : notifications.length === 0 ? (
                <div className="px-6 py-14 text-center">
                  <span className="mx-auto mb-3 grid h-14 w-14 place-items-center rounded-full bg-muted"><Bell size={22} className="text-muted-foreground" /></span>
                  <p className="text-[15px] font-semibold">Nothing yet</p>
                  <p className="mx-auto mt-1 max-w-[26ch] text-[13px] leading-relaxed text-muted-foreground">You&apos;ll hear about events, jobs, messages and more here.</p>
                </div>
              ) : (
                <>
                  {[["Today", todays], ["Earlier", earlier]].map(([label, group]) => (group as NotificationItem[]).length > 0 && (
                    <section key={label as string}>
                      {(todays.length > 0 && earlier.length > 0) && <h3 className="bg-muted/40 px-5 py-1.5 text-[11.5px] font-semibold uppercase tracking-wide text-muted-foreground">{label as string}</h3>}
                      <div className="divide-y divide-border/30">
                        {(group as NotificationItem[]).map((n) => (
                          <NotificationRow key={n.id} notif={n} roomy onMarkRead={(id) => markRead.mutate(id)} onNavigate={() => setOpen(false)} />
                        ))}
                      </div>
                    </section>
                  ))}
                </>
              )}
            </div>
            <div className="space-y-2 border-t border-border/60 px-5 pb-[max(1rem,env(safe-area-inset-bottom))] pt-3">
              <Link href="/notifications" onClick={() => setOpen(false)}
                className="flex h-11 w-full items-center justify-center rounded-full bg-muted text-[14px] font-semibold text-foreground transition-colors hover:bg-muted/70">
                View all notifications
              </Link>
              <button type="button" onClick={() => setOpen(false)}
                className="flex h-10 w-full items-center justify-center rounded-full text-[14px] font-semibold text-muted-foreground transition-colors hover:text-foreground">
                Close
              </button>
            </div>
          </DialogContent>
        </Dialog>
      )}

      {open && !phone && (
        <>
          {/* Mobile backdrop */}
          <div
            className="fixed inset-0 z-40 bg-black/20 backdrop-blur-sm sm:hidden"
            style={GPU_LAYER_STYLE}
            onClick={() => setOpen(false)}
          />
          <div
            className="fixed inset-x-3 top-[4.5rem] z-50 rounded-2xl border border-border/50 bg-background shadow-2xl shadow-black/10 dark:shadow-black/40 overflow-hidden sm:absolute sm:inset-x-auto sm:right-0 sm:top-full sm:mt-2 sm:w-96"
            style={GPU_LAYER_STYLE}
          >
          {/* Header */}
          <div className="flex items-center justify-between px-4 py-3 border-b border-border/40 bg-muted/30">
            <div className="flex items-center gap-2">
              <Bell size={14} className="text-muted-foreground" />
              <span className="font-semibold text-[13px]">Notifications</span>
              {unreadCount > 0 && (
                <span className="text-[12px] font-bold bg-accent text-accent-foreground px-1.5 py-0.5 leading-none">
                  {unreadCount}
                </span>
              )}
            </div>
            <div className="flex items-center gap-1">
              {unreadCount > 0 && (
                <button
                  onClick={() => markAll.mutate()}
                  disabled={markAll.isPending}
                  className="flex items-center gap-1 text-[12px] font-semibold text-accent hover:text-accent/80 transition-colors px-2 py-1 rounded-lg hover:bg-accent/10 disabled:opacity-50"
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
              <div className="divide-y divide-border/30">
                {Array.from({ length: 4 }).map((_, i) => (
                  <div key={i} className="flex gap-3 px-4 py-3 animate-pulse">
                    <div className="w-2 h-2 rounded-full mt-1.5 shrink-0 bg-muted" />
                    <div className="flex-1 min-w-0 space-y-2">
                      <div className="h-3 rounded bg-muted w-3/4" />
                      <div className="h-2.5 rounded bg-muted w-full" />
                      <div className="h-2 rounded bg-muted w-1/4" />
                    </div>
                  </div>
                ))}
              </div>
            ) : notifications.length === 0 ? (
              <div className="text-center py-10">
                <Bell size={28} className="mx-auto mb-2 text-muted-foreground/30" />
                <p className="text-[13px] font-semibold text-muted-foreground">No notifications yet</p>
                <p className="text-[12px] text-muted-foreground/60">
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
                    onNavigate={() => setOpen(false)}
                  />
                ))}
              </div>
            )}
          </div>

          {/* Footer */}
          <Link
            href="/notifications"
            onClick={() => setOpen(false)}
            className="block text-center py-2.5 text-[12px] font-semibold text-accent hover:bg-accent/5 transition-colors border-t border-border/40"
          >
            View all notifications
          </Link>
        </div>
        </>
      )}
    </div>
  );
}
