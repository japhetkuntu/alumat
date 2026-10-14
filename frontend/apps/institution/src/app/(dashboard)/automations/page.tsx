"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { getAutomations, getInstitutionProfile, saveAutomations, type AutomationSettings } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";

function Switch({ on, onChange, label }: { on: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={on}
      aria-label={label}
      onClick={() => onChange(!on)}
      className={cn("relative inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors", on ? "bg-primary" : "bg-muted")}
    >
      <span className={cn("pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow transition-transform", on ? "translate-x-5" : "translate-x-0")} />
    </button>
  );
}

function Days({ id, label, value, min, max, onChange, suffix = "days" }: {
  id: string; label: string; value: number; min: number; max: number; suffix?: string; onChange: (v: number) => void;
}) {
  // The sentence reads top to bottom (what, then the number and its unit), so it never breaks mid-phrase on a phone.
  return (
    <div className="mt-4 space-y-1.5">
      <Label htmlFor={id} className="block text-[13px] font-normal text-muted-foreground">{label}</Label>
      <div className="flex flex-wrap items-center gap-2">
        <Input id={id} type="number" min={min} max={max} value={value} onChange={(e) => onChange(Number(e.target.value))} className="h-10 w-24" />
        <span className="text-[13px] text-foreground">{suffix}</span>
        <span className="text-[12px] text-muted-foreground">(from {min} to {max})</span>
      </div>
    </div>
  );
}

function Rule({ title, description, on, onToggle, children }: {
  title: string; description: string; on: boolean; onToggle: (v: boolean) => void; children?: React.ReactNode;
}) {
  return (
    <li className="border border-border bg-card p-4 sm:p-5">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <p className="text-[15px] font-semibold">{title}</p>
          <p className="mt-1 text-[13px] leading-relaxed text-muted-foreground">{description}</p>
        </div>
        <Switch on={on} onChange={onToggle} label={title} />
      </div>
      {on && children}
    </li>
  );
}

export default function AutomationsPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const qc = useQueryClient();

  const saved = useQuery({ queryKey: ["automations"], queryFn: getAutomations, enabled: isSuperAdmin });
  const profile = useQuery({ queryKey: ["institution-profile"], queryFn: getInstitutionProfile, enabled: isSuperAdmin, staleTime: 60_000 });
  const [draft, setDraft] = useState<AutomationSettings | null>(null);
  useEffect(() => { if (saved.data) setDraft(saved.data); }, [saved.data]);

  const save = useMutation({
    mutationFn: () => saveAutomations(draft!),
    onSuccess: (next) => { toast.success("Automatic messages updated"); setDraft(next); qc.setQueryData(["automations"], next); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (!isSuperAdmin) {
    return <div className="p-4 sm:p-6"><EmptyState title="Automatic messages are managed by full administrators" description="Ask a full administrator to change these." /></div>;
  }
  if (!draft) return <div className="p-4 sm:p-6 text-[13px] text-muted-foreground">Loading…</div>;

  const set = (patch: Partial<AutomationSettings>) => setDraft({ ...draft, ...patch });
  const dirty = JSON.stringify(draft) !== JSON.stringify(saved.data);
  const emailOff = profile.data && profile.data.emailNotificationsEnabled === false;
  const disabled = profile.data?.disabledFeatures ?? [];

  return (
    <div className="mx-auto max-w-[900px] space-y-5 p-4 sm:p-[26px]">
      <div>
        <h1 className="m-0 text-[20px] font-bold sm:text-[25px]">Automatic messages</h1>
        <p className="mt-1.5 text-[13px] text-muted-foreground">
          Messages your community sends on its own. They go out in the daytime only, never more than one engagement message to the same person within a week,
          and members can opt out of each kind in their notification settings.
        </p>
      </div>

      {emailOff && (
        <p role="status" className="border border-warning/40 bg-warning/10 p-3 text-[13px]">
          Email notifications are switched off for your community, so these messages are paused whatever is set here. Turn email back on in Settings, under Notifications.
        </p>
      )}

      <ul className="space-y-3">
        {!disabled.includes("Contributions") && (
          <Rule
            title="Remind members about unpaid dues"
            description="Members who owe membership dues get a reminder with a link to pay. People who have turned dues reminders off are never contacted."
            on={draft.duesReminders}
            onToggle={(v) => set({ duesReminders: v })}
          >
            <Days id="dues-every" label="Remind the same member no more often than once every" value={draft.duesReminderEveryDays} min={7} max={90} onChange={(v) => set({ duesReminderEveryDays: v })} />
            <Days id="dues-after" label="Wait before the first reminder to a new member" value={draft.duesChaseAfterDays} min={0} max={120} suffix="days after they join" onChange={(v) => set({ duesChaseAfterDays: v })} />
          </Rule>
        )}

        {!disabled.includes("Contributions") && (
          <Rule
            title="Remind members about their pledges"
            description="A reminder shortly before a pledge falls due, and a follow-up when it is overdue."
            on={draft.pledgeReminders}
            onToggle={(v) => set({ pledgeReminders: v })}
          />
        )}

        {!disabled.includes("Events") && (
          <Rule
            title="Remind people the day before an event"
            description="Everyone who said they are going gets a short reminder the day before."
            on={draft.eventReminders}
            onToggle={(v) => set({ eventReminders: v })}
          />
        )}

        <Rule
          title="Invite quiet members back"
          description="A friendly note, with what is coming up and what they missed, to members who have not signed in or taken part for a while. Never sent when there is nothing new to show."
          on={draft.reengageQuietMembers}
          onToggle={(v) => set({ reengageQuietMembers: v })}
        >
          <Days id="quiet" label="Treat a member as quiet after they have been away for" value={draft.quietDays} min={14} max={180} onChange={(v) => set({ quietDays: v })} />
        </Rule>
      </ul>

      <p className="text-[12.5px] text-muted-foreground">
        A welcome message when a member is approved is already sent as part of approval, so there is no separate switch for it.
      </p>

      <div className="flex gap-2">
        <Button isLoading={save.isPending} disabled={!dirty} onClick={() => save.mutate()}>Save changes</Button>
        {dirty && <Button variant="outline" onClick={() => saved.data && setDraft(saved.data)}>Discard</Button>}
      </div>
    </div>
  );
}
