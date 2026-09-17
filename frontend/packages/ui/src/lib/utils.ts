import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatCurrency(amount: number, currency = "GHS"): string {
  return new Intl.NumberFormat("en-GH", {
    style: "currency",
    currency,
    minimumFractionDigits: 2,
  }).format(amount);
}

export function formatDate(date: string | Date): string {
  return new Intl.DateTimeFormat("en-GH", {
    year: "numeric",
    month: "short",
    day: "numeric",
  }).format(new Date(date));
}

export function formatDateTime(date: string | Date): string {
  return new Intl.DateTimeFormat("en-GH", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(date));
}

export function getInitials(name: string): string {
  return name
    .split(" ")
    .map((n) => n[0])
    .join("")
    .toUpperCase()
    .slice(0, 2);
}

export function truncate(str: string, length: number): string {
  if (str.length <= length) return str;
  return str.slice(0, length) + "...";
}

/**
 * A user-entered URL (LinkedIn, business website, job apply link, etc.) without
 * a scheme — "cravencogh.com" rather than "https://cravencogh.com" — is a valid
 * relative path as far as `<a href>` is concerned, so the browser resolves it
 * against the CURRENT page instead of navigating out. Prefixing "https://"
 * when no scheme is present turns it back into an absolute link. Leaves
 * mailto:/tel:/relative-app-path hrefs and already-schemed URLs untouched.
 */
export function ensureAbsoluteUrl(url: string): string {
  if (!url) return url;
  return /^([a-z][a-z0-9+.-]*:|\/)/i.test(url) ? url : `https://${url}`;
}
