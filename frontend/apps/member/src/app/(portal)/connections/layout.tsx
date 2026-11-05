"use client";

import { Suspense } from "react";
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

  return (
    <div className="mx-auto max-w-[1200px] lg:grid lg:h-[calc(100dvh-3.5rem-3rem)] lg:grid-cols-[360px_minmax(0,1fr)] lg:overflow-hidden lg:rounded-2xl lg:border lg:shadow-sm" style={{ borderColor: "var(--border)" }}>
      <aside className={cn("min-h-0 flex-col lg:flex lg:border-r", inChat ? "hidden" : "flex h-[calc(100dvh-8.5rem)] sm:h-[calc(100dvh-9.5rem)]")} style={{ borderColor: "var(--border)" }}>
        <Suspense fallback={null}><ConversationList activeId={activeId} /></Suspense>
      </aside>
      <section className={cn("min-h-0 flex-col bg-background", inChat ? "fixed inset-0 z-50 flex lg:static lg:z-auto" : "hidden lg:flex")}>
        {children}
      </section>
    </div>
  );
}
