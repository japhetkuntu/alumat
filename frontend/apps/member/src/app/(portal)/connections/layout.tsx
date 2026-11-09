"use client";

import { Suspense, useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { usePathname } from "next/navigation";
import { cn } from "@alumni/ui";
import { ConversationList } from "@/components/member/conversation-list";

/**
 * Connections is a messenger: on a wide screen the list sits beside the open conversation; on a phone the list is the page and
 * a conversation opens full-screen over everything (menu bar included), like any chat app.
 */
export default function ConnectionsLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const activeId = pathname.startsWith("/connections/") ? pathname.split("/")[2] : undefined;
  const inChat = !!activeId;

  // On a phone the open chat is pinned over the whole screen. It is placed directly in <body> rather than inside the portal's scrolling
  // <main>: a fixed element inside a scroller is where phone browsers lose touch scrolling, and nothing there can then sit above it.
  const [phone, setPhone] = useState(false);
  useEffect(() => {
    const m = window.matchMedia("(max-width: 1023px)");
    const on = () => setPhone(m.matches);
    on();
    m.addEventListener("change", on);
    return () => m.removeEventListener("change", on);
  }, []);
  const pinned = inChat && phone;

  // The on-screen keyboard: phone browsers do not shrink the page for it, so the message box would sit under the keyboard. Follow the
  // visible area instead, so the header stays on screen and the message box rides just above the keyboard.
  const [view, setView] = useState<{ h: number; top: number } | null>(null);
  useEffect(() => {
    const vv = window.visualViewport;
    if (!pinned || !vv) { setView(null); return; }
    const on = () => setView({ h: vv.height, top: vv.offsetTop });
    on();
    vv.addEventListener("resize", on);
    vv.addEventListener("scroll", on);
    return () => { vv.removeEventListener("resize", on); vv.removeEventListener("scroll", on); };
  }, [pinned]);
  // While the chat is open the page behind it must not scroll under the user's finger.
  useEffect(() => {
    if (!pinned) return;
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { document.body.style.overflow = prev; };
  }, [pinned]);

  return (
    <div className="mx-auto max-w-[1200px] lg:grid lg:h-[calc(100dvh-3.5rem-3rem)] lg:grid-cols-[360px_minmax(0,1fr)] lg:overflow-hidden lg:rounded-2xl lg:border lg:shadow-sm" style={{ borderColor: "var(--border)" }}>
      <aside className={cn("min-h-0 flex-col lg:flex lg:border-r", inChat ? "hidden" : "flex h-[calc(100dvh-8.5rem-env(safe-area-inset-top)-env(safe-area-inset-bottom))] sm:h-[calc(100dvh-9.5rem-env(safe-area-inset-top)-env(safe-area-inset-bottom))]")} style={{ borderColor: "var(--border)" }}>
        <Suspense fallback={null}><ConversationList activeId={activeId} /></Suspense>
      </aside>
      {pinned
        ? createPortal(<section className="fixed inset-x-0 z-50 flex min-h-0 flex-col overflow-hidden bg-background" style={{ top: view?.top ?? 0, height: view ? `${view.h}px` : "100dvh" }}>{children}</section>, document.body)
        : <section className={cn("min-h-0 flex-col bg-background", inChat ? "flex" : "hidden lg:flex")}>{children}</section>}
    </div>
  );
}
