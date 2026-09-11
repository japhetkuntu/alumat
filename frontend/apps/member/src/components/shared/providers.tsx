"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { PersistQueryClientProvider } from "@tanstack/react-query-persist-client";
import { createSyncStoragePersister } from "@tanstack/query-sync-storage-persister";
import { useState } from "react";
import { AuthProvider } from "@/hooks/use-auth";
import { PUBLIC_CACHE_QUERY_KEYS } from "@/lib/public-cache-keys";
import { Toaster } from "sonner";
import { TooltipProvider } from "@alumni/ui";

/**
 * Public landing-page content (news/events/spotlight/businesses/theme) is
 * persisted to localStorage so a returning visitor on slow internet sees
 * the last-known page instantly instead of a loading skeleton, while
 * React Query quietly refetches in the background and swaps in anything
 * that changed — no separate "reload" step, no flash of empty content.
 * Everything else (auth-scoped member data) stays in-memory only, filtered
 * out below, so nothing private ever reaches localStorage.
 */
function createPersister() {
  if (typeof window === "undefined") return undefined;
  return createSyncStoragePersister({
    storage: window.localStorage,
    key: "alumni-member-public-cache",
  });
}

export function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: { staleTime: 60 * 1000, retry: 1 },
        },
      })
  );
  const [persister] = useState(createPersister);

  const content = (
    <AuthProvider>
      <TooltipProvider delayDuration={350}>
        {children}
      </TooltipProvider>
      <Toaster
        richColors
        position="top-right"
        toastOptions={{
          className: "!rounded-[12px] !shadow-[0_8px_32px_rgba(0,0,0,0.12)] !border-border/60 !text-[14px] !z-[9999]",
          duration: 4500,
        }}
      />
    </AuthProvider>
  );

  if (!persister) {
    // Server-side render — localStorage doesn't exist yet, fall back to a plain in-memory client.
    return <QueryClientProvider client={queryClient}>{content}</QueryClientProvider>;
  }

  return (
    <PersistQueryClientProvider
      client={queryClient}
      persistOptions={{
        persister,
        maxAge: 24 * 60 * 60 * 1000,
        dehydrateOptions: {
          shouldDehydrateQuery: (query) =>
            typeof query.queryKey[0] === "string" &&
            (PUBLIC_CACHE_QUERY_KEYS as readonly string[]).includes(query.queryKey[0]) &&
            query.state.status === "success",
        },
      }}
    >
      {content}
    </PersistQueryClientProvider>
  );
}
