"use client";

import Link from "next/link";
import { RichText } from "@alumni/ui";
import { withRedirect } from "@/lib/redirect";

export interface ReaderPreview {
  title: string;
  description?: string | null;
  imageUrl?: string | null;
  subtitle?: string | null;
  date?: string | null;
  place?: string | null;
  restricted?: boolean;
  isPublic?: boolean;
  html?: string | null;
  images?: string[] | null;
  facts?: { label: string; value: string }[] | null;
}

const WORDS: Record<string, { back: string; joinNote: string; signInNote: string }> = {
  news: { back: "All news", joinNote: "Members get comments, every story and everything else the community shares.", signInNote: "Already a member? Sign in to join the conversation." },
  event: { back: "Events", joinNote: "Members RSVP, see who is going and get reminders.", signInNote: "Already a member? Sign in to RSVP." },
  job: { back: "Opportunities", joinNote: "Members can apply and message the person who posted this.", signInNote: "Already a member? Sign in to apply." },
  community: { back: "Communities", joinNote: "Members ask to join and the group's leaders approve.", signInNote: "Already a member? Sign in to ask to join." },
  spotlight: { back: "Member spotlights", joinNote: "Join to be featured, cheer others on and stay close to your class.", signInNote: "Already a member? Sign in to see more stories." },
};

const when = (iso: string, withTime: boolean) => {
  const d = new Date(iso);
  return d.toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "long", year: "numeric" }) + (withTime ? `, ${d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" })}` : "");
};

/**
 * A whole story, event, job or spotlight for anyone to read, in the website's own look. Only what the institution made public is
 * here; anything limited to a group shows its title and a way in. It always ends by inviting the reader to join or sign in.
 */
export function ReaderArticle({ type, preview, portalPath, orgName }: { type: string; preview: ReaderPreview; portalPath: string; orgName: string }) {
  const w = WORDS[type] ?? WORDS.news;
  const full = !preview.restricted && !!preview.html;
  return (
    <div className="wrap readwrap">
      <Link href="/" className="readback">← Back to {orgName}</Link>
      {preview.imageUrl && <img className="readhero" src={preview.imageUrl} alt="" />}
      <div className="readgrid">
      <article className="read">
        {preview.subtitle && <p className="readsub">{preview.subtitle}</p>}
        <h1 className="h-lg readtitle">{preview.title}</h1>
        {!preview.restricted && (preview.date || preview.place) && (
          <p className="readmeta">{[preview.date ? when(preview.date, type === "event") : null, preview.place].filter(Boolean).join("  ·  ")}</p>
        )}
        {!preview.restricted && preview.facts && preview.facts.length > 0 && (
          <dl className="readfacts">{preview.facts.map((f) => <div key={f.label}><dt>{f.label}</dt><dd>{f.value}</dd></div>)}</dl>
        )}
        {full ? <RichText content={preview.html} className="readbody" />
          : !preview.restricted && preview.description ? <><p className="readbody">{preview.description}</p><p className="readnote">The rest of this is kept for members. Join or sign in below to read it in full.</p></>
          : preview.restricted ? <p className="readbody">This is shared with a group of members. Sign in to read it.</p> : null}
        {full && preview.images && preview.images.length > 0 && (
          <div className="readgallery">{preview.images.map((src) => <img key={src} src={src} alt="" loading="lazy" />)}</div>
        )}
      </article>

      <aside className="readcta" aria-label="Join or sign in">
        <h2>Be part of {orgName}</h2>
        <p>{w.joinNote}</p>
        <div className="btns">
          <Link className="btn" href={withRedirect("/register", portalPath)}>Join the community</Link>
          <Link className="btn ghost" href={withRedirect("/login", portalPath)}>Sign in</Link>
        </div>
        <small>{w.signInNote}</small>
      </aside>
      </div>
    </div>
  );
}
