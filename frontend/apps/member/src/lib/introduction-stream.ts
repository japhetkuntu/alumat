"use client";

import { useEffect, useSyncExternalStore } from "react";
import { useQueryClient, type QueryClient } from "@tanstack/react-query";
import { API_URL } from "@/lib/api-client";
import { getIntroduction, getIntroSummary, type IntroDetail } from "@/lib/member-api";

/**
 * One live connection for the whole portal. The server sends a tiny notice ("new message in X") the moment something happens, and
 * the page fetches only what changed. While the connection is healthy nothing polls; while it is down the screens fall back to
 * asking every few seconds, and on every (re)connect the page asks once for anything it missed.
 */

// Whether the live connection is up, so screens know whether they still need to poll.
let connected = false;
const listeners = new Set<() => void>();
const setConnected = (v: boolean) => { if (connected !== v) { connected = v; listeners.forEach((l) => l()); } };
export function useStreamConnected() {
  return useSyncExternalStore((cb) => { listeners.add(cb); return () => { listeners.delete(cb); }; }, () => connected, () => false);
}
/** Polling interval for a screen: off while the live connection is up, otherwise the given fallback. */
export const pollWhenOffline = (isLive: boolean, everyMs: number) => (isLive ? false : everyMs);

interface Notice { kind: string; introductionId: string }

// A refresh that is already on its way covers a second notice that arrives meanwhile, so a burst makes one request, not many.
const refresh = (qc: QueryClient, key: readonly unknown[]) => qc.invalidateQueries({ queryKey: key }, { cancelRefetch: false });

const merging = new Map<string, { again: boolean; done: Promise<void> }>();

/** Adds only the messages newer than the ones already on screen, instead of loading the whole conversation again. */
export function mergeNewMessages(qc: QueryClient, id: string): Promise<void> {
  const running = merging.get(id);
  if (running) { running.again = true; return running.done; }   // one more pass after this one, never a pile of them
  const job = { again: false, done: Promise.resolve() };
  job.done = (async () => {
    try {
      do {
        job.again = false;
        const cached = qc.getQueryData<IntroDetail>(["intro", id]);
        if (!cached) return;
        const last = cached.messages[cached.messages.length - 1];
        const fresh = await getIntroduction(id, last?.createdAt);
        qc.setQueryData<IntroDetail>(["intro", id], (old) => {
          if (!old) return fresh;
          const known = new Set(old.messages.map((m) => m.id));
          return { ...fresh, messages: [...old.messages, ...fresh.messages.filter((m) => !known.has(m.id))] };
        });
      } while (job.again);
    } finally { merging.delete(id); }
  })();
  merging.set(id, job);
  return job.done;
}

function apply(qc: QueryClient, n: Notice) {
  // A "read" notice only changes the "Seen" line of a chat that is open; the badge and list change when messages or requests do.
  if (n.kind !== "read") { refresh(qc, ["intro-summary"]); refresh(qc, ["intros"]); refresh(qc, ["intro-state"]); }
  const open = qc.getQueryData<IntroDetail>(["intro", n.introductionId]);
  if (!open) return;
  if (n.kind === "message" || n.kind === "read") void mergeNewMessages(qc, n.introductionId).catch(() => refresh(qc, ["intro", n.introductionId]));
  else refresh(qc, ["intro", n.introductionId]);
}

/** Refreshes everything the live connection would have told us about while it was down. */
function catchUp(qc: QueryClient) {
  ["intro-summary", "intros", "intro", "intro-state"].forEach((k) => refresh(qc, [k]));
}

const HIDDEN_CLOSE_MS = 2 * 60_000;

/** Mount once, high in the signed-in portal. Does nothing when `enabled` is false (feature off, or not signed in as a member). */
export function useIntroductionStream(enabled: boolean) {
  const qc = useQueryClient();

  useEffect(() => {
    if (!enabled || typeof window === "undefined") return;
    let stopped = false;
    let controller: AbortController | null = null;
    let hiddenTimer: number | undefined;
    let retryTimer: number | undefined;
    let attempt = 0;

    async function connect() {
      if (stopped || document.hidden) return;
      controller = new AbortController();
      try {
        const token = localStorage.getItem("access_token");
        const slug = localStorage.getItem("institution_slug");
        const res = await fetch(`${API_URL}/introductions/stream`, {
          headers: { Accept: "text/event-stream", ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(slug ? { "X-Institution-Slug": slug } : {}) },
          signal: controller.signal, cache: "no-store",
        });
        if (res.status === 401) {
          // The login ended (the server closes a stream when it does). One ordinary request renews it through the normal refresh, then we retry.
          await getIntroSummary().catch(() => undefined);
          throw new Error("renew");
        }
        if (res.status === 403 || res.status === 404) { stopped = true; return; }   // feature is off for this community: stay quiet
        if (!res.ok || !res.body) throw new Error(`stream ${res.status}`);

        attempt = 0;
        setConnected(true);
        catchUp(qc);

        const reader = res.body.pipeThrough(new TextDecoderStream()).getReader();
        let buffer = "";
        for (;;) {
          const { value, done } = await reader.read();
          if (done) break;
          buffer += value;
          let at: number;
          while ((at = buffer.indexOf("\n\n")) >= 0) {
            const block = buffer.slice(0, at);
            buffer = buffer.slice(at + 2);
            let event = "message", data = "";
            for (const line of block.split("\n")) {
              if (line.startsWith("event:")) event = line.slice(6).trim();
              else if (line.startsWith("data:")) data += line.slice(5).trim();
            }
            if (event === "intro" && data) { try { apply(qc, JSON.parse(data) as Notice); } catch { /* a malformed notice is ignored */ } }
          }
        }
      } catch {
        /* dropped, refused, renewing or closed on purpose: handled below */
      } finally {
        setConnected(false);
      }
      if (stopped || controller?.signal.aborted) return;
      // Back off with a random spread, so a restart of the server is not met by everyone reconnecting in the same second.
      const delay = Math.min(30_000, 1_000 * 2 ** attempt++) * (0.5 + Math.random());
      retryTimer = window.setTimeout(connect, delay);
    }

    const onVisibility = () => {
      window.clearTimeout(hiddenTimer);
      if (document.hidden) {
        // A tab nobody is looking at does not need to hold a connection.
        hiddenTimer = window.setTimeout(() => { controller?.abort(); setConnected(false); }, HIDDEN_CLOSE_MS);
      } else if (!connected) {
        window.clearTimeout(retryTimer);
        attempt = 0;
        void connect();
      }
    };
    document.addEventListener("visibilitychange", onVisibility);
    void connect();

    return () => {
      stopped = true;
      window.clearTimeout(hiddenTimer);
      window.clearTimeout(retryTimer);
      document.removeEventListener("visibilitychange", onVisibility);
      controller?.abort();
      setConnected(false);
    };
  }, [enabled, qc]);
}
