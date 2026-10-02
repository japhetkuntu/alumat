"use client";

import { useState } from "react";
import { EmptyState, Skeleton } from "@alumni/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { SegmentedControl } from "@alumni/ui";
import { X, Search, Mail, MessageSquare, Bell } from "@alumni/ui";
import {
  getAnnouncements, sendAnnouncement, searchStaffDirectory, getInstitutions,
  type NotificationChannel, type StaffDirectoryEntry,
} from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";
import { PageHeading } from "@/components/platform/page-heading";

type RecipientMode = "all" | "institution" | "specific";

const CHANNEL_OPTIONS: { value: NotificationChannel; label: string; icon: typeof Bell }[] = [
  { value: "InApp", label: "In app", icon: Bell },
  { value: "Email", label: "Email", icon: Mail },
  { value: "Sms", label: "SMS", icon: MessageSquare },
];

function RecipientPicker({
  institutionId, onInstitutionChange, selected, onSelectedChange,
}: {
  institutionId: string;
  onInstitutionChange: (id: string) => void;
  selected: StaffDirectoryEntry[];
  onSelectedChange: (next: StaffDirectoryEntry[]) => void;
}) {
  const [search, setSearch] = useState("");

  const { data: institutions } = useQuery({
    queryKey: ["institutions-for-notification-picker"],
    queryFn: () => getInstitutions({ page: 1, pageSize: 200 }),
  });

  const { data: results = [], isFetching } = useQuery({
    queryKey: ["staff-directory-search", search, institutionId],
    queryFn: () => searchStaffDirectory(search, institutionId || undefined),
    enabled: search.trim().length >= 2 || !!institutionId,
  });

  const selectedIds = new Set(selected.map((s) => s.id));

  function toggle(entry: StaffDirectoryEntry) {
    onSelectedChange(selectedIds.has(entry.id) ? selected.filter((s) => s.id !== entry.id) : [...selected, entry]);
  }

  return (
    <div className="space-y-2.5">
      <select
        value={institutionId}
        onChange={(e) => onInstitutionChange(e.target.value)}
        className="h-10 w-full border border-input bg-background px-3 text-[13.5px]"
      >
        <option value="">Narrow by institution (optional)</option>
        {(institutions?.results ?? []).map((i) => (
          <option key={i.id} value={i.id}>{i.name}</option>
        ))}
      </select>

      <div className="relative">
        <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
        <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search admins by name or email…" className="pl-9" />
      </div>

      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selected.map((s) => (
            <Badge key={s.id} variant="secondary" className="gap-1.5 pr-1">
              {s.firstName} {s.lastName}
              <button type="button" onClick={() => toggle(s)} aria-label={`Remove ${s.firstName}`}>
                <X size={11} />
              </button>
            </Badge>
          ))}
        </div>
      )}

      {(search.trim().length >= 2 || institutionId) && (
        <div className="max-h-56 overflow-y-auto border border-border">
          {isFetching ? (
            <p className="p-3 text-[12.5px] text-muted-foreground">Searching…</p>
          ) : results.length === 0 ? (
            <p className="p-3 text-[12.5px] text-muted-foreground">No admins match.</p>
          ) : (
            results.map((r) => (
              <button
                key={r.id}
                type="button"
                onClick={() => toggle(r)}
                className={`flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-[13px] transition-colors hover:bg-muted ${selectedIds.has(r.id) ? "bg-primary/5" : ""}`}
              >
                <span className="min-w-0">
                  <span className="block truncate font-medium">{r.firstName} {r.lastName} <span className="font-normal text-muted-foreground">· {r.role}</span></span>
                  <span className="block truncate text-[12px] text-muted-foreground">{r.email} · {r.institutionName}</span>
                </span>
                {!r.hasPhone && <span className="shrink-0 text-[12px] text-muted-foreground">no phone</span>}
              </button>
            ))
          )}
        </div>
      )}
    </div>
  );
}

export default function AnnouncementsPage() {
  const queryClient = useQueryClient();
  const { data: items = [], isLoading: itemsLoading } = useQuery({ queryKey: ["announcements"], queryFn: getAnnouncements });

  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [channels, setChannels] = useState<NotificationChannel[]>(["InApp"]);
  const [mode, setMode] = useState<RecipientMode>("all");
  const [institutionId, setInstitutionId] = useState("");
  const [selectedStaff, setSelectedStaff] = useState<StaffDirectoryEntry[]>([]);

  const smsSelectedWithoutPhone = channels.includes("Sms") && mode === "specific"
    ? selectedStaff.filter((s) => !s.hasPhone).length
    : 0;

  const sendMutation = useMutation({
    mutationFn: () => sendAnnouncement({
      title: title.trim(),
      body: body.trim(),
      channels,
      recipientStaffIds: mode === "specific" ? selectedStaff.map((s) => s.id) : undefined,
      institutionId: mode === "institution" ? institutionId : undefined,
    }),
    onSuccess: (result) => {
      const parts = [`${result.totalAdmins} admin${result.totalAdmins === 1 ? "" : "s"}`];
      if (result.channels.includes("Email")) parts.push(`${result.emailSent} emails sent`);
      if (result.channels.includes("Sms")) parts.push(`${result.smsSent} texts sent${result.smsSkippedNoPhone ? `, ${result.smsSkippedNoPhone} skipped (no phone)` : ""}`);
      toast.success(`Sent to ${parts.join(" · ")}.`);
      queryClient.invalidateQueries({ queryKey: ["announcements"] });
      setTitle("");
      setBody("");
      setSelectedStaff([]);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function send() {
    if (!title.trim() || !body.trim()) { toast.error("Add a title and message before sending."); return; }
    if (channels.length === 0) { toast.error("Choose at least one channel."); return; }
    if (mode === "institution" && !institutionId) { toast.error("Choose which institution's admins to notify."); return; }
    if (mode === "specific" && selectedStaff.length === 0) { toast.error("Pick at least one admin to notify."); return; }
    sendMutation.mutate();
  }

  function toggleChannel(c: NotificationChannel) {
    setChannels((prev) => (prev.includes(c) ? prev.filter((x) => x !== c) : [...prev, c]));
  }

  return (
    <div className="p-4 sm:p-7 max-w-[1500px]">
      <PageHeading title="Notifications" description="Send a message to institution admins — in the app, by email, by SMS, or all three." />

      <div className="grid grid-cols-1 lg:grid-cols-[1.1fr_1fr] gap-4">
        <Card>
          <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Compose</p></div>
          <CardContent className="p-5 space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="announcement-title">Title</Label>
              <Input id="announcement-title" value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Scheduled maintenance" maxLength={200} />
            </div>
            <div className="space-y-1.5">
              <Label>Message</Label>
              <Textarea value={body} onChange={(e) => setBody(e.target.value)} rows={5} placeholder="What should they know?" />
            </div>

            <div className="space-y-1.5">
              <Label>Send by</Label>
              <div className="flex flex-wrap gap-2">
                {CHANNEL_OPTIONS.map(({ value, label, icon: Icon }) => {
                  const active = channels.includes(value);
                  return (
                    <button
                      key={value}
                      type="button"
                      aria-pressed={active}
                      onClick={() => toggleChannel(value)}
                      className={`flex items-center gap-1.5 border px-3 py-2 text-[12.5px] font-semibold transition-colors ${active ? "border-primary/30 bg-primary/10 text-primary" : "border-border bg-white text-foreground hover:bg-muted"}`}
                    >
                      <Icon size={13} /> {label}
                    </button>
                  );
                })}
              </div>
              {channels.includes("Sms") && (
                <p className="text-[12px] text-muted-foreground">Only reaches admins who have a phone number on file. Most don&apos;t yet — this shows once you pick recipients.</p>
              )}
            </div>

            <div className="space-y-1.5">
              <Label>Who receives it</Label>
              <SegmentedControl
                label="Recipient group"
                value={mode}
                onChange={(v) => { setMode(v); setSelectedStaff([]); setInstitutionId(""); }}
                options={[
                  { value: "all", label: "Every institution" },
                  { value: "institution", label: "One institution" },
                  { value: "specific", label: "Specific admins" },
                ] as const}
              />
            </div>

            {mode === "institution" && (
              <RecipientPicker institutionId={institutionId} onInstitutionChange={setInstitutionId} selected={[]} onSelectedChange={() => {}} />
            )}
            {mode === "specific" && (
              <RecipientPicker institutionId={institutionId} onInstitutionChange={setInstitutionId} selected={selectedStaff} onSelectedChange={setSelectedStaff} />
            )}
            {smsSelectedWithoutPhone > 0 && (
              <p className="text-[12px] text-warning">{smsSelectedWithoutPhone} of the selected admins have no phone number, so they won&apos;t get the SMS.</p>
            )}

            <Button onClick={send} className="w-full" disabled={sendMutation.isPending} isLoading={sendMutation.isPending} loadingText="Sending…">
              Send
            </Button>
          </CardContent>
        </Card>

        <Card>
          <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">History</p></div>
          <CardContent className="p-0">
            {itemsLoading && Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="px-5 py-4 border-b border-border last:border-0 space-y-2">
                <Skeleton className="h-4 w-2/5" variant="text" />
                <Skeleton className="h-3 w-full" variant="text" />
                <Skeleton className="h-3 w-1/3" variant="text" />
              </div>
            ))}
            {!itemsLoading && items.length === 0 && (
              <EmptyState
                className="py-10"
                title="Reach institution admins directly"
                description="Use this for a maintenance window, a new feature, a policy change, or to follow up with one admin. Choose in-app, email, SMS, or any combination."
                action={<Button size="sm" className="font-semibold" onClick={() => document.getElementById("announcement-title")?.focus()}>Write a notification</Button>}
              />
            )}
            {!itemsLoading && items.map((a) => (
              <div key={a.id} className="px-5 py-4 border-b border-border last:border-0">
                <p className="font-semibold text-[13.5px]">{a.title}</p>
                <p className="text-[13px] text-muted-foreground mt-1">{a.body}</p>
                <div className="mt-2 flex flex-wrap items-center gap-1.5">
                  {a.channels.map((c) => <Badge key={c} variant="outline" className="text-[12px]">{c === "InApp" ? "In app" : c}</Badge>)}
                </div>
                <p className="text-[12px] text-muted-foreground mt-2">
                  {a.audience} &middot; {new Date(a.sentAt).toLocaleString()} &middot; {a.totalAdmins} admin{a.totalAdmins === 1 ? "" : "s"}
                  {a.channels.includes("Email") && ` · ${a.emailSent} emails sent`}
                  {a.channels.includes("Sms") && ` · ${a.smsSent} texts sent${a.smsSkippedNoPhone ? `, ${a.smsSkippedNoPhone} skipped` : ""}`}
                </p>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
