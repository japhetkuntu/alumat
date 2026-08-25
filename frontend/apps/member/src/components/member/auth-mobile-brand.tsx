"use client";

import { useQuery } from "@tanstack/react-query";
import { getInitials } from "@alumni/ui";
import { memberClient } from "@/lib/api-client";

interface MobileBrandTheme {
  displayName?: string | null;
  iconUrl?: string | null;
  logoUrl?: string | null;
  tagline?: string | null;
}

// Mirrors the icon → initials fallback used in the desktop branding panel
// (see (auth)/layout.tsx) — this block only renders on small screens, where
// that panel is hidden, so it needs its own client-side fetch of the same
// public theme endpoint (the layout's fetch is server-only).
function useAuthTheme() {
  return useQuery({
    queryKey: ["auth-mobile-theme"],
    queryFn: async () => {
      const res = await memberClient.get<{ data: MobileBrandTheme }>("/public/institution/theme");
      return res.data.data;
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}

export function AuthMobileBrand({ fallbackTagline }: { fallbackTagline: string }) {
  const { data: theme } = useAuthTheme();
  const displayName = theme?.displayName || "Alumni Portal";
  const markImage = theme?.iconUrl || theme?.logoUrl;

  return (
    <div className="mb-10 text-center md:hidden">
      <div className="w-14 h-14 rounded-2xl overflow-hidden mx-auto mb-5 shadow-sm border border-border flex items-center justify-center bg-primary/10">
        {markImage ? (
          <img src={markImage} alt={displayName} className="w-full h-full object-cover" />
        ) : (
          <span className="text-[18px] font-bold text-primary">{getInitials(displayName)}</span>
        )}
      </div>
      <h1 className="font-[family-name:var(--font-display)] text-2xl font-semibold tracking-tight text-foreground">{displayName}</h1>
      <p className="text-muted-foreground text-sm mt-1">{theme?.tagline || fallbackTagline}</p>
    </div>
  );
}
