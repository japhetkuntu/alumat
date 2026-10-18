"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Button, Card, CardContent, ConfirmModal, Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle, FormError, Input, Label, ShareLinkButton,
} from "@alumni/ui";
import {
  confirmWhatsAppGroup, createWhatsAppGroup, deleteWhatsAppGroup, getCommunities, getWhatsAppAdoption, getWhatsAppOverview, updateWhatsAppGroup,
  type WhatsAppGroup,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";

const KEY = ["whatsapp-overview"];

const COMMAND_LABELS: Record<string, string> = {
  events: "!events: upcoming events",
  campaigns: "!campaigns: open fundraisers",
  jobs: "!jobs: jobs and opportunities",
  updates: "!updates: community news",
  community: "!community: where to find us",
  help: "!help: the list of commands",
};

const STATUS_TEXT: Record<WhatsAppGroup["status"], string> = {
  AwaitingGroup: "Waiting for the code",
  Detected: "Needs your confirmation",
  Active: "Connected",
};

/**
 * Manage the AlumUnion Assistant for this institution: the number members message, and the WhatsApp groups it has been
 * connected to. A group is connected in two deliberate steps (post a one-time code in it, then confirm it here), so the
 * assistant is never active in a group nobody chose.
 */
export function WhatsAppSettings({ enabled, onToggleEnabled, toggling }: { enabled: boolean; onToggleEnabled: (on: boolean) => void; toggling: boolean }) {
  const qc = useQueryClient();
  const [adding, setAdding] = useState(false);
  const [name, setName] = useState("");
  const [communityId, setCommunityId] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [removing, setRemoving] = useState<WhatsAppGroup | null>(null);
  const [editing, setEditing] = useState<WhatsAppGroup | null>(null);

  const overview = useQuery({ queryKey: KEY, queryFn: getWhatsAppOverview, enabled, refetchInterval: (q) => (q.state.data?.groups.some((g) => g.status === "AwaitingGroup") ? 5000 : false) });
  const communities = useQuery({ queryKey: ["communities"], queryFn: getCommunities, enabled });
  const adoption = useQuery({ queryKey: ["whatsapp-adoption"], queryFn: getWhatsAppAdoption, enabled, staleTime: 60_000, retry: false });
  const refresh = () => qc.invalidateQueries({ queryKey: KEY });

  const create = useMutation({
    mutationFn: () => createWhatsAppGroup({ name: name.trim(), communityId: communityId || undefined }),
    onSuccess: () => { setAdding(false); setName(""); setCommunityId(""); setFormError(null); refresh(); },
    onError: (e) => setFormError(handleApiError(e)),
  });
  const confirm = useMutation({
    mutationFn: (id: string) => confirmWhatsAppGroup(id),
    onSuccess: () => { toast.success("Group connected"); refresh(); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const remove = useMutation({
    mutationFn: (id: string) => deleteWhatsAppGroup(id),
    onSuccess: () => { toast.success("Group disconnected"); setRemoving(null); refresh(); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const o = overview.data;

  return (
    <div className="max-w-3xl space-y-6">
      <Card className="border-border/40">
        <CardContent className="space-y-4 p-6">
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="text-[15px] font-semibold">WhatsApp assistant</p>
              <p className="mt-0.5 text-[13px] text-muted-foreground">
                Members message one AlumUnion number to ask about dues, events, jobs and updates. It only shows what a member is allowed to see in the portal.
              </p>
            </div>
            <button
              type="button" role="switch" aria-checked={enabled} aria-label="WhatsApp assistant" disabled={toggling}
              onClick={() => onToggleEnabled(!enabled)}
              className={`relative inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors after:absolute after:-inset-3 after:content-[''] ${enabled ? "bg-primary" : "bg-muted"} ${toggling ? "opacity-50" : ""}`}
            >
              <span className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow transition-transform ${enabled ? "translate-x-5" : "translate-x-0"}`} />
            </button>
          </div>

          {enabled && o?.assistantLink && (
            <div className="flex flex-wrap items-center gap-3 border-t border-border/50 pt-4">
              <div className="min-w-0 basis-full sm:basis-0 sm:flex-1">
                <p className="text-[12.5px] font-semibold text-muted-foreground">Your assistant link</p>
                <p className="break-all text-[13px]">{o.assistantLink}</p>
              </div>
              <ShareLinkButton url={o.assistantLink} title="Chat with the assistant" message="Ask the AlumUnion assistant about events, jobs and fundraisers on WhatsApp." variant="outline" size="sm" />
            </div>
          )}
        </CardContent>
      </Card>

      {enabled && adoption.data && (
        <section aria-labelledby="wa-adoption" className="space-y-3">
          <h2 id="wa-adoption" className="text-[15px] font-semibold">How members are using it</h2>
          <Card className="border-border/40">
            <CardContent className="space-y-5 p-5">
              <dl className="grid grid-cols-2 gap-x-6 gap-y-4 sm:grid-cols-4">
                <div><dt className="text-[12.5px] text-muted-foreground">Connected</dt><dd className="text-[22px] font-bold tabular-nums">{adoption.data.connected}<span className="text-[13px] font-normal text-muted-foreground"> of {adoption.data.activeMembers}</span></dd></div>
                <div><dt className="text-[12.5px] text-muted-foreground">Alerts on WhatsApp</dt><dd className="text-[22px] font-bold tabular-nums">{adoption.data.alertsOn}</dd></div>
                <div><dt className="text-[12.5px] text-muted-foreground">Connected this month</dt><dd className="text-[22px] font-bold tabular-nums">{adoption.data.connectedThisMonth}</dd></div>
                <div><dt className="text-[12.5px] text-muted-foreground">Share of members</dt><dd className="text-[22px] font-bold tabular-nums">{adoption.data.activeMembers > 0 ? Math.round((adoption.data.connected / adoption.data.activeMembers) * 100) : 0}%</dd></div>
              </dl>

              <div className="overflow-x-auto">
                <table className="w-full min-w-[420px] text-[13.5px]">
                  <caption className="pb-2 text-left text-[12.5px] font-semibold text-muted-foreground">Alerts delivered, by month</caption>
                  <thead><tr className="border-b border-border text-left text-[12px] text-muted-foreground">
                    <th scope="col" className="py-1.5 pr-3 font-semibold">Month</th>
                    <th scope="col" className="py-1.5 pr-3 text-right font-semibold">By WhatsApp</th>
                    <th scope="col" className="py-1.5 pr-3 text-right font-semibold">By SMS</th>
                    <th scope="col" className="py-1.5 text-right font-semibold" title="SMS sent only because the WhatsApp message did not deliver">SMS fallback</th>
                  </tr></thead>
                  <tbody>
                    {[...adoption.data.months].reverse().map((m) => (
                      <tr key={m.month} className="border-b border-border/50 last:border-0">
                        <th scope="row" className="py-1.5 pr-3 text-left font-normal">{formatMonth(m.month)}</th>
                        <td className="py-1.5 pr-3 text-right tabular-nums">{m.byWhatsApp}</td>
                        <td className="py-1.5 pr-3 text-right tabular-nums">{m.bySms}</td>
                        <td className="py-1.5 text-right tabular-nums">{m.smsAfterWhatsAppFailure}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {adoption.data.referralsLast90Days.length > 0 && (
                <div>
                  <p className="text-[12.5px] font-semibold text-muted-foreground">Invitations in the last 90 days, by where they were shared</p>
                  <ul className="mt-2 divide-y divide-border/50 text-[13.5px]">
                    {adoption.data.referralsLast90Days.map((c) => (
                      <li key={c.channel} className="flex items-center justify-between gap-3 py-1.5">
                        <span className="capitalize">{c.channel}</span>
                        <span className="tabular-nums text-muted-foreground">{c.invited} invited · {c.registered} joined</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </CardContent>
          </Card>
        </section>
      )}

      {enabled && (
        <section aria-labelledby="wa-groups" className="space-y-3">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 id="wa-groups" className="text-[15px] font-semibold">WhatsApp groups</h2>
              <p className="text-[13px] text-muted-foreground">Connect a group so it can use !events, !jobs and the rest, and so you can post to it from here.</p>
            </div>
            <Button size="sm" onClick={() => setAdding(true)}>Connect a group</Button>
          </div>

          {overview.isLoading && <div className="skeleton h-24 w-full" aria-busy="true" />}

          {o && o.groups.length === 0 && (
            <Card className="border-border/40"><CardContent className="p-5 text-[13.5px] text-muted-foreground">
              No groups yet. Connecting one takes a minute: you get a code, post it in the group, then confirm it here.
            </CardContent></Card>
          )}

          {o?.groups.map((g) => (
            <Card key={g.id} className="border-border/40">
              <CardContent className="space-y-3 p-5">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="break-words text-[14.5px] font-semibold">{g.name}</p>
                    <p className="text-[12.5px] text-muted-foreground">
                      {STATUS_TEXT[g.status]}{g.communityName ? ` · ${g.communityName}` : " · whole institution"}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {g.status === "Detected" && (
                      <Button size="sm" onClick={() => confirm.mutate(g.id)} isLoading={confirm.isPending && confirm.variables === g.id}>Confirm group</Button>
                    )}
                    {g.status === "Active" && <Button size="sm" variant="outline" onClick={() => setEditing(g)}>Settings</Button>}
                    <Button size="sm" variant="outline" onClick={() => setRemoving(g)}>Disconnect</Button>
                  </div>
                </div>

                {g.status === "AwaitingGroup" && g.code && (
                  <ol className="space-y-1.5 border-t border-border/50 pt-3 text-[13.5px]">
                    <li><span className="font-semibold">1.</span> Add the AlumUnion Assistant{o.assistantNumber ? ` (+${o.assistantNumber})` : ""} to the WhatsApp group.</li>
                    <li>
                      <span className="font-semibold">2.</span> Post this message <span className="font-semibold">inside the group chat</span> (not in a private message to the assistant):{" "}
                      <span className="whitespace-nowrap bg-muted px-2 py-0.5 font-mono font-semibold">!link {g.code}</span>
                    </li>
                    <li><span className="font-semibold">3.</span> Come back here and confirm. This page updates by itself.</li>
                    <li className="text-[12.5px] text-muted-foreground">The code works once and expires in 24 hours. Group members will see it, which is fine: it only proposes the group to you.</li>
                  </ol>
                )}
                {g.status === "AwaitingGroup" && !g.code && (
                  <p className="border-t border-border/50 pt-3 text-[13px] text-muted-foreground">This code has expired. Disconnect it and start again.</p>
                )}
                {g.status === "Detected" && (
                  <p className="border-t border-border/50 pt-3 text-[13px]">The assistant heard the code in a group called <span className="font-semibold">{g.name}</span>. If that is the right group, confirm it.</p>
                )}
              </CardContent>
            </Card>
          ))}
        </section>
      )}

      <Dialog open={adding} onOpenChange={(v) => { if (!v) { setAdding(false); setFormError(null); } }}>
        <DialogContent>
          <DialogHeader><DialogTitle>Connect a WhatsApp group</DialogTitle></DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="wa-name">Group name</Label>
              <Input id="wa-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Class of 2015" maxLength={80} />
              <p className="text-xs text-muted-foreground">Just for you. It is replaced by the group&apos;s own name once it is found.</p>
            </div>
            {(communities.data?.length ?? 0) > 0 && (
              <div className="space-y-2">
                <Label htmlFor="wa-community">This is the group for</Label>
                <select id="wa-community" value={communityId} onChange={(e) => setCommunityId(e.target.value)} className="h-11 w-full border border-border bg-background px-3 text-[16px] md:text-[14px]">
                  <option value="">The whole institution</option>
                  {communities.data!.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>
            )}
          </div>
          <FormError message={formError} className="mt-3" />
          <DialogFooter>
            <Button variant="outline" onClick={() => setAdding(false)}>Cancel</Button>
            <Button disabled={name.trim().length < 2} isLoading={create.isPending} loadingText="Creating" onClick={() => create.mutate()}>Get my code</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {editing && o && (
        <GroupSettingsDialog group={editing} commands={o.commands} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); refresh(); }} />
      )}

      <ConfirmModal
        open={!!removing}
        title="Disconnect group"
        message={`The assistant will stop answering in "${removing?.name}" and you won't be able to post to it from here. The group itself is not affected.`}
        confirmLabel="Disconnect"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={() => removing && remove.mutate(removing.id)}
        onCancel={() => setRemoving(null)}
      />
    </div>
  );
}

function formatMonth(key: string): string {
  const [y, m] = key.split("-").map(Number);
  if (!y || !m) return key;
  return new Date(Date.UTC(y, m - 1, 1)).toLocaleDateString("en-GB", { month: "long", year: "numeric", timeZone: "UTC" });
}

function GroupSettingsDialog({ group, commands, onClose, onSaved }: { group: WhatsAppGroup; commands: string[]; onClose: () => void; onSaved: () => void }) {
  const [assistant, setAssistant] = useState(group.assistantEnabled);
  const [publishing, setPublishing] = useState(group.publishingEnabled);
  const [allowed, setAllowed] = useState<string[]>(group.allowedCommands.length === 0 ? commands : group.allowedCommands);
  const [invite, setInvite] = useState(group.inviteLink ?? "");
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: () => updateWhatsAppGroup(group.id, { assistantEnabled: assistant, publishingEnabled: publishing, allowedCommands: allowed, inviteLink: invite.trim() }),
    onSuccess: () => { toast.success("Saved"); onSaved(); },
    onError: (e) => setError(handleApiError(e)),
  });
  const toggle = (c: string) => setAllowed((a) => (a.includes(c) ? a.filter((x) => x !== c) : [...a, c]));

  return (
    <Dialog open onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent>
        <DialogHeader><DialogTitle>{group.name}</DialogTitle></DialogHeader>
        <div className="space-y-5">
          <label className="flex cursor-pointer items-start gap-3">
            <input type="checkbox" className="mt-1 size-5 accent-[var(--primary)]" checked={assistant} onChange={(e) => setAssistant(e.target.checked)} />
            <span><span className="block text-[14px] font-semibold">Answer commands in this group</span><span className="block text-[12.5px] text-muted-foreground">Anyone in the group can use them. Answers are public information only, never anyone&apos;s personal details.</span></span>
          </label>

          <fieldset disabled={!assistant} className="space-y-2 disabled:opacity-50">
            <legend className="text-[13px] font-semibold">Commands it answers</legend>
            {commands.map((c) => (
              <label key={c} className="flex min-h-11 cursor-pointer items-center gap-3">
                <input type="checkbox" className="size-5 accent-[var(--primary)]" checked={allowed.includes(c)} onChange={() => toggle(c)} />
                <span className="text-[13.5px]">{COMMAND_LABELS[c] ?? `!${c}`}</span>
              </label>
            ))}
          </fieldset>

          <label className="flex cursor-pointer items-start gap-3">
            <input type="checkbox" className="mt-1 size-5 accent-[var(--primary)]" checked={publishing} onChange={(e) => setPublishing(e.target.checked)} />
            <span><span className="block text-[14px] font-semibold">Allow posting to this group from the portal</span><span className="block text-[12.5px] text-muted-foreground">Nothing is posted automatically. You see and approve each message first.</span></span>
          </label>

          <div className="space-y-2">
            <Label htmlFor="wa-invite">Invitation link (optional)</Label>
            <Input id="wa-invite" value={invite} onChange={(e) => setInvite(e.target.value)} placeholder="https://chat.whatsapp.com/…" inputMode="url" maxLength={200} />
            <p className="text-xs text-muted-foreground">
              Paste the group&apos;s invite link and members {group.communityName ? `of ${group.communityName}` : "of your community"} see a &ldquo;Join group&rdquo; button in their portal. Leave it empty to keep the group private.
            </p>
          </div>
        </div>
        <FormError message={error} className="mt-3" />
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button disabled={assistant && allowed.length === 0} isLoading={save.isPending} loadingText="Saving" onClick={() => save.mutate()}>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
