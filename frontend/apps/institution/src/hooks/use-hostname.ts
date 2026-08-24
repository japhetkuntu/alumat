import { useSyncExternalStore } from "react";

function subscribe() {
  // The hostname never changes during a session, so there's nothing to
  // subscribe to — return a no-op unsubscribe.
  return () => {};
}

function getSnapshot() {
  return window.location.hostname;
}

function getServerSnapshot() {
  // window isn't available during SSR; React reconciles this against the
  // client snapshot after hydration without a mismatch warning.
  return null;
}

/**
 * Hydration-safe read of the current browser hostname.
 * Cosmetic/structural only — real per-subdomain tenant resolution against
 * the backend is future work.
 */
export function useHostname(): string | null {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}
