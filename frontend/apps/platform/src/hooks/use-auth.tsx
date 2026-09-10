"use client";

import React, { createContext, useContext, useSyncExternalStore } from "react";
import { AuthData, AuthTokens, LoginRequest } from "@/types";
import { loginPlatformStaff } from "@/lib/platform-api";
import { handleApiError, platformClient } from "@/lib/api-client";

interface AuthContextValue {
  user: AuthData | null;
  isLoading: boolean;
  login: (req: LoginRequest) => Promise<void>;
  /** Persists a session obtained outside the normal password login call —
   *  currently just the Google sign-in bridge, which already has the
   *  user/tokens payload from its own API call and only needs it stored. */
  setSession: (user: AuthData, tokens: AuthTokens) => void;
  logout: () => void;
  isPlatformStaff: boolean;
  role: AuthData["role"] | undefined;
  isSuperAdmin: boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

// Hydration-safe read of auth state from localStorage, modeled as an external
// store (see useHostname()) instead of setState-in-effect: the server and the
// first client render both see `null`/`false`, avoiding a hydration mismatch,
// then React syncs to the real value right after mount.
const authListeners = new Set<() => void>();
function subscribeAuth(callback: () => void) {
  authListeners.add(callback);
  return () => authListeners.delete(callback);
}
function notifyAuth() {
  authListeners.forEach((listener) => listener());
}

let cachedRaw: string | null = null;
let cachedUser: AuthData | null = null;
function getUserSnapshot(): AuthData | null {
  const raw = localStorage.getItem("platform_user");
  if (raw !== cachedRaw) {
    cachedRaw = raw;
    try {
      cachedUser = raw ? (JSON.parse(raw) as AuthData) : null;
    } catch {
      localStorage.removeItem("platform_user");
      cachedUser = null;
    }
  }
  return cachedUser;
}
const getUserServerSnapshot = () => null;

function useHasMounted() {
  return useSyncExternalStore(() => () => {}, () => true, () => false);
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const hasMounted = useHasMounted();
  const user = useSyncExternalStore(subscribeAuth, getUserSnapshot, getUserServerSnapshot);

  async function login(req: LoginRequest) {
    try {
      const { user: authUser, tokens } = await loginPlatformStaff(req);
      const userData: AuthData = {
        id: authUser.id,
        email: authUser.email,
        name: authUser.name,
        role: authUser.role as AuthData["role"],
      };
      localStorage.setItem("platform_user", JSON.stringify(userData));
      localStorage.setItem("platform_tokens", JSON.stringify(tokens));
      notifyAuth();
    } catch (error) {
      throw new Error(handleApiError(error));
    }
  }

  function setSession(user: AuthData, tokens: AuthTokens) {
    localStorage.setItem("platform_user", JSON.stringify(user));
    localStorage.setItem("platform_tokens", JSON.stringify(tokens));
    notifyAuth();
  }

  async function logout() {
    // Awaited (not fire-and-forget) — the cookies are httpOnly, so only this
    // backend response can clear them; navigating away immediately risks the
    // browser cancelling the in-flight request, leaving the session live
    // server-side even though the UI looks logged out. Still best-effort: a
    // failed/slow request shouldn't trap the user on the page.
    await platformClient.post("/auth/logout").catch(() => {});
    localStorage.removeItem("platform_user");
    localStorage.removeItem("platform_tokens");
    notifyAuth();
    window.location.href = "/login";
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading: !hasMounted,
        login,
        setSession,
        logout,
        isPlatformStaff: !!user,
        role: user?.role,
        isSuperAdmin: user?.role === "SuperAdmin",
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
