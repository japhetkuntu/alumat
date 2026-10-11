"use client";

import { useQuery } from "@tanstack/react-query";
import { Card, CardContent, LoadError, cn } from "@alumni/ui";
import { getWhatsAppHealth } from "@/lib/platform-api";

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

export default function WhatsAppHealthPage() {
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["whatsapp-health"], queryFn: getWhatsAppHealth, refetchInterval: 30_000 });

  if (isError) return <div className="mx-auto max-w-[1000px] p-4 sm:p-7"><LoadError title="WhatsApp health couldn’t load" onRetry={() => void refetch()} /></div>;

  const d = data;
  const state = !d ? "…" : !d.enabled ? "Switched off" : !d.connected ? "Not connected" : "Connected";

  return (
    <div className="mx-auto max-w-[1000px] space-y-5 p-4 sm:p-7">
      <header>
        <h1 className="m-0 text-[20px] font-bold sm:text-[25px]">WhatsApp</h1>
        <p className="mt-1.5 max-w-[64ch] text-[13px] text-muted-foreground">
          The AlumUnion Assistant and WhatsApp notifications. This is the shared session every institution uses. You see states and counts only: never numbers, members or keys.
        </p>
      </header>

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
