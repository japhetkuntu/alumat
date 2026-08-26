"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { getInitials } from "@alumni/ui";
import { institutionClient } from "@/lib/api-client";

interface BrandMarkTheme {
  displayName?: string | null;
  iconUrl?: string | null;
  logoUrl?: string | null;
}

// The desktop branding aside ((auth)/layout.tsx) fetches theme server-side;
// this renders inside the client-only page body (mobile header + the
// desktop "mark" shown above the form), so it needs its own client fetch of
// the same public endpoint.
function useAuthTheme() {
  return useQuery({
    queryKey: ["auth-brand-mark-theme"],
    queryFn: async () => {
      const res = await institutionClient.get<{ data: BrandMarkTheme }>("/public/institution/theme");
      return res.data.data;
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}

function Mark({ size, className }: { size: number; className?: string }) {
  const { data: theme } = useAuthTheme();
  const displayName = theme?.displayName || "Institution Portal";
  const markImage = theme?.iconUrl || theme?.logoUrl;

  return (
    <div
      className={className}
      style={{ width: size, height: size, borderRadius: 10, overflow: "hidden" }}
    >
      {markImage ? (
        <img src={markImage} alt={displayName} className="w-full h-full object-cover" />
      ) : (
        <div className="w-full h-full flex items-center justify-center font-extrabold bg-primary text-white" style={{ fontSize: size * 0.42 }}>
          {getInitials(displayName)}
        </div>
      )}
    </div>
  );
}

export function AuthMobileBrand() {
  const { data: theme } = useAuthTheme();
  const displayName = theme?.displayName || "Institution Portal";
  return (
    <Link href="/" className="mb-10 md:hidden flex flex-col items-center gap-3 transition-opacity hover:opacity-80">
      <Mark size={48} />
      <div className="text-center">
        <p className="text-[15px] font-semibold" style={{ color: "var(--foreground)" }}>{displayName}</p>
        <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>Institution Portal</p>
      </div>
    </Link>
  );
}

export function AuthDesktopMark() {
  return (
    <Link href="/" className="hidden md:flex mb-6 w-fit transition-opacity hover:opacity-80">
      <Mark size={52} />
    </Link>
  );
}
