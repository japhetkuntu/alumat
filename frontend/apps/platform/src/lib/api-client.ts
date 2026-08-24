import axios, { AxiosError, AxiosInstance, InternalAxiosRequestConfig } from "axios";
import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import { ApiResponse } from "@/types";

const API_URL = process.env.NEXT_PUBLIC_PLATFORM_API_URL ?? "http://localhost:5300/api/v1";

let isRefreshing = false;
let failedQueue: { resolve: (token: string) => void; reject: (err: unknown) => void }[] = [];

function processQueue(error: unknown, token: string | null) {
  failedQueue.forEach((p) => {
    if (token) p.resolve(token);
    else p.reject(error);
  });
  failedQueue = [];
}

function clearAuthAndRedirect() {
  localStorage.removeItem("access_token");
  localStorage.removeItem("refresh_token");
  localStorage.removeItem("platform_user");
  localStorage.removeItem("platform_tokens");
  window.location.href = "/login";
}

function createClient(baseURL: string): AxiosInstance {
  const instance = axios.create({ baseURL, timeout: 30000 });

  instance.interceptors.request.use((config) => {
    if (typeof window !== "undefined") {
      const token = localStorage.getItem("access_token");
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;
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

      const refreshToken = localStorage.getItem("refresh_token");
      if (!refreshToken) {
        clearAuthAndRedirect();
        return Promise.reject(error);
      }

      if (isRefreshing) {
        return new Promise<string>((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        }).then((token) => {
          originalRequest.headers.Authorization = `Bearer ${token}`;
          return instance(originalRequest);
        });
      }

      isRefreshing = true;
      originalRequest._retry = true;

      try {
        const res = await axios.post(`${baseURL}/auth/refreshtoken`, {
          accessToken: localStorage.getItem("access_token"),
          refreshToken,
        });
        const newTokens = res.data?.data?.tokens ?? res.data?.data;
        const newAccessToken = newTokens?.accessToken;
        const newRefreshToken = newTokens?.refreshToken;

        if (newAccessToken) {
          localStorage.setItem("access_token", newAccessToken);
          if (newRefreshToken) localStorage.setItem("refresh_token", newRefreshToken);
          const storedTokens = localStorage.getItem("platform_tokens");
          if (storedTokens) {
            try {
              const parsed = JSON.parse(storedTokens);
              parsed.accessToken = newAccessToken;
              if (newRefreshToken) parsed.refreshToken = newRefreshToken;
              localStorage.setItem("platform_tokens", JSON.stringify(parsed));
            } catch { /* ignore */ }
          }
          processQueue(null, newAccessToken);
          originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
          return instance(originalRequest);
        }
        throw new Error("No access token in refresh response");
      } catch (refreshError) {
        processQueue(refreshError, null);
        clearAuthAndRedirect();
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }
  );

  return instance;
}

export const platformClient = createClient(API_URL);

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
