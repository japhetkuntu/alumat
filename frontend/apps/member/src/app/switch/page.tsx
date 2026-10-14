"use client";

import { Suspense, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { publicMemberClient } from "@/lib/api-client";

/**
 * The landing point of "Switch community". The one-time code in the address is exchanged here, on this community's own portal,
 * for a session, and the person goes straight to their home page. The code is removed from the address first, so it never sits in
 * the browser history.
 */
function SwitchInner() {
  const params = useSearchParams();
  const { setSession } = useAuth();
  const code = params.get("code");
  const [failed, setFailed] = useState(!code);
  const ran = useRef(false);

  useEffect(() => {
    if (ran.current || !code) return;
    ran.current = true;
    window.history.replaceState(null, "", "/switch");

    // Locally the API cannot see the portal's host, so it is told which community this is.
    const host = window.location.hostname;
    const headers: Record<string, string> = {};
    if (host.endsWith(".localhost")) headers["X-Institution-Slug"] = host.split(".")[0];

    publicMemberClient
      .post<{ data: { user: Parameters<typeof setSession>[0]; tokens: Parameters<typeof setSession>[1] } }>("/auth/switch/exchange", { code }, { headers })
      .then((res) => {
        setSession(res.data.data.user, res.data.data.tokens);
        window.location.replace("/dashboard");
      })
      .catch(() => setFailed(true));
  }, [code, setSession]);

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-3 px-6 text-center">
      {failed ? (
        <>
          <h1 className="text-[20px] font-bold">That link has expired</h1>
          <p className="max-w-[40ch] text-[14px] text-muted-foreground">For your safety it only works once, for a minute. Please sign in to continue.</p>
          <Link href="/login" className="mt-2 inline-flex h-11 items-center bg-primary px-5 text-[14px] font-semibold text-primary-foreground">Sign in</Link>
        </>
      ) : (
        <>
          <div className="size-8 animate-spin rounded-full border-2 border-border border-t-primary" aria-hidden />
          <p className="text-[14px] text-muted-foreground" role="status">Taking you there…</p>
        </>
      )}
    </main>
  );
}

export default function SwitchPage() {
  return <Suspense fallback={null}><SwitchInner /></Suspense>;
}
