"use client";

import React, { createContext, useContext, useSyncExternalStore } from "react";
import { AuthData, LoginRequest } from "@/types";
import { loginPlatformStaff } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

interface AuthContextValue {
  user: AuthData | null;
  isLoading: boolean;
  login: (req: LoginRequest) => Promise<void>;
  logout: () => void;
  isPlatformStaff: boolean;
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
      localStorage.setItem("access_token", tokens.accessToken);
      localStorage.setItem("refresh_token", tokens.refreshToken);
      notifyAuth();
    } catch (error) {
      throw new Error(handleApiError(error));
    }
  }

  function logout() {
    localStorage.removeItem("platform_user");
    localStorage.removeItem("platform_tokens");
    localStorage.removeItem("access_token");
    localStorage.removeItem("refresh_token");
    notifyAuth();
    window.location.href = "/login";
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading: !hasMounted,
        login,
        logout,
        isPlatformStaff: !!user,
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
