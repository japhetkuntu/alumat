"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent, ConfirmModal, LoadError, cn } from "@alumni/ui";
import { getPlatformSettings, getWhatsAppHealth, getWhatsAppInstitutions, setInstitutionWhatsApp, setWhatsAppBotEnabled, type WhatsAppInstitutionRow } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";

function Check({ ok, label, hint }: { ok: boolean; label: string; hint?: string }) {
  return (
    <li className="flex items-start gap-3 py-3">
      <span
        className={cn("mt-1.5 size-2.5 shrink-0 rounded-full", ok ? "bg-success" : "bg-destructive")}
        aria-hidden
      />
      <div className="min-w-0">
        <p className="text-[14px] font-semibold">{label}: <span className={ok ? "text-success" : "text-destructive"}>{ok ? "yes" : "no"}</span></p>
        {hint && !ok && <p className="text-[12.5px] text-muted-foreground">{hint}</p>}
      </div>
    </li>
  );
}

function monthName(key: string) {
  const [y, m] = key.split("-").map(Number);
  return y && m ? new Date(Date.UTC(y, m - 1, 1)).toLocaleDateString("en-GB", { month: "short", year: "numeric", timeZone: "UTC" }) : key;
}

/** The cost case for the channel: alerts that went by WhatsApp instead of SMS. Counts come from the notification worker; money only when an SMS cost is configured. */
function Adoption({ a }: { a: NonNullable<import("@/lib/platform-api").WhatsAppHealth["adoption"]> }) {
  const total = a.months.reduce((s, m) => s + m.byWhatsApp, 0);
  const sms = a.months.reduce((s, m) => s + m.bySms, 0);
  const share = total + sms > 0 ? Math.round((total / (total + sms)) * 100) : 0;
  return (
    <Card className="border-border/40">
      <CardContent className="space-y-5 p-5">
        <div>
          <p className="text-[15px] font-semibold">Alerts by channel, last six months</p>
          <p className="mt-0.5 text-[13px] text-muted-foreground">
            {total} by WhatsApp and {sms} by SMS ({share}% on WhatsApp). {a.alertsOn} members have WhatsApp alerts on.
          </p>
          {a.estimatedSmsSaved != null && a.smsUnitCost != null && (
            <p className="mt-1 text-[13px]">
              Estimated SMS cost avoided: <span className="font-semibold tabular-nums">{a.estimatedSmsSaved.toLocaleString("en-GB", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
              <span className="text-muted-foreground"> ({total} alerts × {a.smsUnitCost} each, an estimate)</span>
            </p>
          )}
        </div>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[360px] text-[13.5px]">
            <thead><tr className="border-b border-border text-left text-[12px] text-muted-foreground">
              <th scope="col" className="py-1.5 pr-3 font-semibold">Month</th>
              <th scope="col" className="py-1.5 pr-3 text-right font-semibold">WhatsApp</th>
              <th scope="col" className="py-1.5 text-right font-semibold">SMS</th>
            </tr></thead>
            <tbody>
              {[...a.months].reverse().map((m) => (
                <tr key={m.month} className="border-b border-border/50 last:border-0">
                  <th scope="row" className="py-1.5 pr-3 text-left font-normal">{monthName(m.month)}</th>
                  <td className="py-1.5 pr-3 text-right tabular-nums">{m.byWhatsApp}</td>
                  <td className="py-1.5 text-right tabular-nums">{m.bySms}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {a.institutions.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[480px] text-[13.5px]">
              <caption className="pb-2 text-left text-[12.5px] font-semibold text-muted-foreground">By institution, this month</caption>
              <thead><tr className="border-b border-border text-left text-[12px] text-muted-foreground">
                <th scope="col" className="py-1.5 pr-3 font-semibold">Institution</th>
                <th scope="col" className="py-1.5 pr-3 text-right font-semibold">Connected</th>
                <th scope="col" className="py-1.5 pr-3 text-right font-semibold">WhatsApp</th>
                <th scope="col" className="py-1.5 text-right font-semibold">SMS</th>
              </tr></thead>
              <tbody>
                {a.institutions.map((i) => (
                  <tr key={i.name} className="border-b border-border/50 last:border-0">
                    <th scope="row" className="py-1.5 pr-3 text-left font-normal">{i.name}</th>
                    <td className="py-1.5 pr-3 text-right tabular-nums">{i.connected} / {i.activeMembers}</td>
                    <td className="py-1.5 pr-3 text-right tabular-nums">{i.byWhatsAppThisMonth}</td>
                    <td className="py-1.5 text-right tabular-nums">{i.bySmsThisMonth}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * The platform-wide switch for the WhatsApp bot: assistant replies, group commands and posting, and WhatsApp notifications, for
 * every institution at once. Off takes effect within seconds with no restart, and notifications go by SMS and email as before.
 */
function BotSwitch() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const [confirmOff, setConfirmOff] = useState(false);
  const settings = useQuery({ queryKey: ["platform-settings"], queryFn: getPlatformSettings });
  const mutation = useMutation({
    mutationFn: (on: boolean) => setWhatsAppBotEnabled(on, settings.data?.blockOverdueCampaignPayments ?? false),
    onSuccess: (data) => {
      qc.setQueryData(["platform-settings"], data);
      qc.invalidateQueries({ queryKey: ["whatsapp-health"] });
      setConfirmOff(false);
      toast.success(data.whatsAppBotEnabled ? "WhatsApp bot switched on" : "WhatsApp bot switched off");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const s = settings.data;
  if (!s) return <div className="skeleton h-24 w-full" aria-busy="true" />;
  const on = s.whatsAppBotEnabled !== false;
  const canChange = user?.role === "SuperAdmin";

  return (
    <Card className="border-border/40">
      <CardContent className="space-y-3 p-5">
        <div className="flex items-start justify-between gap-4">
          <div className="min-w-0">
            <p className="text-[15px] font-semibold">WhatsApp bot for every institution</p>
            <p className="mt-0.5 max-w-[60ch] text-[13px] text-muted-foreground">
              Covers the assistant, group commands and posting, and WhatsApp notifications. Switched off, the bot goes quiet and every
              notification goes by SMS and email, exactly as it did before the bot.
            </p>
          </div>
          <button
            type="button" role="switch" aria-checked={on} aria-label="WhatsApp bot for every institution"
            disabled={!canChange || mutation.isPending}
            onClick={() => (on ? setConfirmOff(true) : mutation.mutate(true))}
            className={cn("relative inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors after:absolute after:-inset-3 after:content-[''] disabled:cursor-not-allowed disabled:opacity-50", on ? "bg-primary" : "bg-muted")}
          >
            <span className={cn("pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow transition-transform", on ? "translate-x-5" : "translate-x-0")} />
          </button>
        </div>
        <p className="text-[13px]">
          Right now: <span className={cn("font-semibold", on ? "text-success" : "text-destructive")}>{on ? "On" : "Off"}</span>
          {!s.whatsAppConfigured && <span className="text-muted-foreground"> · the channel is not set up yet, so this has no effect until it is</span>}
          {!canChange && <span className="text-muted-foreground"> · only a super administrator can change it</span>}
        </p>
      </CardContent>
      <ConfirmModal
        open={confirmOff}
        title="Switch the WhatsApp bot off for everyone?"
        message="The assistant and group commands stop answering right away, and notifications fall back to SMS and email. Members stay connected, and you can switch it back on at any time."
        confirmLabel="Switch off"
        variant="destructive"
        isLoading={mutation.isPending}
        onConfirm={() => mutation.mutate(false)}
        onCancel={() => setConfirmOff(false)}
      />
    </Card>
  );
}

/**
 * One switch per institution, under the platform-wide one. With the platform-wide switch on, an institution that is off here has
 * no bot and no WhatsApp notifications (SMS and email carry on); with it off, nothing runs anywhere whatever is set here.
 */
function InstitutionSwitches() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const settings = useQuery({ queryKey: ["platform-settings"], queryFn: getPlatformSettings });
  const list = useQuery({ queryKey: ["whatsapp-institutions"], queryFn: getWhatsAppInstitutions });
  const [pending, setPending] = useState<string | null>(null);
  const canChange = user?.role === "SuperAdmin";
  const globalOn = settings.data?.whatsAppBotEnabled !== false;

  const mutation = useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) => setInstitutionWhatsApp(id, enabled),
    onMutate: ({ id }) => setPending(id),
    onSuccess: (row: WhatsAppInstitutionRow) => {
      qc.setQueryData<WhatsAppInstitutionRow[]>(["whatsapp-institutions"], (rows) => (rows ?? []).map((r) => (r.id === row.id ? row : r)));
      toast.success(`WhatsApp ${row.platformEnabled ? "activated" : "deactivated"} for ${row.name}`);
    },
    onError: (e) => toast.error(handleApiError(e)),
    onSettled: () => setPending(null),
  });

  const rows = list.data ?? [];
  return (
    <Card className="border-border/40">
      <CardContent className="space-y-3 p-5">
        <div>
          <p className="text-[15px] font-semibold">WhatsApp by institution</p>
          <p className="mt-0.5 max-w-[64ch] text-[13px] text-muted-foreground">
            {globalOn
              ? "Switch it off for one institution and only that institution loses the bot and WhatsApp notifications; the others carry on."
              : "The platform-wide switch is off, so WhatsApp is blocked for every institution below whatever is set here."}
          </p>
        </div>

        {list.isLoading && <div className="skeleton h-20 w-full" aria-busy="true" />}
        {!list.isLoading && rows.length === 0 && <p className="text-[13px] text-muted-foreground">No active institutions yet.</p>}

        <ul className="divide-y divide-border">
          {rows.map((r) => {
            const effective = globalOn && r.platformEnabled && !r.institutionOptedOut;
            return (
              <li key={r.id} className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 py-3">
                <div className="min-w-0 flex-1 basis-56">
                  <p className="truncate text-[14px] font-semibold">{r.name}</p>
                  <p className="text-[12.5px] text-muted-foreground">
                    {r.connected} of {r.activeMembers} members connected · {r.activeGroups} group{r.activeGroups === 1 ? "" : "s"}
                  </p>
                  <p className="mt-0.5 text-[12.5px]">
                    <span className={cn("font-semibold", effective ? "text-success" : "text-muted-foreground")}>{effective ? "Running" : "Not running"}</span>
                    {!globalOn && <span className="text-muted-foreground"> · blocked by the platform-wide switch</span>}
                    {globalOn && !r.platformEnabled && <span className="text-muted-foreground"> · deactivated by you</span>}
                    {globalOn && r.platformEnabled && r.institutionOptedOut && <span className="text-muted-foreground"> · the institution turned it off itself</span>}
                  </p>
                </div>
                <button
                  type="button" role="switch" aria-checked={r.platformEnabled} aria-label={`WhatsApp for ${r.name}`}
                  disabled={!canChange || pending === r.id}
                  onClick={() => mutation.mutate({ id: r.id, enabled: !r.platformEnabled })}
                  className={cn("relative inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors after:absolute after:-inset-3 after:content-[''] disabled:cursor-not-allowed disabled:opacity-50", r.platformEnabled ? "bg-primary" : "bg-muted")}
                >
                  <span className={cn("pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow transition-transform", r.platformEnabled ? "translate-x-5" : "translate-x-0")} />
                </button>
              </li>
            );
          })}
        </ul>
      </CardContent>
    </Card>
  );
}

export default function WhatsAppHealthPage() {
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["whatsapp-health"], queryFn: getWhatsAppHealth, refetchInterval: 30_000 });

  if (isError) return <div className="mx-auto max-w-[1000px] p-4 sm:p-7"><LoadError title="WhatsApp health couldn’t load" onRetry={() => void refetch()} /></div>;

  const d = data;
  const state = !d ? "…" : d.botSwitchedOn === false ? "Switched off by you" : !d.enabled ? "Switched off" : !d.connected ? "Not connected" : "Connected";

  return (
    <div className="mx-auto max-w-[1000px] space-y-5 p-4 sm:p-7">
      <header>
        <h1 className="m-0 text-[20px] font-bold sm:text-[25px]">WhatsApp</h1>
        <p className="mt-1.5 max-w-[64ch] text-[13px] text-muted-foreground">
          The AlumUnion Assistant and WhatsApp notifications. This is the shared session every institution uses. You see states and counts only: never numbers, members or keys.
        </p>
      </header>

      <BotSwitch />
      <InstitutionSwitches />

      <Card className="border-border/40">
        <CardContent className="space-y-4 p-5">
          <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
            <p className="text-[26px] font-bold leading-none">{isLoading ? "…" : state}</p>
            {d && <p className="text-[13px] text-muted-foreground">Session: {d.sessionState}</p>}
          </div>
          {d && d.enabled && !d.connected && (
            <p className="border-l-2 border-warning bg-muted/40 py-2.5 pl-3 text-[13px]">
              The assistant can&apos;t reply and notifications fall back to SMS until the WhatsApp session reconnects. Reconnect it in the WaSender dashboard (scan the QR code with the assistant&apos;s phone).
            </p>
          )}
          {d && !d.enabled && (
            <p className="border-l-2 border-border bg-muted/40 py-2.5 pl-3 text-[13px]">
              Off by configuration (<span className="font-mono">WaSenderConfig__Enabled</span>). Nothing is sent or answered, and AlumUnion behaves exactly as before. Switching it on takes effect on the next restart.
            </p>
          )}
        </CardContent>
      </Card>

      <Card className="border-border/40">
        <CardContent className="p-5">
          <p className="text-[15px] font-semibold">Setup</p>
          <ul className="divide-y divide-border">
            <Check ok={!!d?.apiKeyConfigured} label="API key set" hint="Set WaSenderConfig__ApiKey." />
            <Check ok={!!d?.webhookSecretConfigured} label="Webhook secret set" hint="Set WaSenderConfig__WebhookSecret and the same value as the webhook secret in the WaSender dashboard." />
            <Check ok={!!d?.assistantNumberConfigured} label="Assistant number set" hint="Set WaSenderConfig__AssistantNumber (digits with country code) so members get a working chat link." />
          </ul>
        </CardContent>
      </Card>

      {d?.adoption && <Adoption a={d.adoption} />}

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {[
          ["Members connected", d?.verifiedMembers],
          ["Groups connected", d?.activeGroups],
          ["Institutions with groups", d?.institutionsWithGroups],
          ["Institutions that opted out", d?.institutionsOptedOut],
        ].map(([label, value]) => (
          <Card key={label as string} className="border-border/40">
            <CardContent className="p-4">
              <p className="text-[28px] font-bold leading-none tabular-nums">{value ?? "…"}</p>
              <p className="mt-1.5 text-[13px] text-muted-foreground">{label}</p>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
