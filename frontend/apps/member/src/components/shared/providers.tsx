"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import { AuthProvider } from "@/hooks/use-auth";
import { Toaster } from "sonner";
import { TooltipProvider } from "@/components/ui/tooltip";

export function Providers({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: { staleTime: 60 * 1000, retry: 1 },
        },
      })
  );

  return (
    <QueryClientProvider client={queryClient}>
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
    </QueryClientProvider>
  );
}
