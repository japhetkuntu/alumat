"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Copy } from "@alumni/ui";
import { Button, Card, CardContent, CardSkeleton, ConfirmModal, Input, Label, LoadError, Textarea } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import {
  createIntegrationKey, getInstitutionIntegration, revokeIntegrationKey, saveInstitutionIntegration,
  type IntegrationKey, type IntegrationSettingsBody,
} from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

const API_PLACEHOLDER = "https://<institution-admin-portal>";
const SAMPLE_ID = "STU-12345";

const RESPONSES = [
  { title: "Already a member (any status)", body: `{\n  "message": "Member exists.",\n  "code": 200,\n  "data": { "exists": true }\n}` },
  { title: "Not a member yet", body: `{\n  "message": "Member does not exist. Send the student to registrationUrl to register.",\n  "code": 200,\n  "data": {\n    "exists": false,\n    "registrationUrl": "https://<member-portal>/register?ob=<one-use token>",\n    "linkExpiresAt": "2026-10-26T17:35:39Z"\n  }\n}` },
  { title: "Errors", body: `401  missing, wrong or revoked key\n400  memberId missing or longer than 64 characters\n403  integration switched off for this institution\n429  too many requests (about 600 a minute per key)` },
];

function copy(text: string, done: string) {
  navigator.clipboard.writeText(text).then(() => toast.success(done), () => toast.error("Could not copy. Select the text and copy it by hand."));
}

const EMPTY: IntegrationSettingsBody = { enabled: false, portalName: "", redirectUrl: "", bannerMessage: "", bannerHours: 72, linkExpiryHours: 72, statusCacheDays: 30 };

/** A number field that keeps what is typed and only commits a whole number, so clearing the box to retype never jumps to 0. */
function NumberField({ label, hint, value, onChange, min, max, unit }: { label: string; hint: string; value: number; onChange: (n: number) => void; min: number; max: number; unit: string }) {
  const [text, setText] = useState(String(value));
  useEffect(() => setText(String(value)), [value]);
  return (
    <div className="space-y-1.5">
      <Label className="text-[12.5px]">{label}</Label>
      <div className="flex items-center gap-2">
        <Input
          type="number" inputMode="numeric" min={min} max={max} className="w-28" value={text}
          onChange={(e) => { setText(e.target.value); const n = Number(e.target.value); if (Number.isInteger(n)) onChange(n); }}
        />
        <span className="text-[13px] text-muted-foreground">{unit}</span>
      </div>
      <p className="text-[12px] text-muted-foreground">{hint}</p>
    </div>
  );
}

/**
 * Partner onboarding for one institution: a partner portal asks whether a student ID is already a member and, if not, is given a
 * one-use registration link. Platform SuperAdmins set the link lifetime, the banner and where members are sent back to here, and
 * manage the API keys the partner's server calls with.
 */
export function InstitutionIntegrationsTab({ institutionId, apiBaseUrl }: { institutionId: string; apiBaseUrl?: string }) {
  const qc = useQueryClient();
  const query = useQuery({ queryKey: ["institution-integration", institutionId], queryFn: () => getInstitutionIntegration(institutionId) });
  const [form, setForm] = useState<IntegrationSettingsBody>(EMPTY);
  const [loaded, setLoaded] = useState(false);
  const [newKeyName, setNewKeyName] = useState("");
  const [shownKey, setShownKey] = useState<string | null>(null);
  const [revoking, setRevoking] = useState<IntegrationKey | null>(null);

  useEffect(() => {
    if (query.data && !loaded) {
      const { keys: _k, linksIssued: _i, linksUsed: _u, ...settings } = query.data;
      setForm(settings);
      setLoaded(true);
    }
  }, [query.data, loaded]);

  const refresh = () => qc.invalidateQueries({ queryKey: ["institution-integration", institutionId] });
  const save = useMutation({
    mutationFn: () => saveInstitutionIntegration(institutionId, form),
    onSuccess: () => { toast.success("Integration saved"); refresh(); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const createKey = useMutation({
    mutationFn: () => createIntegrationKey(institutionId, newKeyName),
    onSuccess: (r) => { setShownKey(r.apiKey); setNewKeyName(""); refresh(); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const revoke = useMutation({
    mutationFn: (keyId: string) => revokeIntegrationKey(institutionId, keyId),
    onSuccess: () => { toast.success("Key revoked"); setRevoking(null); refresh(); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (query.isLoading) return <CardSkeleton />;
  if (query.isError || !query.data) return <LoadError title="Could not load the integration" onRetry={() => query.refetch()} />;

  const set = <K extends keyof IntegrationSettingsBody>(k: K, v: IntegrationSettingsBody[K]) => setForm((f) => ({ ...f, [k]: v }));
  const keys = query.data.keys;
  const base = (apiBaseUrl || API_PLACEHOLDER).replace(/\/$/, "");
  const apiKeyValue = shownKey ?? "YOUR_API_KEY";
  const curl = `curl -G "${base}/api/v1/integrations/members/status" \\\n  --data-urlencode "memberId=${SAMPLE_ID}" \\\n  -H "X-Api-Key: ${apiKeyValue}"`;
  const guide = [
    "AlumUnion: check whether a student is already a member",
    "",
    "Call this from your server, never from a browser, so the key stays secret.",
    "",
    curl,
    "",
    ...RESPONSES.flatMap((r) => [r.title + ":", r.body, ""]),
    "Send the student to registrationUrl as it is. The link works once, for that student ID only, until linkExpiresAt. Asking again for the same ID gives a new link and cancels the old one.",
  ].join("\n");

  return (
    <div className="max-w-[1100px] space-y-5">
      <Card>
        <div className="border-b border-border px-5 py-4">
          <p className="text-[14px] font-semibold">Partner onboarding</p>
          <p className="mt-0.5 text-[12px] text-muted-foreground">
            For a school that off-boards its students on its own portal. Its server asks us whether a student ID is already a member. If not, we answer with a
            one-use registration link for that student, who is approved on registering and sees a banner back to the partner portal for a while.
          </p>
        </div>
        <CardContent className="space-y-5 p-5">
          <label className="flex cursor-pointer items-start justify-between gap-4 border border-border px-4 py-3">
            <span className="min-w-0">
              <span className="block text-[13.5px] font-semibold">Turn the integration on</span>
              <span className="block text-[12.5px] text-muted-foreground">While off, the status check refuses every call and links already issued stop working.</span>
            </span>
            <input type="checkbox" checked={form.enabled} onChange={(e) => set("enabled", e.target.checked)} className="mt-1 size-4 shrink-0 accent-[var(--primary)]" aria-label="Turn the integration on" />
          </label>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-1.5">
              <Label className="text-[12.5px]">Partner portal name</Label>
              <Input value={form.portalName} maxLength={80} onChange={(e) => set("portalName", e.target.value)} placeholder="e.g. UMaT Off-boarding Portal" />
              <p className="text-[12px] text-muted-foreground">Used on the banner button and on an expired link&apos;s page.</p>
            </div>
            <div className="space-y-1.5">
              <Label className="text-[12.5px]">Where members go back to</Label>
              <Input value={form.redirectUrl} maxLength={500} onChange={(e) => set("redirectUrl", e.target.value)} placeholder="https://offboarding.school.edu" inputMode="url" />
              <p className="text-[12px] text-muted-foreground">A full https:// address.</p>
            </div>
          </div>

          <div className="space-y-1.5">
            <Label className="text-[12.5px]">Banner message</Label>
            <Textarea value={form.bannerMessage} maxLength={300} rows={3} onChange={(e) => set("bannerMessage", e.target.value)} placeholder="Welcome to AlumUnion. When you are done looking around, go back to finish your off-boarding." />
            <p className="text-[12px] text-muted-foreground">Plain text, shown at the top of every page of the member portal. {form.bannerMessage.length}/300</p>
          </div>

          <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
            <NumberField label="Registration link lasts" unit="hours" min={1} max={720} value={form.linkExpiryHours} onChange={(n) => set("linkExpiryHours", n)} hint="How long a student has to use a link after it is issued." />
            <NumberField label="Banner stays up" unit="hours" min={1} max={720} value={form.bannerHours} onChange={(n) => set("bannerHours", n)} hint="From the moment they register. It goes away by itself." />
            <NumberField label="Remember existing students for" unit="days" min={1} max={365} value={form.statusCacheDays} onChange={(n) => set("statusCacheDays", n)} hint="So repeat checks for the same ID do not reach the database." />
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-[12.5px] text-muted-foreground">
              {query.data.linksIssued} link{query.data.linksIssued === 1 ? "" : "s"} issued, {query.data.linksUsed} used to register.
            </p>
            <Button onClick={() => save.mutate()} isLoading={save.isPending} loadingText="Saving">Save settings</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <div className="border-b border-border px-5 py-4">
          <p className="text-[14px] font-semibold">API keys</p>
          <p className="mt-0.5 text-[12px] text-muted-foreground">
            The partner&apos;s server sends a key in the <code className="font-mono">X-Api-Key</code> header. A key is shown once, when it is made. Give each system its own key so one can be revoked on its own.
          </p>
        </div>
        <CardContent className="space-y-4 p-5">
          {shownKey && (
            <div className="space-y-2 border-l-2 border-primary bg-muted/40 py-3 pl-4 pr-3" role="status">
              <p className="text-[13px] font-semibold">Copy this key now. It will not be shown again.</p>
              <div className="flex flex-wrap items-center gap-2">
                <code className="min-w-0 flex-1 break-all font-mono text-[12.5px]">{shownKey}</code>
                <Button variant="outline" size="sm" onClick={() => { navigator.clipboard.writeText(shownKey).then(() => toast.success("Key copied"), () => toast.error("Could not copy. Select it and copy by hand.")); }}>
                  <Copy size={13} className="mr-1.5" /> Copy
                </Button>
                <Button variant="ghost" size="sm" onClick={() => setShownKey(null)}>Done</Button>
              </div>
            </div>
          )}

          <form className="flex flex-wrap items-end gap-2" onSubmit={(e) => { e.preventDefault(); if (newKeyName.trim()) createKey.mutate(); }}>
            <div className="min-w-[220px] flex-1 space-y-1.5">
              <Label className="text-[12.5px]">New key for</Label>
              <Input value={newKeyName} maxLength={80} onChange={(e) => setNewKeyName(e.target.value)} placeholder="e.g. Off-boarding portal server" />
            </div>
            <Button type="submit" disabled={!newKeyName.trim()} isLoading={createKey.isPending} loadingText="Creating">Create key</Button>
          </form>

          {keys.length === 0 ? (
            <p className="border border-dashed border-border p-3 text-[13px] text-muted-foreground">No keys yet.</p>
          ) : (
            <ul className="divide-y divide-border border border-border">
              {keys.map((k) => (
                <li key={k.id} className="flex flex-wrap items-center gap-3 px-4 py-3 text-[13px]">
                  <div className="min-w-0 flex-1">
                    <p className="font-semibold">{k.name}{k.revokedAt && <span className="ml-2 font-normal text-destructive">Revoked</span>}</p>
                    <p className="text-[12px] text-muted-foreground">
                      <span className="font-mono">{k.keyPrefix}…</span> · made {formatDate(k.createdAt)} · {k.lastUsedAt ? `last used ${formatDate(k.lastUsedAt)}` : "never used"}
                    </p>
                  </div>
                  {!k.revokedAt && <Button variant="outline" size="sm" className="text-destructive" onClick={() => setRevoking(k)}>Revoke</Button>}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Card>
        <div className="border-b border-border px-5 py-4">
          <p className="text-[14px] font-semibold">Share with the partner</p>
          <p className="mt-0.5 text-[12px] text-muted-foreground">
            Copy the request and send it to their developers. {shownKey ? "The key you just made is filled in below. Remove it before sharing anywhere public." : "Create a key above and its value is filled in here; until then it shows YOUR_API_KEY."}
          </p>
        </div>
        <CardContent className="space-y-4 p-5 text-[13px]">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="font-semibold">Request</p>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" onClick={() => copy(curl, "curl copied")}><Copy size={13} className="mr-1.5" /> Copy curl</Button>
              <Button variant="outline" size="sm" onClick={() => copy(guide, "Instructions copied")}><Copy size={13} className="mr-1.5" /> Copy full instructions</Button>
            </div>
          </div>
          <pre className="overflow-x-auto whitespace-pre bg-muted/50 p-3 font-mono text-[12.5px] leading-relaxed">{curl}</pre>
          {!apiBaseUrl && <p className="text-[12px] text-destructive">This server has no admin portal address configured (AdminBaseDomain), so replace {API_PLACEHOLDER} with the institution&apos;s admin portal address.</p>}

          <div className="space-y-3">
            {RESPONSES.map((r) => (
              <div key={r.title} className="space-y-1">
                <p className="text-[12.5px] font-semibold">{r.title}</p>
                <pre className="overflow-x-auto whitespace-pre bg-muted/50 p-3 font-mono text-[12px] leading-relaxed">{r.body}</pre>
              </div>
            ))}
          </div>
          <p className="text-muted-foreground">
            Send the link in <code className="font-mono">registrationUrl</code> to the student as it is. It works once, for that student ID only, until <code className="font-mono">linkExpiresAt</code>.
            Asking again for the same ID gives a new link and cancels the old one.
          </p>
        </CardContent>
      </Card>

      <ConfirmModal
        open={!!revoking}
        title={`Revoke "${revoking?.name ?? ""}"?`}
        message="The partner's server stops working with this key straight away. This cannot be undone, but you can make a new key."
        confirmLabel="Revoke key"
        isLoading={revoke.isPending}
        onConfirm={() => revoking && revoke.mutate(revoking.id)}
        onCancel={() => setRevoking(null)}
      />
    </div>
  );
}
