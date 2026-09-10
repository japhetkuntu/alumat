import axios, { AxiosError, AxiosInstance, InternalAxiosRequestConfig } from "axios";
import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import { ApiResponse } from "@/types";

// Same-origin by default: the browser calls whatever host it's currently on
// (e.g. greenfield.member.yourplatform.example/api/v1/...), and nginx proxies
// that path to member-api while preserving the Host header — which is what
// TenantResolutionMiddleware reads to resolve the tenant. An absolute URL
// baked in at build time would instead always resolve to whichever tenant
// that fixed hostname maps to, no matter which institution's portal the
// browser is actually on. Set NEXT_PUBLIC_MEMBER_API_URL only for setups
// that deliberately want a fixed cross-origin API host.
const API_URL = process.env.NEXT_PUBLIC_MEMBER_API_URL || "/api/v1";

let isRefreshing = false;
let failedQueue: { resolve: () => void; reject: (err: unknown) => void }[] = [];

function processQueue(error: unknown) {
  failedQueue.forEach((p) => {
    if (error) p.reject(error);
    else p.resolve();
  });
  failedQueue = [];
}

function clearAuthAndRedirect() {
  // access_token/refresh_token are httpOnly cookies now — only the backend's
  // own logout/refresh-failure response can clear those. This just drops the
  // non-sensitive client-side copy of who's signed in.
  localStorage.removeItem("user");
  localStorage.removeItem("tokens");
  window.location.href = "/login";
}

function createClient(baseURL: string): AxiosInstance {
  const instance = axios.create({ baseURL, timeout: 30000, withCredentials: true });

  instance.interceptors.request.use((config) => {
    if (typeof window !== "undefined") {
      // Dev-only tenant override — see TenantResolutionMiddleware. Ignored by
      // the backend outside Development, so this is a no-op once real
      // wildcard-subdomain routing is live in production.
      const slug = localStorage.getItem("institution_slug");
      if (slug) {
        config.headers["X-Institution-Slug"] = slug;
      }
    }
    return config;
  });

  instance.interceptors.response.use(
    (res) => res,
    async (error: AxiosError) => {
      const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };
      if (error.response?.status !== 401 || typeof window === "undefined" || originalRequest._retry) {
        return Promise.reject(error);
      }

      if (isRefreshing) {
        return new Promise<void>((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        }).then(() => instance(originalRequest));
      }

      isRefreshing = true;
      originalRequest._retry = true;

      try {
        // No body needed — the refresh token travels as its own httpOnly
        // cookie, and the new tokens come back the same way.
        await axios.post(`${baseURL}/auth/refreshtoken`, null, { withCredentials: true });
        processQueue(null);
        return instance(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError);
        clearAuthAndRedirect();
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }
  );

  return instance;
}

export const memberClient = createClient(API_URL);

/** Unauthenticated client for public endpoints (e.g. registration flow) */
export const publicMemberClient = axios.create({ baseURL: API_URL, timeout: 15000 });
publicMemberClient.interceptors.request.use((config) => {
  if (typeof window !== "undefined") {
    const slug = localStorage.getItem("institution_slug");
    if (slug) {
      config.headers["X-Institution-Slug"] = slug;
    }
  }
  return config;
});

export function handleApiError(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as ApiResponse<unknown> | undefined;
    if (data?.errors?.length) {
      return data.errors.map((e) => e.errorMessage).join(", ");
    }
    return data?.message ?? error.message;
  }
  if (error instanceof Error) return error.message;
  return "An unexpected error occurred";
}

/**
 * Maps backend field-level validation errors (ApiResponse.errors[]) onto a react-hook-form
 * form's fields via setError, so a server-side rejection highlights the specific input
 * instead of only surfacing as a flattened toast. Backend field keys are PascalCase
 * (ASP.NET ModelState); form field names are camelCase, so the first letter is lowercased
 * before matching. Returns true if at least one field error was applied.
 */
export function applyServerFieldErrors<TFieldValues extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<TFieldValues>
): boolean {
  if (!axios.isAxiosError(error)) return false;
  const data = error.response?.data as ApiResponse<unknown> | undefined;
  if (!data?.errors?.length) return false;

  let applied = false;
  for (const e of data.errors) {
    if (!e.field || !e.errorMessage) continue;
    const fieldName = (e.field.charAt(0).toLowerCase() + e.field.slice(1)) as Path<TFieldValues>;
    setError(fieldName, { type: "server", message: e.errorMessage });
    applied = true;
  }
  return applied;
}
