"use client";

import Link from "next/link";
import { ArrowRight, Mail, MapPin, Phone, Users } from "@alumni/ui";
import { FitImage } from "@alumni/ui";
import { DataPromise } from "@/components/member/data-promise";

export interface PublicCommunityItem { id: string; name: string; kind?: string | null; description: string; coverImageUrl?: string | null; memberCount: number }
export interface PublicLeaderItem { name: string; role: string; photoUrl?: string | null; email?: string | null; phone?: string | null }
export interface PublicProfileData { contactEmail?: string | null; contactPhone?: string | null; address?: string | null; leaders?: PublicLeaderItem[] | null }

function initials(name: string) {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0]?.toUpperCase()).join("");
}

/** True when there is anything in the profile worth a section. */
export function hasPublicProfile(p?: PublicProfileData | null) {
  return !!p && !!(p.contactEmail || p.contactPhone || p.address || p.leaders?.length);
}

/** Groups the institution chose to show: name, description, a head count, never who is in them. */
export function GroupsSection({ items }: { items: PublicCommunityItem[] }) {
  if (items.length === 0) return null;
  return (
    <section id="groups" className="border-b" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
      <div className="section__inner--wide section">
        <p className="text-[14px] font-semibold tracking-[0.12em] uppercase mb-3" style={{ color: "var(--primary)" }}>Groups</p>
        <h2 className="font-[family-name:var(--font-display)] mb-3" style={{ color: "var(--foreground)", fontSize: "1.6rem" }}>Find your people</h2>
        <p className="mb-8 max-w-[60ch] text-[16px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
          Chapters, committees and interest groups you can ask to join. Sign in to request a place; the group&apos;s leaders approve it.
        </p>
        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {items.map((g) => (
            <Link key={g.id} href={`/communities/${g.id}`} className="group flex flex-col border transition-colors hover:border-[var(--primary)]" style={{ borderColor: "var(--border)", background: "var(--card)" }}>
              {g.coverImageUrl ? (
                <FitImage src={g.coverImageUrl} alt="" className="w-full" style={{ aspectRatio: "16/8", background: "var(--muted)" }} />
              ) : (
                <div className="flex items-center justify-center" style={{ aspectRatio: "16/8", background: "color-mix(in oklch, var(--primary) 8%, var(--muted))" }}>
                  <Users size={26} style={{ color: "var(--primary)", opacity: 0.5 }} />
                </div>
              )}
              <div className="flex flex-1 flex-col p-5">
                {g.kind && <p className="mb-1 text-[13px] font-bold uppercase tracking-[0.1em]" style={{ color: "var(--primary)" }}>{g.kind}</p>}
                <h3 className="text-[19px] font-semibold leading-snug group-hover:text-primary" style={{ color: "var(--foreground)" }}>{g.name}</h3>
                {g.description && <p className="mt-2 line-clamp-3 text-[16px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>{g.description}</p>}
                <div className="mt-auto flex items-center justify-between pt-4 text-[15px]">
                  <span style={{ color: "var(--muted-foreground)" }}>{g.memberCount > 0 ? `${g.memberCount.toLocaleString()} ${g.memberCount === 1 ? "member" : "members"}` : "Open to new members"}</span>
                  <span className="flex items-center gap-1 font-semibold" style={{ color: "var(--primary)" }}>View <ArrowRight size={12} /></span>
                </div>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}

/** The people the administrators chose to show, and how to reach the community. Only what was typed in settings. */
export function LeadershipSection({ profile }: { profile?: PublicProfileData | null }) {
  if (!hasPublicProfile(profile)) return null;
  const leaders = profile?.leaders ?? [];
  const hasContact = !!(profile?.contactEmail || profile?.contactPhone || profile?.address);
  return (
    <section id="leadership" className="border-b" style={{ background: "var(--muted)", borderColor: "var(--border)" }}>
      <div className="section__inner--wide section">
        <p className="text-[14px] font-semibold tracking-[0.12em] uppercase mb-3" style={{ color: "var(--primary)" }}>Leadership</p>
        <h2 className="font-[family-name:var(--font-display)] mb-8" style={{ color: "var(--foreground)", fontSize: "1.6rem" }}>
          {leaders.length > 0 ? "Who runs this community" : "Get in touch"}
        </h2>
        <div className={hasContact && leaders.length > 0 ? "grid gap-10 lg:grid-cols-[1fr_320px]" : "grid gap-10"}>
          {leaders.length > 0 && (
            <ul className="grid gap-6 sm:grid-cols-2 xl:grid-cols-3">
              {leaders.map((l, i) => (
                <li key={`${l.name}-${i}`} className="flex gap-4">
                  {l.photoUrl ? (
                    <FitImage src={l.photoUrl} alt={l.name} className="h-16 w-16 shrink-0" style={{ background: "var(--card)" }} />
                  ) : (
                    <div aria-hidden className="flex h-16 w-16 shrink-0 items-center justify-center text-[20px] font-semibold" style={{ background: "var(--primary)", color: "var(--primary-foreground, white)" }}>{initials(l.name)}</div>
                  )}
                  <div className="min-w-0">
                    <p className="text-[18px] font-semibold leading-snug" style={{ color: "var(--foreground)" }}>{l.name}</p>
                    {l.role && <p className="text-[16px]" style={{ color: "var(--muted-foreground)" }}>{l.role}</p>}
                    {l.email && <a href={`mailto:${l.email}`} className="mt-1 block truncate text-[15px] font-medium hover:underline" style={{ color: "var(--primary)" }}>{l.email}</a>}
                    {l.phone && <a href={`tel:${l.phone.replace(/\s+/g, "")}`} className="block text-[15px] font-medium hover:underline" style={{ color: "var(--primary)" }}>{l.phone}</a>}
                  </div>
                </li>
              ))}
            </ul>
          )}
          {hasContact && (
            <div className="border p-6" style={{ borderColor: "var(--border)", background: "var(--card)" }}>
              <p className="mb-4 text-[17px] font-semibold" style={{ color: "var(--foreground)" }}>Contact the community</p>
              <ul className="space-y-3 text-[16px]">
                {profile?.contactEmail && <li className="flex gap-3"><Mail size={16} className="mt-1 shrink-0" style={{ color: "var(--primary)" }} /><a href={`mailto:${profile.contactEmail}`} className="break-all hover:underline">{profile.contactEmail}</a></li>}
                {profile?.contactPhone && <li className="flex gap-3"><Phone size={16} className="mt-1 shrink-0" style={{ color: "var(--primary)" }} /><a href={`tel:${profile.contactPhone.replace(/\s+/g, "")}`} className="hover:underline">{profile.contactPhone}</a></li>}
                {profile?.address && <li className="flex gap-3"><MapPin size={16} className="mt-1 shrink-0" style={{ color: "var(--primary)" }} /><span className="whitespace-pre-line">{profile.address}</span></li>}
              </ul>
            </div>
          )}
        </div>
      </div>
    </section>
  );
}

/** What happens to a member's details, shown before someone is asked to hand them over. */
export function PrivacySection() {
  return (
    <section id="your-information" className="border-b" style={{ background: "var(--background)", borderColor: "var(--border)" }}>
      <div className="section__inner--wide section">
        <div className="grid gap-8 lg:grid-cols-[320px_1fr]">
          <div>
            <p className="text-[14px] font-semibold tracking-[0.12em] uppercase mb-3" style={{ color: "var(--primary)" }}>Your information</p>
            <h2 className="font-[family-name:var(--font-display)]" style={{ color: "var(--foreground)", fontSize: "1.6rem" }}>Who can see what you share</h2>
          </div>
          <DataPromise />
        </div>
      </div>
    </section>
  );
}
