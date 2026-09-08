import { useQuery } from "@tanstack/react-query";
import { memberClient } from "./api-client";

export interface GoogleBridgeTheme {
  displayName?: string | null;
  logoUrl?: string | null;
  iconUrl?: string | null;
  primaryColorHex?: string | null;
  secondaryColorHex?: string | null;
}

/**
 * The current (tenant-resolved, if any) institution's branding — forwarded
 * to the Google sign-in bridge (a separate page on the fixed base domain,
 * with no tenant context of its own) as URL params, so its branding panel
 * can render in this institution's actual colors instead of the generic
 * platform default. See apps/member/src/app/google-auth/page.tsx.
 */
export function useGoogleBridgeTheme() {
  return useQuery({
    queryKey: ["google-bridge-theme"],
    queryFn: async () => {
      const res = await memberClient.get<{ data: GoogleBridgeTheme }>("/public/institution/theme");
      return res.data.data;
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}

/** Builds the bridge URL, carrying whatever institution branding is known so far (may be none — the bridge page falls back to generic branding either way). */
export function buildGoogleBridgeUrl(
  bridgeUrl: string,
  params: { portal: string; return: string; mode?: string; theme?: GoogleBridgeTheme | null },
): string {
  const url = new URL(bridgeUrl);
  url.searchParams.set("portal", params.portal);
  url.searchParams.set("return", params.return);
  if (params.mode) url.searchParams.set("mode", params.mode);

  const theme = params.theme;
  if (theme?.primaryColorHex) url.searchParams.set("primary", theme.primaryColorHex);
  if (theme?.secondaryColorHex) url.searchParams.set("secondary", theme.secondaryColorHex);
  const mark = theme?.iconUrl || theme?.logoUrl;
  if (mark) url.searchParams.set("logo", mark);
  if (theme?.displayName) url.searchParams.set("name", theme.displayName);

  return url.toString();
}
