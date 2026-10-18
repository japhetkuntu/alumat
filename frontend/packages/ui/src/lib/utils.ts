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

/** "1.4 MB", "220 KB". */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(bytes < 10 * 1024 * 1024 ? 1 : 0)} MB`;
}

/** A short kind for a file, for its label: "PDF", "Word", "Excel", "PowerPoint", or the extension in capitals. */
export function fileKind(name: string): string {
  const ext = name.split(".").pop()?.toLowerCase() ?? "";
  if (ext === "pdf") return "PDF";
  if (ext === "doc" || ext === "docx") return "Word";
  if (ext === "xls" || ext === "xlsx" || ext === "csv") return ext === "csv" ? "CSV" : "Excel";
  if (ext === "ppt" || ext === "pptx") return "PowerPoint";
  return ext ? ext.toUpperCase() : "File";
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
