"use client";

import { CheckOption } from "@alumni/ui";

/**
 * Lets an admin show one record in full to people who are not signed in. Off by default: guests then see only a short
 * preview. Only items for everyone qualify, since anything meant for a group or year group is for those members.
 */
export function PublicOption({ id, checked, onChange, forEveryone, noun }: {
  id: string;
  checked: boolean;
  onChange: (v: boolean) => void;
  /** True when the audience is the whole community. */
  forEveryone: boolean;
  /** What is being shown: "story", "event", "posting"… */
  noun: string;
}) {
  if (!forEveryone) {
    return (
      <p className="border border-dashed border-border p-3 text-[13px] leading-relaxed text-muted-foreground">
        Public website: this {noun} is for a specific audience, so only members who are meant to see it can. To show it to everyone, change who it is for.
      </p>
    );
  }
  return (
    <CheckOption
      id={id}
      checked={checked}
      onChange={onChange}
      label="Show in full on the public website"
      hint={`Anyone can read the whole ${noun} without signing in. Otherwise visitors see a short preview. Signing in is still needed to act on it.`}
    />
  );
}
