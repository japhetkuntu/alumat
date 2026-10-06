"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { CalendarDays, MapPin, ArrowRight } from "@alumni/ui";
import { memberClient, publicMemberClient } from "@/lib/api-client";
import { withRedirect } from "@/lib/redirect";

export type GuestEntityType = "event" | "job" | "news";

interface Preview {
  title: string;
  description?: string | null;
  imageUrl?: string | null;
  subtitle?: string | null;
  date?: string | null;
  place?: string | null;
  restricted?: boolean;
}

interface Theme { displayName?: string | null; iconUrl?: string | null; logoUrl?: string | null }

const COPY: Record<GuestEntityType, { dateLabel: string; cta: string; note: string }> = {
  event: { dateLabel: "When", cta: "Sign in to RSVP", note: "Members RSVP and see who is going." },
  job: { dateLabel: "Apply by", cta: "Sign in to see the full posting", note: "Members see the full details and how to apply." },
  news: { dateLabel: "Posted", cta: "Sign in to read the full story", note: "Members read the full post." },
};

function formatWhen(iso: string, withTime: boolean) {
  const d = new Date(iso);
  return d.toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "long", year: "numeric" })
    + (withTime ? `, ${d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" })}` : "");
}

/**
 * What someone sees when they open a shared event, job or news link without being signed in:
 * a short read-only summary and a way in. Only the public preview fields are used; anything scoped
 * to a community arrives as title-only (`restricted`). Signing in or joining returns them to this exact item.
 */
export function GuestEntityPreview({ type, id, path }: { type: GuestEntityType; id: string; path: string }) {
  const copy = COPY[type];
  const { data: preview, isLoading, isError } = useQuery({
    queryKey: ["guest-preview", type, id],
    queryFn: async () => (await publicMemberClient.get<{ data: Preview }>(`/public/preview/${type}/${id}`)).data.data,
    retry: false,
  });
  const { data: theme } = useQuery({
    queryKey: ["auth-mobile-theme"],
    queryFn: async () => (await memberClient.get<{ data: Theme }>("/public/institution/theme")).data.data,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
  const org = theme?.displayName || "our community";
  const mark = theme?.iconUrl || theme?.logoUrl;

  return (
    <div className="min-h-dvh bg-background text-foreground">
      <header className="border-b border-border">
        <div className="mx-auto flex h-14 max-w-2xl items-center justify-between gap-3 px-5">
          <Link href="/" className="flex min-w-0 items-center gap-2.5">
            {mark && <img src={mark} alt="" className="h-7 w-7 shrink-0 object-cover" />}
            <span className="truncate text-[15px] font-semibold">{org}</span>
          </Link>
          <Link href={withRedirect("/login", path)} className="shrink-0 text-[13px] font-semibold text-primary hover:underline">Sign in</Link>
        </div>
      </header>

      <main className="mx-auto max-w-2xl px-5 pb-16 pt-8">
        {isLoading ? (
          <div className="space-y-4 animate-pulse"><div className="h-48 bg-muted" /><div className="h-8 w-3/4 bg-muted" /><div className="h-4 w-1/2 bg-muted" /></div>
        ) : isError || !preview ? (
          <div className="py-16 text-center">
            <h1 className="text-xl font-semibold">We couldn&apos;t find that</h1>
            <p className="mt-2 text-[14px] text-muted-foreground">It may have been removed, or the link is incomplete.</p>
          </div>
        ) : (
          <article>
            {preview.imageUrl && <img src={preview.imageUrl} alt="" className="mb-6 aspect-[16/8] w-full object-cover bg-muted" />}
            {preview.subtitle && <p className="text-[12px] font-semibold uppercase tracking-wider text-primary">{preview.subtitle}</p>}
            <h1 className="mt-1 text-[28px] font-bold leading-tight tracking-tight text-balance sm:text-[34px]">{preview.title}</h1>

            {!preview.restricted && (preview.date || preview.place) && (
              <dl className="mt-5 space-y-2 text-[15px]">
                {preview.date && (
                  <div className="flex items-start gap-2.5"><CalendarDays size={16} className="mt-1 shrink-0 text-muted-foreground" />
                    <div><dt className="sr-only">{copy.dateLabel}</dt><dd>{type === "event" ? formatWhen(preview.date, true) : formatWhen(preview.date, false)}</dd></div></div>
                )}
                {preview.place && (
                  <div className="flex items-start gap-2.5"><MapPin size={16} className="mt-1 shrink-0 text-muted-foreground" />
                    <div><dt className="sr-only">Place</dt><dd>{preview.place}</dd></div></div>
                )}
              </dl>
            )}

            {!preview.restricted && preview.description && (
              <p className="mt-6 line-clamp-6 whitespace-pre-line text-[16px] leading-relaxed text-muted-foreground">{preview.description}</p>
            )}

            <div className="mt-8 border-t border-border pt-6">
              <Link href={withRedirect("/login", path)} className="inline-flex min-h-12 w-full items-center justify-center gap-2 bg-primary px-7 text-[15px] font-semibold text-primary-foreground hover:opacity-90 sm:w-auto">
                {copy.cta} <ArrowRight size={16} />
              </Link>
              <p className="mt-3 text-[13px] text-muted-foreground">
                {preview.restricted ? "This is shared with a specific group in the community." : copy.note}{" "}
                New here? <Link href={withRedirect("/register", path)} className="font-semibold text-foreground underline underline-offset-2">Join {org}</Link>.
              </p>
            </div>
          </article>
        )}
      </main>
    </div>
  );
}
