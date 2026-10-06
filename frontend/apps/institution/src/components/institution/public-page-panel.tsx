"use client";

import { useState } from "react";
import { toast } from "sonner";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button, Card, CardContent, CardHeader, CardTitle, Checkbox, Label, ShareLinkButton, Textarea, formatDate } from "@alumni/ui";
import { updateCampaignPublicPage } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import type { Campaign, CampaignPublicPage } from "@/types";

const DEFAULTS: CampaignPublicPage = {
  isPublished: false, namePolicy: "OptedIn",
  showTotalRaised: true, showTarget: true, showProgress: true, showContributorCount: true, showDeadline: true, message: "",
};

const FIGURES: { key: "showTotalRaised" | "showTarget" | "showProgress" | "showContributorCount" | "showDeadline"; label: string }[] = [
  { key: "showTotalRaised", label: "Total contributed" },
  { key: "showTarget", label: "Target amount" },
  { key: "showProgress", label: "Progress toward the target" },
  { key: "showContributorCount", label: "Number of contributors" },
  { key: "showDeadline", label: "Closing date" },
];

/**
 * Publish a branded, shareable page for a fundraiser that lists who gave. Never shows what any one person gave;
 * the admin picks which fundraiser-wide figures appear and which givers' names are listed.
 */
export function PublicPagePanel({ campaign, pageUrl }: { campaign: Campaign; pageUrl: string }) {
  const qc = useQueryClient();
  const saved = campaign.publicPage ?? DEFAULTS;
  const [draft, setDraft] = useState<CampaignPublicPage>({ ...DEFAULTS, ...saved, message: saved.message ?? "" });

  const mut = useMutation({
    mutationFn: (body: CampaignPublicPage) => updateCampaignPublicPage(campaign.id, body),
    onSuccess: (c, body) => {
      qc.invalidateQueries({ queryKey: ["admin-campaign", campaign.id] });
      qc.invalidateQueries({ queryKey: ["admin-campaigns"] });
      toast.success(body.isPublished ? (saved.isPublished ? "Page updated" : "Page published") : saved.isPublished ? "Page taken down" : "Settings saved");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const set = <K extends keyof CampaignPublicPage>(k: K, v: CampaignPublicPage[K]) => setDraft((d) => ({ ...d, [k]: v }));
  const dirty = JSON.stringify({ ...draft, message: draft.message?.trim() || null }) !== JSON.stringify({ ...DEFAULTS, ...saved, message: saved.message?.trim() || null });
  const live = saved.isPublished;

  return (
    <Card>
      <CardHeader>
        <div className="flex items-start justify-between gap-3">
          <div>
            <CardTitle className="text-base">Public page</CardTitle>
            <p className="text-[12.5px] text-muted-foreground mt-1 max-w-xl">
              A shareable page for this fundraiser that thanks the people who gave. It never shows what any one person gave.
            </p>
          </div>
          <span className={live ? "text-[12px] font-semibold text-success" : "text-[12px] font-semibold text-muted-foreground"}>
            {live ? `Live${saved.publishedAt ? ` since ${formatDate(saved.publishedAt)}` : ""}` : "Not published"}
          </span>
        </div>
      </CardHeader>
      <CardContent className="space-y-6">
        <fieldset className="space-y-2">
          <legend className="text-sm font-semibold mb-1">Names to list</legend>
          {([
            ["OptedIn", "Only people who chose to be shown", "Givers tick “show my name” when they pay."],
            ["Everyone", "Everyone who has given", "Includes people who didn’t tick the box. Use only if your givers expect to be named."],
          ] as const).map(([value, title, hint]) => (
            <label key={value} className="flex items-start gap-2.5 cursor-pointer">
              <input type="radio" name="name-policy" className="mt-1 h-4 w-4" checked={draft.namePolicy === value} onChange={() => set("namePolicy", value)} />
              <span>
                <span className="block text-sm font-medium">{title}</span>
                <span className="block text-[12.5px] text-muted-foreground">{hint}</span>
              </span>
            </label>
          ))}
        </fieldset>

        <fieldset className="space-y-2">
          <legend className="text-sm font-semibold mb-1">Figures to show</legend>
          <div className="grid sm:grid-cols-2 gap-x-6 gap-y-2">
            {FIGURES.map(({ key, label }) => (
              <label key={key} className="flex items-center gap-2.5 cursor-pointer text-sm">
                <Checkbox checked={draft[key]} onCheckedChange={(v) => set(key, v === true)} />
                {label}
              </label>
            ))}
          </div>
        </fieldset>

        <div className="space-y-1.5">
          <Label htmlFor="public-page-note">A note of thanks <span className="text-muted-foreground font-normal">(optional)</span></Label>
          <Textarea id="public-page-note" rows={3} maxLength={400} value={draft.message ?? ""} onChange={(e) => set("message", e.target.value)} placeholder="Thank you to everyone who has given so far." />
          <p className="text-[12px] text-muted-foreground text-right tabular-nums">{(draft.message ?? "").length}/400</p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {live ? (
            <>
              <Button onClick={() => mut.mutate({ ...draft, isPublished: true })} disabled={!dirty || mut.isPending}>Save changes</Button>
              <Button variant="outline" onClick={() => mut.mutate({ ...draft, isPublished: false })} disabled={mut.isPending}>Take down</Button>
              <a href={pageUrl} target="_blank" rel="noreferrer" className="text-sm font-semibold text-accent hover:underline px-2">View page</a>
              <ShareLinkButton url={pageUrl} title={campaign.title} onSuccess={(r) => toast.success(r === "shared" ? "Share sheet opened" : "Page link copied")} onError={(m) => toast.error(m)} />
            </>
          ) : (
            <>
              <Button onClick={() => mut.mutate({ ...draft, isPublished: true })} disabled={mut.isPending}>Publish page</Button>
              <Button variant="outline" onClick={() => mut.mutate({ ...draft, isPublished: false })} disabled={!dirty || mut.isPending}>Save without publishing</Button>
            </>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
