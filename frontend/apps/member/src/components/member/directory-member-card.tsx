"use client";

import { useCohortPhrase } from "@/components/member/member-layout";
import { Badge, UserAvatar, Linkedin, MessageCircle, ensureAbsoluteUrl, cn } from "@alumni/ui";
import type { Member } from "@/types";

export type DirectoryMember = Pick<Member, "id" | "firstName" | "lastName" | "profilePictureUrl" | "jobTitle" | "company" | "location" | "graduationYear" | "departmentName" | "linkedInUrl">;

export function DirectoryMemberCard({ member: m, selected = false, isCommunity = false, canConnect = false, titles = [], onSelect }: {
  member: DirectoryMember; selected?: boolean; isCommunity?: boolean;
  /** Leadership titles this member holds, shown under their name. */
  titles?: string[];
  /** Shows a Connect button, so it is plain that a member can be asked for an introduction. It opens the profile, where the request is made. */
  canConnect?: boolean; onSelect: () => void;
}) {
  const cohort = useCohortPhrase();
  return (
    <div className={cn("rounded-2xl border p-5 transition-shadow hover:shadow-md", selected && "ring-2 ring-primary/30")}
      style={{ borderColor: selected ? "var(--primary)" : "var(--border)", background: "var(--background)" }}>
      <button type="button" onClick={onSelect} aria-pressed={selected}
        className="block w-full text-left rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary mb-4">
        <div className="flex items-start gap-3">
          <UserAvatar src={m.profilePictureUrl} name={`${m.firstName} ${m.lastName}`} size="lg" />
          <div className="flex-1 min-w-0">
            <p className="text-[14.5px] font-semibold leading-snug truncate text-foreground">{m.firstName} {m.lastName}</p>
            {titles.length > 0 && <p className="text-[12px] mt-0.5 truncate font-medium text-primary">{titles.join(" · ")}</p>}
            {m.jobTitle && <p className="text-[12.5px] mt-0.5 truncate text-muted-foreground">{m.jobTitle}{m.company ? ` · ${m.company}` : ""}</p>}
            {m.location && <p className="text-[12px] mt-0.5 truncate text-muted-foreground">{m.location}</p>}
          </div>
        </div>
      </button>
      <div className="flex flex-wrap items-center gap-1.5 pt-3 border-t border-border">
        {!isCommunity && m.graduationYear > 0 && <Badge variant="secondary" className="text-[12px] font-semibold">{cohort.of(m.graduationYear)}</Badge>}
        {m.departmentName && <Badge variant="outline" className="text-[12px] font-semibold truncate max-w-[110px]">{m.departmentName}</Badge>}
        {canConnect && (
          <button type="button" onClick={onSelect}
            className="ml-auto inline-flex items-center gap-1.5 rounded-full border border-primary/30 px-3 py-1 text-[12.5px] font-semibold text-primary transition-colors hover:bg-primary/10">
            <MessageCircle size={12} /> Connect
          </button>
        )}
        {m.linkedInUrl && <a href={ensureAbsoluteUrl(m.linkedInUrl)} target="_blank" rel="noopener noreferrer"
          className={cn("w-7 h-7 rounded-lg flex items-center justify-center text-primary hover:bg-primary/10", !canConnect && "ml-auto")}
          aria-label={`${m.firstName} ${m.lastName} on LinkedIn`}><Linkedin size={14} /></a>}
      </div>
    </div>
  );
}
