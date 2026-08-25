"use client";

import { useRef, useState } from "react";
import Link from "next/link";
import { Plus, Trash2, Copy, ExternalLink } from "lucide-react";
import { notFound, useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { FormError } from "@alumni/ui";
import { DropdownMenu, DropdownMenuTrigger, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { BrandPreview } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import {
  getInstitution, updateInstitutionBranding, updateInstitutionPlan, updateInstitutionStatus,
  updateInstitutionFeatures, getFeatureCatalog,
  updateInstitutionPayments, getInstitutionRevenue,
  updateInstitutionLandingContent, STORY_ICON_OPTIONS, type LandingPageStory, type NewsBanner,
  uploadPlatformImage,
} from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

const TABS = ["Overview", "Branding", "Features", "Content", "Admins", "Usage & Limits", "Payments"] as const;

const EMPTY_STORY: LandingPageStory = { icon: "Briefcase", eyebrow: "", scenario: "", description: "", imageUrl: "" };
const EMPTY_BANNER: NewsBanner = { enabled: false, text: "", linkText: "", linkUrl: "" };

const statusBadge: Record<string, { label: string; variant: "info" | "success" | "warning" | "destructive" }> = {
  Trial: { label: "Trial", variant: "info" },
  Active: { label: "Active", variant: "success" },
  Suspended: { label: "Suspended", variant: "destructive" },
  Cancelled: { label: "Cancelled", variant: "warning" },
};

function Limit({ label, used, limit, unit = "" }: { label: string; used: number; limit: number; unit?: string }) {
  const pct = limit > 0 ? Math.min(100, Math.round((used / limit) * 100)) : 0;
  return (
    <div className="mb-4">
      <div className="flex justify-between text-[13px] mb-1.5">
        <span className="font-semibold">{label}</span>
        <span className="text-muted-foreground">{used.toLocaleString()}{unit} / {limit.toLocaleString()}{unit}</span>
      </div>
      <div className="h-[7px] bg-muted rounded-full overflow-hidden">
        <div className={`h-full rounded-full ${pct > 85 ? "bg-amber-500" : "bg-primary"}`} style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

/** A URL text field with an "Upload" button beside it — uploads to S3-backed storage and fills the field, instead of requiring a hand-pasted hosted URL. */
function ImageUrlField({ label, hint, value, onChange, placeholder }: {
  label: string; hint?: string; value: string; onChange: (url: string) => void; placeholder: string;
}) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadPlatformImage(file),
    onSuccess: (url) => { onChange(url); toast.success("Image uploaded"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      {hint && <p className="text-[12px] text-muted-foreground -mt-0.5">{hint}</p>}
      <div className="flex items-center gap-2">
        <Input value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder} />
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          className="hidden"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) uploadMutation.mutate(file);
            e.target.value = "";
          }}
        />
        <Button type="button" variant="outline" size="sm" className="shrink-0" onClick={() => fileInputRef.current?.click()} disabled={uploadMutation.isPending}>
          {uploadMutation.isPending ? "Uploading…" : "Upload"}
        </Button>
      </div>
    </div>
  );
}

export default function InstitutionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<(typeof TABS)[number]>("Overview");

  const { data: inst, isLoading, isError } = useQuery({
    queryKey: ["institution", id],
    queryFn: () => getInstitution(id),
    retry: false,
  });
  const { data: featureCatalog = [] } = useQuery({ queryKey: ["feature-catalog"], queryFn: getFeatureCatalog });
  const { data: revenue } = useQuery({
    queryKey: ["institution-revenue", id],
    queryFn: () => getInstitutionRevenue(id),
    enabled: tab === "Payments",
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ["institution", id] });
    queryClient.invalidateQueries({ queryKey: ["institutions"] });
  };

  const statusMutation = useMutation({
    mutationFn: (status: string) => updateInstitutionStatus(id, status),
    onSuccess: (updated) => {
      toast.success(`Institution status set to ${updated.status}`);
      invalidate();
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const [planOpen, setPlanOpen] = useState(false);
  const [planForm, setPlanForm] = useState({ plan: "", memberLimit: "", storageLimitGb: "" });
  const [planError, setPlanError] = useState<string | null>(null);
  const planMutation = useMutation({
    mutationFn: () =>
      updateInstitutionPlan(
        id,
        planForm.plan,
        planForm.memberLimit ? Number(planForm.memberLimit) : undefined,
        planForm.storageLimitGb ? Number(planForm.storageLimitGb) : undefined
      ),
    onSuccess: () => {
      toast.success("Capacity limits updated");
      invalidate();
      setPlanOpen(false);
      setPlanError(null);
    },
    onError: (e) => {
      const msg = handleApiError(e);
      setPlanError(msg);
      toast.error(msg);
    },
  });

  const [branding, setBranding] = useState<{
    portalName: string; tagline: string; contactEmail: string; supportEmail: string;
    logoUrl: string; iconUrl: string; primaryColorHex: string; secondaryColorHex: string;
    institutionPortalTitle: string; institutionAuthHeadline: string; institutionAuthSubtext: string;
    memberPortalTitle: string; memberAuthHeadline: string; memberAuthSubtext: string;
    requireStudentId: boolean;
  } | null>(null);
  const [brandingError, setBrandingError] = useState<string | null>(null);
  const brandingMutation = useMutation({
    mutationFn: () =>
      updateInstitutionBranding(id, {
        portalName: branding!.portalName,
        tagline: branding!.tagline || undefined,
        contactEmail: branding!.contactEmail,
        supportEmail: branding!.supportEmail || undefined,
        logoUrl: branding!.logoUrl || undefined,
        iconUrl: branding!.iconUrl || undefined,
        primaryColorHex: branding!.primaryColorHex,
        secondaryColorHex: branding!.secondaryColorHex || undefined,
        institutionPortalTitle: branding!.institutionPortalTitle || undefined,
        institutionAuthHeadline: branding!.institutionAuthHeadline || undefined,
        institutionAuthSubtext: branding!.institutionAuthSubtext || undefined,
        memberPortalTitle: branding!.memberPortalTitle || undefined,
        memberAuthHeadline: branding!.memberAuthHeadline || undefined,
        memberAuthSubtext: branding!.memberAuthSubtext || undefined,
        requireStudentId: branding!.requireStudentId,
      }),
    onSuccess: () => {
      toast.success("Branding updated");
      invalidate();
      setBrandingError(null);
    },
    onError: (e) => {
      const msg = handleApiError(e);
      setBrandingError(msg);
      toast.error(msg);
    },
  });

  const [disabledFeatures, setDisabledFeatures] = useState<Set<string> | null>(null);
  const [featuresError, setFeaturesError] = useState<string | null>(null);
  const featuresMutation = useMutation({
    mutationFn: () => updateInstitutionFeatures(id, Array.from(disabledFeatures!)),
    onSuccess: () => {
      toast.success("Features updated");
      invalidate();
      setFeaturesError(null);
    },
    onError: (e) => {
      const msg = handleApiError(e);
      setFeaturesError(msg);
      toast.error(msg);
    },
  });

  const [payments, setPayments] = useState<{
    platformFeePercentage: string; settlementBankCode: string; settlementBankName: string;
    settlementAccountNumber: string; settlementAccountName: string;
  } | null>(null);
  const [paymentsError, setPaymentsError] = useState<string | null>(null);
  const paymentsMutation = useMutation({
    mutationFn: () =>
      updateInstitutionPayments(id, {
        platformFeePercentage: Number(payments!.platformFeePercentage) || 0,
        settlementBankCode: payments!.settlementBankCode,
        settlementBankName: payments!.settlementBankName,
        settlementAccountNumber: payments!.settlementAccountNumber,
        settlementAccountName: payments!.settlementAccountName,
      }),
    onSuccess: () => {
      toast.success("Payments settings updated");
      invalidate();
      queryClient.invalidateQueries({ queryKey: ["institution-revenue", id] });
      setPaymentsError(null);
    },
    onError: (e) => {
      const msg = handleApiError(e);
      setPaymentsError(msg);
      toast.error(msg);
    },
  });

  const [stories, setStories] = useState<LandingPageStory[] | null>(null);
  const [banner, setBanner] = useState<NewsBanner | null>(null);
  const [contentError, setContentError] = useState<string | null>(null);
  const contentMutation = useMutation({
    mutationFn: () => updateInstitutionLandingContent(id, stories ?? [], banner?.enabled ? banner : null),
    onSuccess: () => {
      toast.success("Landing content updated");
      invalidate();
      setContentError(null);
    },
    onError: (e) => {
      const msg = handleApiError(e);
      setContentError(msg);
      toast.error(msg);
    },
  });

  if (isError) notFound();
  if (isLoading || !inst) {
    return <div className="p-7 text-[13px] text-muted-foreground">Loading institution…</div>;
  }

  if (branding === null) {
    setBranding({
      portalName: inst.portalName,
      tagline: inst.tagline ?? "",
      contactEmail: inst.contactEmail,
      supportEmail: inst.supportEmail ?? "",
      logoUrl: inst.logoUrl ?? "",
      iconUrl: inst.iconUrl ?? "",
      primaryColorHex: inst.primaryColorHex,
      secondaryColorHex: inst.secondaryColorHex ?? "",
      institutionPortalTitle: inst.institutionPortalTitle ?? "",
      institutionAuthHeadline: inst.institutionAuthHeadline ?? "",
      institutionAuthSubtext: inst.institutionAuthSubtext ?? "",
      memberPortalTitle: inst.memberPortalTitle ?? "",
      memberAuthHeadline: inst.memberAuthHeadline ?? "",
      memberAuthSubtext: inst.memberAuthSubtext ?? "",
      requireStudentId: inst.requireStudentId,
    });
  }

  if (disabledFeatures === null) {
    setDisabledFeatures(new Set(inst.disabledFeatures));
  }

  if (payments === null) {
    setPayments({
      platformFeePercentage: String(inst.platformFeePercentage ?? 0),
      settlementBankCode: inst.settlementBankCode ?? "",
      settlementBankName: inst.settlementBankName ?? "",
      settlementAccountNumber: inst.settlementAccountNumber ?? "",
      settlementAccountName: inst.settlementAccountName ?? "",
    });
  }

  if (stories === null) {
    setStories(inst.landingPageStories.length ? inst.landingPageStories : []);
  }
  if (banner === null) {
    setBanner(inst.newsBanner ?? EMPTY_BANNER);
  }

  const badge = statusBadge[inst.status] ?? statusBadge.Trial;

  return (
    <div className="p-7 max-w-[1500px]">
      <p className="text-[12px] text-muted-foreground mb-3">
        <Link href="/institutions" className="hover:underline">Institutions</Link> / {inst.name}
      </p>

      <Card className="mb-5">
        <CardContent className="p-5 flex items-center justify-between flex-wrap gap-3">
          <div className="flex items-center gap-4">
            <UserAvatar name={inst.name} size="lg" />
            <div>
              <h1 className="text-[20px] font-bold flex items-center gap-2">
                {inst.name}
                <Badge variant={badge.variant}>{badge.label}</Badge>
              </h1>
              <p className="text-[13px] text-muted-foreground font-mono mt-1">
                {inst.customDomain ?? inst.memberPortalUrl.replace(/^https?:\/\//, "")} &middot; {inst.plan} plan &middot; {inst.memberCount.toLocaleString()} members
              </p>
            </div>
          </div>
          <div className="flex gap-2">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" disabled={statusMutation.isPending}>More actions</Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                {inst.status !== "Active" && (
                  <DropdownMenuItem onClick={() => statusMutation.mutate("Active")}>Activate</DropdownMenuItem>
                )}
                {inst.status !== "Trial" && (
                  <DropdownMenuItem onClick={() => statusMutation.mutate("Trial")}>Move to trial</DropdownMenuItem>
                )}
                {inst.status !== "Suspended" && (
                  <DropdownMenuItem onClick={() => statusMutation.mutate("Suspended")} className="text-destructive">
                    Suspend
                  </DropdownMenuItem>
                )}
                <DropdownMenuSeparator />
                {inst.status !== "Cancelled" && (
                  <DropdownMenuItem onClick={() => statusMutation.mutate("Cancelled")} className="text-destructive">
                    Cancel institution
                  </DropdownMenuItem>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </CardContent>
      </Card>

      <div className="flex gap-6 border-b border-border mb-5 overflow-x-auto">
        {TABS.map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`pb-3 text-[13.5px] font-medium whitespace-nowrap border-b-2 -mb-px transition-colors ${
              tab === t ? "text-primary border-primary font-semibold" : "text-muted-foreground border-transparent hover:text-foreground"
            }`}
          >
            {t}
          </button>
        ))}
      </div>

      {tab === "Overview" && (
        <div className="grid grid-cols-1 lg:grid-cols-[1.5fr_1fr] gap-4">
          <Card>
            <CardContent className="p-5 space-y-3 text-[13.5px]">
              <div className="flex justify-between border-b border-border pb-3"><span className="text-muted-foreground">Primary contact</span><span className="font-semibold">{inst.contactName}</span></div>
              <div className="flex justify-between border-b border-border pb-3"><span className="text-muted-foreground">Contact email</span><span className="font-semibold">{inst.contactEmail}</span></div>
              <div className="flex justify-between border-b border-border pb-3"><span className="text-muted-foreground">Plan</span><span className="font-semibold">{inst.plan}</span></div>
              <div className="flex justify-between"><span className="text-muted-foreground">Onboarded</span><span className="font-semibold">{formatDate(inst.onboardedAt)}</span></div>
            </CardContent>
          </Card>
          <Card>
            <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Recent audit</p></div>
            <CardContent className="p-5">
              <p className="text-[13px] text-muted-foreground">
                See the <Link href="/audit-log" className="text-primary hover:underline">platform audit log</Link> for actions taken on this institution.
              </p>
            </CardContent>
          </Card>
        </div>
      )}

      {tab === "Branding" && branding && (
        <div className="space-y-4 max-w-[680px]">
          <Card>
            <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Identity</p></div>
            <CardContent className="p-5 space-y-4">
              <div className="space-y-1.5">
                <Label>Display name</Label>
                <Input value={branding.portalName} onChange={(e) => setBranding((b) => ({ ...b!, portalName: e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>Tagline</Label>
                <Input value={branding.tagline} onChange={(e) => setBranding((b) => ({ ...b!, tagline: e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>Contact email</Label>
                <Input type="email" value={branding.contactEmail} onChange={(e) => setBranding((b) => ({ ...b!, contactEmail: e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>Support email</Label>
                <Input type="email" value={branding.supportEmail} onChange={(e) => setBranding((b) => ({ ...b!, supportEmail: e.target.value }))} />
              </div>
              <div className="space-y-2.5">
                <div>
                  <p className="text-[12px] font-semibold text-muted-foreground mb-1">Member portal</p>
                  <div className="text-[14px] font-mono flex items-center gap-2">
                    {inst.memberPortalUrl.replace(/^https?:\/\//, "")}
                    <Badge variant="success">Live</Badge>
                    <button
                      type="button"
                      className="text-muted-foreground hover:text-foreground"
                      onClick={async () => { await navigator.clipboard.writeText(inst.memberPortalUrl); toast.success("Member portal link copied"); }}
                      title="Copy link"
                    >
                      <Copy size={13} />
                    </button>
                    <a href={inst.memberPortalUrl} target="_blank" rel="noreferrer" className="text-muted-foreground hover:text-foreground" title="Open">
                      <ExternalLink size={13} />
                    </a>
                  </div>
                </div>
                <div>
                  <p className="text-[12px] font-semibold text-muted-foreground mb-1">Institution portal</p>
                  <div className="text-[14px] font-mono flex items-center gap-2">
                    {inst.institutionPortalUrl.replace(/^https?:\/\//, "")}
                    <Badge variant="success">Live</Badge>
                    <button
                      type="button"
                      className="text-muted-foreground hover:text-foreground"
                      onClick={async () => { await navigator.clipboard.writeText(inst.institutionPortalUrl); toast.success("Institution portal link copied"); }}
                      title="Copy link"
                    >
                      <Copy size={13} />
                    </button>
                    <a href={inst.institutionPortalUrl} target="_blank" rel="noreferrer" className="text-muted-foreground hover:text-foreground" title="Open">
                      <ExternalLink size={13} />
                    </a>
                  </div>
                </div>
              </div>
              <div>
                <p className="text-[12px] font-semibold text-muted-foreground mb-1">Custom domain</p>
                <p className="text-[14px] font-mono">{inst.customDomain ?? "Not configured"}</p>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label>Primary color</Label>
                  <div className="flex items-center gap-2">
                    <span className="w-7 h-7 rounded-md border border-border" style={{ background: branding.primaryColorHex }} />
                    <Input value={branding.primaryColorHex} onChange={(e) => setBranding((b) => ({ ...b!, primaryColorHex: e.target.value }))} className="w-[140px]" />
                  </div>
                </div>
                <div className="space-y-1.5">
                  <Label>Secondary color</Label>
                  <div className="flex items-center gap-2">
                    <span className="w-7 h-7 rounded-md border border-border" style={{ background: branding.secondaryColorHex || "#e2e8f0" }} />
                    <Input value={branding.secondaryColorHex} onChange={(e) => setBranding((b) => ({ ...b!, secondaryColorHex: e.target.value }))} className="w-[140px]" placeholder="Optional" />
                  </div>
                </div>
              </div>
              <div className="space-y-1.5 pt-1">
                <Label>Preview</Label>
                <BrandPreview color={branding.primaryColorHex} secondaryColor={branding.secondaryColorHex || undefined} name={branding.portalName || inst.name} className="pt-1" />
              </div>
              <ImageUrlField
                label="Logo URL"
                value={branding.logoUrl}
                onChange={(url) => setBranding((b) => ({ ...b!, logoUrl: url }))}
                placeholder="https://…/logo.svg"
              />
              <ImageUrlField
                label="Icon URL"
                hint="Small square icon — used for the browser tab and app icon, separate from the logo."
                value={branding.iconUrl}
                onChange={(url) => setBranding((b) => ({ ...b!, iconUrl: url }))}
                placeholder="https://…/icon.png"
              />
            </CardContent>
          </Card>

          <Card>
            <div className="px-5 py-4 border-b border-border">
              <p className="text-[14px] font-semibold">Institution Portal content</p>
              <p className="text-[12px] text-muted-foreground mt-0.5">Shown to this institution&apos;s own staff, on their sign-in page.</p>
            </div>
            <CardContent className="p-5 space-y-4">
              <div className="space-y-1.5">
                <Label>Browser tab title</Label>
                <Input value={branding.institutionPortalTitle} onChange={(e) => setBranding((b) => ({ ...b!, institutionPortalTitle: e.target.value }))} placeholder={branding.portalName || "Institution Portal"} />
              </div>
              <div className="space-y-1.5">
                <Label>Sign-in headline</Label>
                <Input value={branding.institutionAuthHeadline} onChange={(e) => setBranding((b) => ({ ...b!, institutionAuthHeadline: e.target.value }))} placeholder="Run the alumni office with a clear view of what matters." />
              </div>
              <div className="space-y-1.5">
                <Label>Sign-in subtext</Label>
                <Textarea rows={3} value={branding.institutionAuthSubtext} onChange={(e) => setBranding((b) => ({ ...b!, institutionAuthSubtext: e.target.value }))} placeholder="A short supporting sentence shown under the headline." />
              </div>
            </CardContent>
          </Card>

          <Card>
            <div className="px-5 py-4 border-b border-border">
              <p className="text-[14px] font-semibold">Member Portal content</p>
              <p className="text-[12px] text-muted-foreground mt-0.5">Shown to this institution&apos;s alumni, on their sign-in and registration pages.</p>
            </div>
            <CardContent className="p-5 space-y-4">
              <div className="space-y-1.5">
                <Label>Browser tab title</Label>
                <Input value={branding.memberPortalTitle} onChange={(e) => setBranding((b) => ({ ...b!, memberPortalTitle: e.target.value }))} placeholder={branding.portalName || "Alumni Portal"} />
              </div>
              <div className="space-y-1.5">
                <Label>Sign-in quote</Label>
                <Textarea rows={3} value={branding.memberAuthHeadline} onChange={(e) => setBranding((b) => ({ ...b!, memberAuthHeadline: e.target.value }))} placeholder="A short testimonial-style quote shown on the sign-in page." />
              </div>
              <div className="space-y-1.5">
                <Label>Quote attribution</Label>
                <Input value={branding.memberAuthSubtext} onChange={(e) => setBranding((b) => ({ ...b!, memberAuthSubtext: e.target.value }))} placeholder="Alumna · Class of 2018" />
              </div>
              <div className="flex items-center justify-between border-t border-border pt-4">
                <div>
                  <p className="text-[13px] font-semibold">Require Student ID at registration</p>
                  <p className="text-[12px] text-muted-foreground mt-0.5">Off makes the field optional for this institution&apos;s members.</p>
                </div>
                <button
                  type="button"
                  role="switch"
                  aria-checked={branding.requireStudentId}
                  onClick={() => setBranding((b) => ({ ...b!, requireStudentId: !b!.requireStudentId }))}
                  className={`relative inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors ${branding.requireStudentId ? "bg-primary" : "bg-muted"}`}
                >
                  <span className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow-lg transition-transform ${branding.requireStudentId ? "translate-x-5" : "translate-x-0"}`} />
                </button>
              </div>
            </CardContent>
          </Card>

          <FormError message={brandingError} />
          <Button onClick={() => brandingMutation.mutate()} disabled={brandingMutation.isPending}>
            {brandingMutation.isPending ? "Saving…" : "Save branding"}
          </Button>
        </div>
      )}

      {tab === "Features" && disabledFeatures && (
        <Card className="max-w-[680px]">
          <div className="px-5 py-4 border-b border-border">
            <p className="text-[14px] font-semibold">Feature access</p>
            <p className="text-[12px] text-muted-foreground mt-0.5">
              All features are enabled by default. Turning one off removes it from both this institution&apos;s staff and member portals — enforced on the backend too, not just hidden.
            </p>
          </div>
          <CardContent className="p-0">
            {featureCatalog.map((f) => {
              const enabled = !disabledFeatures.has(f.key);
              return (
                <div key={f.key} className="flex items-center justify-between px-5 py-3.5 border-b border-border last:border-0">
                  <div>
                    <p className="text-[13px] font-semibold">{f.label}</p>
                    {f.description && (
                      <p className="text-[12px] text-muted-foreground mt-0.5">{f.description}</p>
                    )}
                  </div>
                  <button
                    type="button"
                    role="switch"
                    aria-checked={enabled}
                    onClick={() =>
                      setDisabledFeatures((prev) => {
                        const next = new Set(prev);
                        if (enabled) next.add(f.key);
                        else next.delete(f.key);
                        return next;
                      })
                    }
                    className={`relative inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors ${enabled ? "bg-primary" : "bg-muted"}`}
                  >
                    <span className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow-lg transition-transform ${enabled ? "translate-x-5" : "translate-x-0"}`} />
                  </button>
                </div>
              );
            })}
          </CardContent>
          <div className="p-5 border-t border-border space-y-3">
            <FormError message={featuresError} />
            <Button onClick={() => featuresMutation.mutate()} disabled={featuresMutation.isPending}>
              {featuresMutation.isPending ? "Saving…" : "Save features"}
            </Button>
          </div>
        </Card>
      )}

      {tab === "Content" && stories && banner && (
        <div className="space-y-4 max-w-[680px]">
          <Card>
            <div className="px-5 py-4 border-b border-border">
              <p className="text-[14px] font-semibold">News banner</p>
              <p className="text-[12px] text-muted-foreground mt-0.5">The dismissible strip at the top of the Member Portal landing page. This institution&apos;s own admins can also edit this.</p>
            </div>
            <CardContent className="p-5 space-y-4">
              <div className="flex items-center justify-between">
                <p className="text-[13px] font-semibold">Show banner</p>
                <button
                  type="button"
                  role="switch"
                  aria-checked={banner.enabled}
                  onClick={() => setBanner((b) => ({ ...b!, enabled: !b!.enabled }))}
                  className={`relative inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors ${banner.enabled ? "bg-primary" : "bg-muted"}`}
                >
                  <span className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow-lg transition-transform ${banner.enabled ? "translate-x-5" : "translate-x-0"}`} />
                </button>
              </div>
              <div className="space-y-1.5">
                <Label>Banner text</Label>
                <Textarea rows={2} value={banner.text} onChange={(e) => setBanner((b) => ({ ...b!, text: e.target.value }))} placeholder="A new Vice Chancellor has been appointed — effective this year." />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label>Link text</Label>
                  <Input value={banner.linkText ?? ""} onChange={(e) => setBanner((b) => ({ ...b!, linkText: e.target.value }))} placeholder="See the spotlight" />
                </div>
                <div className="space-y-1.5">
                  <Label>Link URL</Label>
                  <Input value={banner.linkUrl ?? ""} onChange={(e) => setBanner((b) => ({ ...b!, linkUrl: e.target.value }))} placeholder="#spotlight or https://…" />
                </div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <div className="px-5 py-4 border-b border-border flex items-center justify-between">
              <div>
                <p className="text-[14px] font-semibold">Stories</p>
                <p className="text-[12px] text-muted-foreground mt-0.5">The &quot;why alumni join&quot; cards on the landing page. Leave empty to use generic default copy.</p>
              </div>
              <Button variant="outline" size="sm" onClick={() => setStories((s) => [...(s ?? []), { ...EMPTY_STORY }])}>
                <Plus size={14} className="mr-1.5" /> Add story
              </Button>
            </div>
            <CardContent className="p-5 space-y-5">
              {stories.length === 0 && (
                <p className="text-[13px] text-muted-foreground">No custom stories — the landing page falls back to built-in default copy.</p>
              )}
              {stories.map((story, i) => (
                <div key={i} className="space-y-3 pb-5 border-b border-border last:border-0 last:pb-0">
                  <div className="flex items-center justify-between">
                    <p className="text-[12px] font-bold text-muted-foreground uppercase tracking-wide">Story {i + 1}</p>
                    <button type="button" onClick={() => setStories((s) => s!.filter((_, idx) => idx !== i))} className="text-destructive hover:opacity-70">
                      <Trash2 size={14} />
                    </button>
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <div className="space-y-1.5">
                      <Label>Icon</Label>
                      <select
                        className="w-full h-9 rounded-md border border-border bg-background px-3 text-[13px]"
                        value={story.icon}
                        onChange={(e) => setStories((s) => s!.map((it, idx) => (idx === i ? { ...it, icon: e.target.value } : it)))}
                      >
                        {STORY_ICON_OPTIONS.map((opt) => <option key={opt} value={opt}>{opt}</option>)}
                      </select>
                    </div>
                    <div className="space-y-1.5">
                      <Label>Eyebrow</Label>
                      <Input value={story.eyebrow} onChange={(e) => setStories((s) => s!.map((it, idx) => (idx === i ? { ...it, eyebrow: e.target.value } : it)))} placeholder="Career" />
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <Label>Title</Label>
                    <Input value={story.scenario} onChange={(e) => setStories((s) => s!.map((it, idx) => (idx === i ? { ...it, scenario: e.target.value } : it)))} placeholder="The job that never reached a public board" />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Description</Label>
                    <Textarea rows={2} value={story.description} onChange={(e) => setStories((s) => s!.map((it, idx) => (idx === i ? { ...it, description: e.target.value } : it)))} />
                  </div>
                  <ImageUrlField
                    label="Image URL"
                    value={story.imageUrl ?? ""}
                    onChange={(url) => setStories((s) => s!.map((it, idx) => (idx === i ? { ...it, imageUrl: url } : it)))}
                    placeholder="https://…"
                  />
                </div>
              ))}
            </CardContent>
          </Card>

          <FormError message={contentError} />
          <Button onClick={() => contentMutation.mutate()} disabled={contentMutation.isPending}>
            {contentMutation.isPending ? "Saving…" : "Save content"}
          </Button>
        </div>
      )}

      {tab === "Admins" && (
        <Card>
          <div className="px-5 py-4 border-b border-border flex items-center justify-between">
            <p className="text-[14px] font-semibold">Institution admins</p>
          </div>
          <CardContent className="p-0">
            <div className="flex items-center justify-between px-5 py-3.5 border-b border-border">
              <div className="flex items-center gap-3">
                <UserAvatar name={inst.contactName} size="sm" />
                <div>
                  <p className="text-[13.5px] font-semibold">{inst.contactName}</p>
                  <p className="text-[12px] text-muted-foreground">SuperAdmin &middot; {inst.contactEmail}</p>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {tab === "Usage & Limits" && (
        <Card>
          <CardContent className="p-5 max-w-[560px]">
            <Limit label="Members" used={inst.memberCount} limit={inst.memberLimit} />
            <Limit label="Storage" used={inst.storageUsedGb} limit={inst.storageLimitGb} unit=" GB" />
            <Button
              variant="outline"
              className="mt-2"
              onClick={() => {
                setPlanForm({ plan: inst.plan, memberLimit: String(inst.memberLimit), storageLimitGb: String(inst.storageLimitGb) });
                setPlanOpen(true);
              }}
            >
              Change limits
            </Button>
          </CardContent>
        </Card>
      )}

      {tab === "Payments" && payments && (
        <div className="space-y-4 max-w-[680px]">
          <Card>
            <div className="px-5 py-4 border-b border-border">
              <p className="text-[14px] font-semibold">Revenue</p>
              <p className="text-[12px] text-muted-foreground mt-0.5">Real figures from this institution&apos;s confirmed payments.</p>
            </div>
            <CardContent className="p-5">
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <div>
                  <p className="text-[12px] text-muted-foreground">Gross collected</p>
                  <p className="text-[20px] font-bold mt-1">{formatCurrency(revenue?.grossCollected ?? 0, "GHS")}</p>
                </div>
                <div>
                  <p className="text-[12px] text-muted-foreground">Platform fee</p>
                  <p className="text-[20px] font-bold mt-1">{formatCurrency(revenue?.platformFeeTotal ?? 0, "GHS")}</p>
                </div>
                <div>
                  <p className="text-[12px] text-muted-foreground">Net to institution</p>
                  <p className="text-[20px] font-bold mt-1">{formatCurrency(revenue?.netToInstitution ?? 0, "GHS")}</p>
                </div>
              </div>
              <p className="text-[12px] text-muted-foreground mt-3">{(revenue?.confirmedPaymentCount ?? 0).toLocaleString()} confirmed payments</p>
            </CardContent>
          </Card>

          <Card>
            <div className="px-5 py-4 border-b border-border">
              <p className="text-[14px] font-semibold">Fee &amp; settlement</p>
              <p className="text-[12px] text-muted-foreground mt-0.5">
                The platform&apos;s cut of each confirmed payment, and the bank account this institution&apos;s share settles to.
              </p>
            </div>
            <CardContent className="p-5 space-y-4">
              <div className="space-y-1.5">
                <Label>Platform fee</Label>
                <div className="flex items-center gap-2">
                  <Input
                    type="number"
                    min={0}
                    max={100}
                    step="0.1"
                    value={payments.platformFeePercentage}
                    onChange={(e) => setPayments((p) => ({ ...p!, platformFeePercentage: e.target.value }))}
                    className="w-[120px]"
                  />
                  <span className="text-[13px] text-muted-foreground">% of each confirmed payment</span>
                </div>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label>Settlement bank name</Label>
                  <Input value={payments.settlementBankName} onChange={(e) => setPayments((p) => ({ ...p!, settlementBankName: e.target.value }))} placeholder="GCB Bank" />
                </div>
                <div className="space-y-1.5">
                  <Label>Settlement bank code</Label>
                  <Input value={payments.settlementBankCode} onChange={(e) => setPayments((p) => ({ ...p!, settlementBankCode: e.target.value }))} placeholder="040100" />
                </div>
                <div className="space-y-1.5">
                  <Label>Account number</Label>
                  <Input value={payments.settlementAccountNumber} onChange={(e) => setPayments((p) => ({ ...p!, settlementAccountNumber: e.target.value }))} placeholder="1234567890" />
                </div>
                <div className="space-y-1.5">
                  <Label>Account name</Label>
                  <Input value={payments.settlementAccountName} onChange={(e) => setPayments((p) => ({ ...p!, settlementAccountName: e.target.value }))} placeholder="Institution alumni association" />
                </div>
              </div>
              {inst.paystackSubaccountCode && (
                <p className="text-[12px] text-muted-foreground">Payment subaccount ID: <span className="font-mono">{inst.paystackSubaccountCode}</span></p>
              )}
            </CardContent>
          </Card>

          <FormError message={paymentsError} />
          <Button onClick={() => paymentsMutation.mutate()} disabled={paymentsMutation.isPending}>
            {paymentsMutation.isPending ? "Saving…" : "Save payments"}
          </Button>
        </div>
      )}

      <Dialog open={planOpen} onOpenChange={(o) => { setPlanOpen(o); if (!o) setPlanError(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Update capacity limits</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 mt-2">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Member limit</Label>
                <Input type="number" value={planForm.memberLimit} onChange={(e) => setPlanForm((f) => ({ ...f, memberLimit: e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>Storage GB</Label>
                <Input type="number" value={planForm.storageLimitGb} onChange={(e) => setPlanForm((f) => ({ ...f, storageLimitGb: e.target.value }))} />
              </div>
            </div>
            <FormError message={planError} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setPlanOpen(false)}>Cancel</Button>
            <Button onClick={() => planMutation.mutate()} disabled={planMutation.isPending || !planForm.plan}>
              {planMutation.isPending ? "Saving…" : "Save limits"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
