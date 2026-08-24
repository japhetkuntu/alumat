"use client";

import React, { createContext, useContext, useSyncExternalStore } from "react";
import { AuthData, AuthTokens, LoginRequest } from "@/types";
import { institutionClient } from "@/lib/api-client";

interface AuthContextValue {
  user: AuthData | null;
  tokens: AuthTokens | null;
  isLoading: boolean;
  login: (req: LoginRequest) => Promise<void>;
  logout: () => void;
  isAdmin: boolean;
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

function createJSONSnapshot<T>(key: string) {
  let cachedRaw: string | null = null;
  let cachedValue: T | null = null;
  return function getSnapshot(): T | null {
    const raw = localStorage.getItem(key);
    if (raw !== cachedRaw) {
      cachedRaw = raw;
      try {
        cachedValue = raw ? (JSON.parse(raw) as T) : null;
      } catch {
        localStorage.removeItem(key);
        cachedValue = null;
      }
    }
    return cachedValue;
  };
}

const getUserSnapshot = createJSONSnapshot<AuthData>("user");
const getUserServerSnapshot = () => null;
const getTokensSnapshot = createJSONSnapshot<AuthTokens>("tokens");
const getTokensServerSnapshot = () => null;

function useHasMounted() {
  return useSyncExternalStore(() => () => {}, () => true, () => false);
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const hasMounted = useHasMounted();
  const user = useSyncExternalStore(subscribeAuth, getUserSnapshot, getUserServerSnapshot);
  const tokens = useSyncExternalStore(subscribeAuth, getTokensSnapshot, getTokensServerSnapshot);
  const isLoading = !hasMounted;

  function persist(u: AuthData, t: AuthTokens) {
    localStorage.setItem("user", JSON.stringify(u));
    localStorage.setItem("tokens", JSON.stringify(t));
    localStorage.setItem("access_token", t.accessToken);
    localStorage.setItem("refresh_token", t.refreshToken);
    notifyAuth();
  }

  async function login(req: LoginRequest) {
    const res = await institutionClient.post<{ data: { user: AuthData; tokens: AuthTokens } }>(
      "/auth/login",
      req
    );
    persist(res.data.data.user, res.data.data.tokens);
  }

  function logout() {
    localStorage.removeItem("user");
    localStorage.removeItem("tokens");
    localStorage.removeItem("access_token");
    localStorage.removeItem("refresh_token");
    notifyAuth();
    window.location.href = "/login";
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        tokens,
        isLoading,
        login,
        logout,
        isAdmin: user?.role === "Admin" || user?.role === "SuperAdmin",
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
