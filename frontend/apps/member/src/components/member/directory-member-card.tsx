"use client";

import { Badge, UserAvatar, Linkedin, ensureAbsoluteUrl, cn } from "@alumni/ui";
import type { Member } from "@/types";

export type DirectoryMember = Pick<Member, "id" | "firstName" | "lastName" | "profilePictureUrl" | "jobTitle" | "company" | "location" | "graduationYear" | "departmentName" | "linkedInUrl">;

export function DirectoryMemberCard({ member: m, selected = false, isCommunity = false, onSelect }: {
  member: DirectoryMember; selected?: boolean; isCommunity?: boolean; onSelect: () => void;
}) {
  return (
    <div className={cn("rounded-2xl border p-5 transition-shadow hover:shadow-md", selected && "ring-2 ring-primary/30")}
      style={{ borderColor: selected ? "var(--primary)" : "var(--border)", background: "var(--background)" }}>
      <button type="button" onClick={onSelect} aria-pressed={selected}
        className="block w-full text-left rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary mb-4">
        <div className="flex items-start gap-3">
          <UserAvatar src={m.profilePictureUrl} name={`${m.firstName} ${m.lastName}`} size="lg" />
          <div className="flex-1 min-w-0">
            <p className="text-[14.5px] font-semibold leading-snug truncate text-foreground">{m.firstName} {m.lastName}</p>
            {m.jobTitle && <p className="text-[12.5px] mt-0.5 truncate text-muted-foreground">{m.jobTitle}{m.company ? ` · ${m.company}` : ""}</p>}
            {m.location && <p className="text-[12px] mt-0.5 truncate text-muted-foreground">{m.location}</p>}
          </div>
        </div>
      </button>
      <div className="flex flex-wrap items-center gap-1.5 pt-3 border-t border-border">
        {!isCommunity && <Badge variant="secondary" className="text-[10.5px] font-semibold">Class of {m.graduationYear}</Badge>}
        {m.departmentName && <Badge variant="outline" className="text-[10.5px] font-semibold truncate max-w-[110px]">{m.departmentName}</Badge>}
        {m.linkedInUrl && <a href={ensureAbsoluteUrl(m.linkedInUrl)} target="_blank" rel="noopener noreferrer"
          className="ml-auto w-7 h-7 rounded-lg flex items-center justify-center text-primary hover:bg-primary/10"
          aria-label={`${m.firstName} ${m.lastName} on LinkedIn`}><Linkedin size={14} /></a>}
      </div>
    </div>
  );
}
