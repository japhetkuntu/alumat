/**
 * React Query key prefixes safe to persist to localStorage — public,
 * unauthenticated landing-page content only (news/events/spotlight/
 * businesses/theme). Everything else (member session data, contributions,
 * anything behind login) must never touch localStorage, so the persister in
 * providers.tsx filters against this exact list rather than persisting the
 * whole cache.
 */
export const PUBLIC_CACHE_QUERY_KEYS = [
  "member-landing-content",
  "public-news",
  "public-events",
  "public-spotlight",
  "public-businesses",
] as const;
