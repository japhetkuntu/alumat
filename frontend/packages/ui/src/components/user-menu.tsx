"use client";

import * as React from "react";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from "./dropdown-menu";
import { UserAvatar } from "./user-avatar";
import { ChevronDown, LogOut, type IconType } from "./icons";
import { cn } from "../lib/utils";

export interface UserMenuLink {
  label: string;
  icon: IconType;
  /** Called when chosen. Each app passes its router so the menu has no framework dependency. */
  onSelect: () => void;
}

/**
 * The signed-in person at the top right of every portal: their avatar (with name and role on wide screens) opens a menu
 * to reach their profile and settings or to sign out. Sign out lives here so it is always one click away, on any page.
 */
export function UserMenu({ name, email, role, avatarUrl, links, onLogout, showName = false, className }: {
  name: string;
  email?: string | null;
  role?: string | null;
  avatarUrl?: string | null;
  links: UserMenuLink[];
  onLogout: () => void;
  /** Show the name and role beside the avatar. Used in the wide desktop header. */
  showName?: boolean;
  className?: string;
}) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label={`Account menu for ${name}`}
          className={cn(
            "group flex items-center gap-2.5 rounded-full py-1 pl-1 outline-none transition-colors hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring data-[state=open]:bg-muted",
            showName ? "pr-2.5" : "pr-1",
            className,
          )}
        >
          <UserAvatar src={avatarUrl} name={name} size="sm" />
          {showName && (
            <span className="min-w-0 text-left leading-tight">
              <span className="block max-w-[160px] truncate text-[13px] font-semibold">{name}</span>
              {role && <span className="block max-w-[160px] truncate text-[12px] text-muted-foreground">{role}</span>}
            </span>
          )}
          {showName && <ChevronDown size={12} className="shrink-0 text-muted-foreground transition-transform group-data-[state=open]:rotate-180" />}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-[260px]">
        <DropdownMenuLabel className="flex items-center gap-3 px-3 py-2.5 font-normal normal-case tracking-normal">
          <UserAvatar src={avatarUrl} name={name} size="default" />
          <span className="min-w-0 flex-1">
            <span className="block truncate text-[14px] font-semibold text-foreground">{name}</span>
            {email && <span className="block truncate text-[12.5px] text-muted-foreground">{email}</span>}
            {role && <span className="mt-0.5 block text-[12px] font-medium uppercase tracking-wider text-muted-foreground">{role}</span>}
          </span>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        {links.map((l) => (
          <DropdownMenuItem key={l.label} onSelect={l.onSelect}>
            <l.icon size={15} />
            {l.label}
          </DropdownMenuItem>
        ))}
        {links.length > 0 && <DropdownMenuSeparator />}
        <DropdownMenuItem destructive onSelect={onLogout}>
          <LogOut size={15} />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
