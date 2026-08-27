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
import { FormSelect } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { FormError } from "@alumni/ui";
import { DropdownMenu, DropdownMenuTrigger, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { BrandPreview } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import {
  getInstitution, updateInstitutionBranding, updateInstitutionStatus, updateInstitutionMemberPolicy,
  updateInstitutionFeatures, getFeatureCatalog,
  updateInstitutionPayments, getInstitutionRevenue,
  updateInstitutionLandingContent, STORY_ICON_OPTIONS, type LandingPageStory, type NewsBanner,
  uploadPlatformImage,
  getInstitutionStaff, inviteInstitutionStaff, setInstitutionStaffDisabled,
} from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";
import { SettlementAccountFields } from "@/components/platform/settlement-account-fields";

const TABS = ["Overview", "Branding", "Features", "Content", "Admins", "Payments"] as const;

const EMPTY_STORY: LandingPageStory = { icon: "Briefcase", eyebrow: "", scenario: "", description: "", imageUrl: "" };
const EMPTY_BANNER: NewsBanner = { enabled: false, text: "", linkText: "", linkUrl: "" };

const statusBadge: Record<string, { label: string; variant: "info" | "success" | "warning" | "destructive" }> = {
  Trial: { label: "Trial", variant: "info" },
  Active: { label: "Active", variant: "success" },
  Suspended: { label: "Suspended", variant: "destructive" },
  Cancelled: { label: "Cancelled", variant: "warning" },
};

/** A URL text field with an "Upload" button beside it — uploads to S3-backed storage and fills the field, instead of requiring a hand-pasted hosted URL. */
function ImageUrlField({ label, hint, value, onChange, placeholder, institutionSlug }: {
  label: string; hint?: string; value: string; onChange: (url: string) => void; placeholder: string; institutionSlug?: string;
}) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadPlatformImage(file, institutionSlug),
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
  const { data: staff = [], isLoading: staffLoading } = useQuery({
    queryKey: ["institution-staff", id],
    queryFn: () => getInstitutionStaff(id),
    enabled: tab === "Admins",
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

  const memberPolicyMutation = useMutation({
    mutationFn: (policy: "ApprovedOnly" | "DuesRequired") => updateInstitutionMemberPolicy(id, policy),
    onSuccess: (updated) => {
      toast.success(updated.memberActivePolicy === "DuesRequired" ? "Dues payment now required for active status" : "Dues payment no longer required for active status");
      invalidate();
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const [inviteOpen, setInviteOpen] = useState(false);
  const [inviteForm, setInviteForm] = useState({ firstName: "", lastName: "", email: "", role: "Admin" });
  const [inviteError, setInviteError] = useState<string | null>(null);
  const inviteMutation = useMutation({
    mutationFn: () => inviteInstitutionStaff(id, inviteForm),
    onSuccess: (created) => {
      toast.success(`Invited ${created.email} — they'll get an email to set their password.`);
      queryClient.invalidateQueries({ queryKey: ["institution-staff", id] });
      setInviteOpen(false);
      setInviteForm({ firstName: "", lastName: "", email: "", role: "Admin" });
      setInviteError(null);
    },
    onError: (e) => {
      const msg = handleApiError(e);
      setInviteError(msg);
      toast.error(msg);
    },
  });

  const toggleStaffMutation = useMutation({
    mutationFn: ({ staffId, isDisabled }: { staffId: string; isDisabled: boolean }) =>
      setInstitutionStaffDisabled(id, staffId, isDisabled),
    onSuccess: (updated) => {
      toast.success(updated.isDisabled ? `${updated.email} disabled` : `${updated.email} re-enabled`);
      queryClient.invalidateQueries({ queryKey: ["institution-staff", id] });
    },
    onError: (e) => toast.error(handleApiError(e)),
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
  const [heroImageUrl, setHeroImageUrl] = useState<string | null>(null);
  const [heroHeadline, setHeroHeadline] = useState<string | null>(null);
  const [contentError, setContentError] = useState<string | null>(null);
  const contentMutation = useMutation({
    mutationFn: () => updateInstitutionLandingContent(id, stories ?? [], banner?.enabled ? banner : null, heroImageUrl || undefined, heroHeadline || undefined),
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
  if (heroImageUrl === null) {
    setHeroImageUrl(inst.heroImageUrl ?? "");
  }
  if (heroHeadline === null) {
    setHeroHeadline(inst.heroHeadline ?? "");
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
                {inst.customDomain ?? inst.memberPortalUrl.replace(/^https?:\/\//, "")} &middot; {inst.memberCount.toLocaleString()} members
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
          <Card className="lg:col-span-2">
            <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Active member policy</p></div>
            <CardContent className="p-5">
              <div className="flex items-center justify-between gap-4">
                <div>
                  <p className="text-[13px] font-semibold">Require dues payment for active status</p>
                  <p className="text-[12px] text-muted-foreground mt-0.5">
                    Off (default) — any approved member is active regardless of dues paid. On — a member is only active once current and past dues are paid. Normally the institution's own choice, editable from their own settings too.
                  </p>
                </div>
                <button
                  type="button"
                  role="switch"
                  aria-checked={inst.memberActivePolicy === "DuesRequired"}
                  disabled={memberPolicyMutation.isPending}
                  onClick={() => memberPolicyMutation.mutate(inst.memberActivePolicy === "DuesRequired" ? "ApprovedOnly" : "DuesRequired")}
                  className={`relative inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors disabled:opacity-60 ${inst.memberActivePolicy === "DuesRequired" ? "bg-primary" : "bg-muted"}`}
                >
                  <span className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow-lg transition-transform ${inst.memberActivePolicy === "DuesRequired" ? "translate-x-5" : "translate-x-0"}`} />
                </button>
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {tab === "Branding" && branding && (
        <div className="space-y-4">
          <Card>
            <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Identity</p></div>
            <CardContent className="p-5 space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
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
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <ImageUrlField
                  label="Logo URL"
                  value={branding.logoUrl}
                  onChange={(url) => setBranding((b) => ({ ...b!, logoUrl: url }))}
                  placeholder="https://…/logo.svg"
                  institutionSlug={inst.slug}
                />
                <ImageUrlField
                  label="Icon URL"
                  hint="Small square icon — used for the browser tab and app icon, separate from the logo."
                  value={branding.iconUrl}
                  onChange={(url) => setBranding((b) => ({ ...b!, iconUrl: url }))}
                  placeholder="https://…/icon.png"
                  institutionSlug={inst.slug}
                />
              </div>
            </CardContent>
          </Card>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
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
          </div>

          <FormError message={brandingError} />
          <Button onClick={() => brandingMutation.mutate()} disabled={brandingMutation.isPending}>
            {brandingMutation.isPending ? "Saving…" : "Save branding"}
          </Button>
        </div>
      )}

      {tab === "Features" && disabledFeatures && (
        <Card className="max-w-[1100px]">
          <div className="px-5 py-4 border-b border-border">
            <p className="text-[14px] font-semibold">Feature access</p>
            <p className="text-[12px] text-muted-foreground mt-0.5">
              All features are enabled by default. Turning one off removes it from both this institution&apos;s staff and member portals — enforced on the backend too, not just hidden.
            </p>
          </div>
          <CardContent className="p-5">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              {featureCatalog.map((f) => {
                const enabled = !disabledFeatures.has(f.key);
                return (
                  <div key={f.key} className="flex items-center justify-between gap-3 px-4 py-3.5 border border-border rounded-lg">
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
            </div>
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
        <div className="space-y-4">
          <Card>
            <div className="px-5 py-4 border-b border-border">
              <p className="text-[14px] font-semibold">Hero photo</p>
              <p className="text-[12px] text-muted-foreground mt-0.5">The large photo and headline at the top of the Member Portal landing page. This institution&apos;s own admins can also edit this.</p>
            </div>
            <CardContent className="p-5 space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <ImageUrlField
                  label="Hero photo"
                  value={heroImageUrl ?? ""}
                  onChange={setHeroImageUrl}
                  placeholder="https://…"
                  institutionSlug={inst.slug}
                />
                <div className="space-y-1.5">
                  <Label>Headline</Label>
                  <Textarea rows={3} value={heroHeadline ?? ""} onChange={(e) => setHeroHeadline(e.target.value)} placeholder="One network. Every graduate, wherever they are." />
                </div>
              </div>
            </CardContent>
          </Card>

          <div className="grid grid-cols-1 lg:grid-cols-[380px_1fr] gap-4 items-start">
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
                <div className="space-y-3">
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
              <CardContent className="p-5">
                {stories.length === 0 && (
                  <p className="text-[13px] text-muted-foreground">No custom stories — the landing page falls back to built-in default copy.</p>
                )}
                <div className="grid grid-cols-1 xl:grid-cols-2 gap-5">
                  {stories.map((story, i) => (
                    <div key={i} className="space-y-3 pb-5 border-b border-border xl:border-b-0 xl:border xl:rounded-lg xl:p-4 last:border-b-0 last:pb-0">
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
                        institutionSlug={inst.slug}
                      />
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          </div>

          <FormError message={contentError} />
          <Button onClick={() => contentMutation.mutate()} disabled={contentMutation.isPending}>
            {contentMutation.isPending ? "Saving…" : "Save content"}
          </Button>
        </div>
      )}

      {tab === "Admins" && (
        <Card>
          <div className="px-5 py-4 border-b border-border flex items-center justify-between">
            <div>
              <p className="text-[14px] font-semibold">Institution admins</p>
              <p className="text-[12px] text-muted-foreground mt-0.5">Staff who can sign in to this institution&apos;s own admin portal.</p>
            </div>
            <Button size="sm" onClick={() => setInviteOpen(true)}>
              <Plus size={14} className="mr-1.5" />
              Invite admin
            </Button>
          </div>
          <CardContent className="p-0">
            {staffLoading ? (
              <p className="px-5 py-6 text-[13px] text-muted-foreground">Loading…</p>
            ) : staff.length === 0 ? (
              <p className="px-5 py-6 text-[13px] text-muted-foreground">No admins yet — invite one to get started.</p>
            ) : (
              staff.map((s) => (
                <div key={s.id} className="flex items-center justify-between px-5 py-3.5 border-b border-border last:border-b-0">
                  <div className="flex items-center gap-3 min-w-0">
                    <UserAvatar name={`${s.firstName} ${s.lastName}`} size="sm" />
                    <div className="min-w-0">
                      <p className="text-[13.5px] font-semibold truncate">{s.firstName} {s.lastName}</p>
                      <p className="text-[12px] text-muted-foreground truncate">{s.role} &middot; {s.email}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 shrink-0">
                    <Badge variant={s.isDisabled ? "destructive" : "success"}>{s.isDisabled ? "Disabled" : "Active"}</Badge>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={toggleStaffMutation.isPending}
                      onClick={() => toggleStaffMutation.mutate({ staffId: s.id, isDisabled: !s.isDisabled })}
                    >
                      {s.isDisabled ? "Re-enable" : "Disable"}
                    </Button>
                  </div>
                </div>
              ))
            )}
          </CardContent>
        </Card>
      )}

      {tab === "Payments" && payments && (
        <div className="space-y-4">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 items-start">
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
                <SettlementAccountFields value={payments} onChange={(next) => setPayments((p) => ({ ...p!, ...next }))} />
                {inst.paystackSubaccountCode && (
                  <p className="text-[12px] text-muted-foreground">Payment subaccount ID: <span className="font-mono">{inst.paystackSubaccountCode}</span></p>
                )}
              </CardContent>
            </Card>
          </div>

          <FormError message={paymentsError} />
          <Button onClick={() => paymentsMutation.mutate()} disabled={paymentsMutation.isPending}>
            {paymentsMutation.isPending ? "Saving…" : "Save payments"}
          </Button>
        </div>
      )}

      <Dialog open={inviteOpen} onOpenChange={(o) => { setInviteOpen(o); if (!o) setInviteError(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Invite admin</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 mt-2">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>First name</Label>
                <Input value={inviteForm.firstName} onChange={(e) => setInviteForm((f) => ({ ...f, firstName: e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>Last name</Label>
                <Input value={inviteForm.lastName} onChange={(e) => setInviteForm((f) => ({ ...f, lastName: e.target.value }))} />
              </div>
            </div>
            <div className="space-y-1.5">
              <Label>Email</Label>
              <Input type="email" value={inviteForm.email} onChange={(e) => setInviteForm((f) => ({ ...f, email: e.target.value }))} />
            </div>
            <div className="space-y-1.5">
              <Label>Role</Label>
              <FormSelect
                value={inviteForm.role}
                onValueChange={(role) => setInviteForm((f) => ({ ...f, role }))}
                options={[
                  { value: "Admin", label: "Admin" },
                  { value: "SuperAdmin", label: "SuperAdmin" },
                ]}
              />
            </div>
            <p className="text-[12px] text-muted-foreground">
              They&apos;ll get an email with a link to set their own password — no temp password to relay.
            </p>
            <FormError message={inviteError} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setInviteOpen(false)}>Cancel</Button>
            <Button
              onClick={() => inviteMutation.mutate()}
              disabled={inviteMutation.isPending || !inviteForm.firstName || !inviteForm.lastName || !inviteForm.email}
            >
              {inviteMutation.isPending ? "Inviting…" : "Send invite"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
