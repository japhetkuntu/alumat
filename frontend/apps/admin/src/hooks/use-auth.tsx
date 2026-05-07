"use client";

import React, { createContext, useContext, useState } from "react";
import { AuthData, AuthTokens, LoginRequest } from "@/types";
import { adminClient } from "@/lib/api-client";

interface AuthContextValue {
  user: AuthData | null;
  tokens: AuthTokens | null;
  isLoading: boolean;
  login: (req: LoginRequest) => Promise<void>;
  logout: () => void;
  isAdmin: boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthData | null>(null);
  const [tokens, setTokens] = useState<AuthTokens | null>(null);
  const [isHydrated, setIsHydrated] = useState(false);

  React.useEffect(() => {
    try {
      const storedUser = localStorage.getItem("user");
      if (storedUser) setUser(JSON.parse(storedUser));
    } catch {
      localStorage.removeItem("user");
    }

    try {
      const storedTokens = localStorage.getItem("tokens");
      if (storedTokens) setTokens(JSON.parse(storedTokens));
    } catch {
      localStorage.removeItem("tokens");
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
    }

    setIsHydrated(true);
  }, []);

  const isLoading = !isHydrated;

  function persist(u: AuthData, t: AuthTokens) {
    setUser(u);
    setTokens(t);
    localStorage.setItem("user", JSON.stringify(u));
    localStorage.setItem("tokens", JSON.stringify(t));
    localStorage.setItem("access_token", t.accessToken);
    localStorage.setItem("refresh_token", t.refreshToken);
  }

  async function login(req: LoginRequest) {
    const res = await adminClient.post<{ data: { user: AuthData; tokens: AuthTokens } }>(
      "/auth/login",
      req
    );
    persist(res.data.data.user, res.data.data.tokens);
  }

  function logout() {
    setUser(null);
    setTokens(null);
    localStorage.removeItem("user");
    localStorage.removeItem("tokens");
    localStorage.removeItem("access_token");
    localStorage.removeItem("refresh_token");
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
