"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import axios from "axios";
import { buildTemplate, type SiteBrand, type SiteDocument } from "@alumni/site";
import { getWebsite, publishWebsite, saveWebsiteDraft, unpublishWebsite, type WebsiteStatus } from "@/lib/institution-api";

export type SaveState = "saved" | "saving" | "unsaved" | "error" | "conflict";

const HISTORY = 100;
/** Typing in one place counts as one step for undo; a pause starts a new step. */
const COALESCE_MS = 900;

/**
 * Everything about editing the page: the page itself, undo and redo, saving in the background, publishing. Changes are made by handing
 * `change` a new page. It is saved a moment after the last change, and a save that finds the page changed elsewhere stops and says so
 * instead of overwriting.
 */
export function useWebsiteEditor() {
  const query = useQuery({ queryKey: ["website"], queryFn: getWebsite, staleTime: 0, refetchOnWindowFocus: false, retry: false });
  const [doc, setDoc] = useState<SiteDocument | null>(null);
  const [status, setStatus] = useState<WebsiteStatus | null>(null);
  const [saveState, setSaveState] = useState<SaveState>("saved");
  // The undo and redo stacks live in refs (a state updater must not have side effects); the two counts drive the buttons.
  const pastRef = useRef<SiteDocument[]>([]), futureRef = useRef<SiteDocument[]>([]);
  const [counts, setCounts] = useState({ past: 0, future: 0 });
  const syncCounts = () => setCounts({ past: pastRef.current.length, future: futureRef.current.length });
  const revision = useRef(0), version = useRef(0), savedVersion = useRef(0), lastKey = useRef<{ key: string; at: number } | null>(null);
  const docRef = useRef<SiteDocument | null>(null);
  const saving = useRef<Promise<void> | null>(null);

  const brand: SiteBrand | null = useMemo(() => {
    const b = query.data?.brand; if (!b) return null;
    return { name: b.name, tagline: b.tagline, primary: b.primaryColor, secondary: b.secondaryColor, logoUrl: b.logoUrl, slug: b.slug };
  }, [query.data]);

  // The first load: use the saved draft, or start from the Vibrant look in the institution's own branding.
  useEffect(() => {
    const d = query.data; if (!d || !brand || docRef.current) return;
    const start = d.draft ?? buildTemplate("vibrant", { brand });
    docRef.current = start; setDoc(start); setStatus(d.status); revision.current = d.status.draftRevision;
    // A page that only exists in the browser so far counts as unsaved, so the first autosave creates it.
    // eslint-disable-next-line react-hooks/set-state-in-effect -- copying the loaded page into the editable local copy, once
    if (!d.draft) { version.current = 1; setSaveState("unsaved"); }
  }, [query.data, brand]);

  const change = useCallback((next: SiteDocument, key?: string) => {
    const prev = docRef.current; if (!prev || next === prev) return;
    const now = Date.now();
    const same = key && lastKey.current?.key === key && now - lastKey.current.at < COALESCE_MS;
    lastKey.current = key ? { key, at: now } : null;
    if (!same) pastRef.current = [...pastRef.current.slice(-(HISTORY - 1)), prev];
    futureRef.current = []; syncCounts();
    docRef.current = next; setDoc(next);
    version.current++; setSaveState((s) => (s === "conflict" ? s : "unsaved"));
  }, []);

  const undo = useCallback(() => {
    const prev = pastRef.current.pop(); if (!prev || !docRef.current) return;
    futureRef.current.unshift(docRef.current);
    docRef.current = prev; setDoc(prev); version.current++; setSaveState("unsaved"); lastKey.current = null; syncCounts();
  }, []);
  const redo = useCallback(() => {
    const next = futureRef.current.shift(); if (!next || !docRef.current) return;
    pastRef.current.push(docRef.current);
    docRef.current = next; setDoc(next); version.current++; setSaveState("unsaved"); lastKey.current = null; syncCounts();
  }, []);

  const save = useCallback(async (): Promise<boolean> => {
    if (saving.current) await saving.current;
    const d = docRef.current; if (!d || version.current === savedVersion.current) return true;
    const v = version.current;
    setSaveState("saving");
    const run = (async () => {
      try {
        const s = await saveWebsiteDraft(d, revision.current);
        revision.current = s.draftRevision; savedVersion.current = v; setStatus(s);
        setSaveState(version.current === v ? "saved" : "unsaved");
      } catch (e) {
        setSaveState(axios.isAxiosError(e) && e.response?.status === 409 ? "conflict" : "error");
        throw e;
      }
    })();
    saving.current = run.then(() => undefined, () => undefined);
    try { await run; return true; } catch { return false; } finally { saving.current = null; }
  }, []);

  // Save a moment after the last change.
  useEffect(() => {
    if (saveState !== "unsaved") return;
    const t = setTimeout(() => { void save(); }, 1100);
    return () => clearTimeout(t);
  }, [doc, saveState, save]);

  // Leaving with something unsaved asks first.
  useEffect(() => {
    const warn = (e: BeforeUnloadEvent) => { if (version.current !== savedVersion.current) { e.preventDefault(); e.returnValue = ""; } };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, []);

  const publish = useCallback(async () => {
    if (!(await save())) throw new Error("The page could not be saved first.");
    const s = await publishWebsite(revision.current); setStatus(s); return s;
  }, [save]);
  const unpublish = useCallback(async () => { const s = await unpublishWebsite(); setStatus(s); return s; }, []);

  /** Throw away the browser's copy and take what is saved (after a conflict, or restoring a version). */
  const reload = useCallback(async () => {
    docRef.current = null; setDoc(null); pastRef.current = []; futureRef.current = []; syncCounts(); version.current = 0; savedVersion.current = 0; setSaveState("saved");
    await query.refetch();
  }, [query]);

  return {
    doc, status, brand, saveState, change, undo, redo, canUndo: counts.past > 0, canRedo: counts.future > 0,
    save, publish, unpublish, reload,
    disabledFeatures: query.data?.disabledFeatures ?? [],
    isLoading: query.isLoading || (!!query.data && !doc), error: query.error,
    refetch: query.refetch,
  };
}
