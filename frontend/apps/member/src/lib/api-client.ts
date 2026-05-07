import axios, { AxiosError, AxiosInstance, InternalAxiosRequestConfig } from "axios";
import { ApiResponse } from "@/types";

const API_URL = process.env.NEXT_PUBLIC_MEMBER_API_URL ?? "http://localhost:5200/api/v1";

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
  localStorage.removeItem("user");
  localStorage.removeItem("tokens");
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
        const res = await axios.post(`${baseURL}/auth/refresh`, {
          accessToken: localStorage.getItem("access_token"),
          refreshToken,
        });
        const newTokens = res.data?.data?.tokens ?? res.data?.data;
        const newAccessToken = newTokens?.accessToken;
        const newRefreshToken = newTokens?.refreshToken;

        if (newAccessToken) {
          localStorage.setItem("access_token", newAccessToken);
          if (newRefreshToken) localStorage.setItem("refresh_token", newRefreshToken);
          const storedTokens = localStorage.getItem("tokens");
          if (storedTokens) {
            try {
              const parsed = JSON.parse(storedTokens);
              parsed.accessToken = newAccessToken;
              if (newRefreshToken) parsed.refreshToken = newRefreshToken;
              localStorage.setItem("tokens", JSON.stringify(parsed));
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

export const memberClient = createClient(API_URL);

/** Unauthenticated client for public endpoints (e.g. registration flow) */
export const publicMemberClient = axios.create({ baseURL: API_URL, timeout: 15000 });

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
