"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Lock, Bell, Shield, Eye, EyeOff, Loader2, CheckCircle2, AlertCircle, Globe, Building2, Megaphone, Plus, Trash2, Copy,
} from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Avatar, AvatarFallback } from "@alumni/ui";
import { FormError } from "@alumni/ui";
import { getInitials, cn } from "@alumni/ui";
import { BrandPreview } from "@alumni/ui";
import { MultiImageUpload } from "@alumni/ui";
import {
  getStaffProfile, changeStaffPassword, getInstitutionProfile, updateLandingContent, updateMemberActivePolicy,
  uploadImage, STORY_ICON_OPTIONS, type LandingPageStory, type NewsBanner,
  getAdminNotificationPreferences, updateAdminNotificationPreferences, type AdminNotificationPreferences,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import { useHostname } from "@/hooks/use-hostname";
import { toast } from "sonner";

const EMPTY_STORY: LandingPageStory = { icon: "Briefcase", eyebrow: "", scenario: "", description: "", imageUrl: "" };
const EMPTY_BANNER: NewsBanner = { enabled: false, text: "", linkText: "", linkUrl: "" };

const defaultNotifPrefs = {
  newMemberRegistrations: true,
  pendingApprovals: true,
  newContributions: false,
  systemAlerts: true,
};

type NotifPrefs = typeof defaultNotifPrefs;

function toNotifPrefs(dto: AdminNotificationPreferences): NotifPrefs {
  return {
    newMemberRegistrations: dto.newMemberRegistrationAlerts,
    pendingApprovals: dto.pendingApprovalAlerts,
    newContributions: dto.paymentReceivedAlerts,
    systemAlerts: dto.systemAlerts,
  };
}

function fromNotifPrefs(prefs: NotifPrefs) {
  return {
    newMemberRegistrationAlerts: prefs.newMemberRegistrations,
    pendingApprovalAlerts: prefs.pendingApprovals,
    paymentReceivedAlerts: prefs.newContributions,
    systemAlerts: prefs.systemAlerts,
  };
}

function Toggle({ checked, onChange, label, description }: { checked: boolean; onChange: (v: boolean) => void; label: string; description?: string }) {
  return (
    <div className="flex items-start justify-between gap-4 py-4">
      <div>
        <p className="text-sm font-semibold text-foreground">{label}</p>
        {description && <p className="text-[12px] text-muted-foreground mt-0.5">{description}</p>}
      </div>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className={cn(
          "relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-all duration-200 ease-[cubic-bezier(0.2,0,0,1)] active:scale-[0.96] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary",
          checked ? "bg-primary" : "bg-muted"
        )}
      >
        <span
          className={cn(
            "pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow-lg transition-transform duration-200 ease-[cubic-bezier(0.2,0,0,1)]",
            checked ? "translate-x-5" : "translate-x-0"
          )}
        />
      </button>
    </div>
  );
}

const TABS = ["Institution profile", "Landing content", "Domain", "Notifications", "Security"] as const;

export default function BrandingSettingsPage() {
  const { user, logout, isScopedAdmin } = useAuth();
  const [tab, setTab] = useState<(typeof TABS)[number]>("Institution profile");
  const queryClient = useQueryClient();

  const [pwForm, setPwForm] = useState({ currentPassword: "", newPassword: "", confirm: "" });
  const [showPw, setShowPw] = useState(false);
  const [notifSaved, setNotifSaved] = useState(false);

  const { data: profile } = useQuery({
    queryKey: ["admin-profile"],
    queryFn: getStaffProfile,
  });

  const { data: institution } = useQuery({
    queryKey: ["institution-profile"],
    queryFn: getInstitutionProfile,
  });

  const [stories, setStories] = useState<LandingPageStory[] | null>(null);
  const [banner, setBanner] = useState<NewsBanner | null>(null);
  const [heroHeadline, setHeroHeadline] = useState<string | null>(null);
  const [heroImageFiles, setHeroImageFiles] = useState<File[]>([]);
  const [heroExistingImageUrls, setHeroExistingImageUrls] = useState<string[] | null>(null);
  const [contentError, setContentError] = useState<string | null>(null);
  const contentMutation = useMutation({
    mutationFn: async () => {
      const uploaded = await Promise.all(heroImageFiles.map((f) => uploadImage(f)));
      const heroImageUrls = [...(heroExistingImageUrls ?? []), ...uploaded.map((u) => u.url)];
      return updateLandingContent(stories ?? [], banner?.enabled ? banner : null, heroImageUrls, heroHeadline || undefined);
    },
    onSuccess: () => {
      toast.success("Landing content updated");
      queryClient.invalidateQueries({ queryKey: ["institution-profile"] });
      setContentError(null);
      setHeroImageFiles([]);
    },
    onError: (e) => {
      const msg = handleApiError(e);
      setContentError(msg);
      toast.error(msg);
    },
  });
  if (institution && stories === null) setStories(institution.landingPageStories);
  if (institution && banner === null) setBanner(institution.newsBanner ?? EMPTY_BANNER);
  if (institution && heroHeadline === null) setHeroHeadline(institution.heroHeadline ?? "");
  if (institution && heroExistingImageUrls === null) setHeroExistingImageUrls(institution.heroImageUrls ?? []);

  const policyMutation = useMutation({
    mutationFn: (policy: "DuesRequired" | "ApprovedOnly") => updateMemberActivePolicy(policy),
    onSuccess: () => {
      toast.success("Active-member policy updated");
      queryClient.invalidateQueries({ queryKey: ["institution-profile"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const pwMut = useMutation({
    mutationFn: () => changeStaffPassword({ currentPassword: pwForm.currentPassword, newPassword: pwForm.newPassword }),
    onSuccess: () => {
      setPwForm({ currentPassword: "", newPassword: "", confirm: "" });
      toast.success("Password updated successfully");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function handlePwSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (pwForm.newPassword !== pwForm.confirm) {
      toast.error("Passwords don't match");
      return;
    }
    if (pwForm.newPassword.length < 8) {
      toast.error("New password must be at least 8 characters");
      return;
    }
    pwMut.mutate();
  }

  const { data: notifPrefsDto } = useQuery({
    queryKey: ["admin-notification-preferences"],
    queryFn: getAdminNotificationPreferences,
  });

  const notifPrefs: NotifPrefs = notifPrefsDto ? toNotifPrefs(notifPrefsDto) : defaultNotifPrefs;

  const notifMutation = useMutation({
    mutationFn: (prefs: NotifPrefs) => updateAdminNotificationPreferences(fromNotifPrefs(prefs)),
    onSuccess: (dto) => {
      queryClient.setQueryData(["admin-notification-preferences"], dto);
      setNotifSaved(true);
      setTimeout(() => setNotifSaved(false), 2500);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function saveNotifPrefs(prefs: NotifPrefs) {
    notifMutation.mutate(prefs);
  }

  const fullName = profile
    ? `${profile.firstName} ${profile.lastName}`
    : (user?.name ?? "Administrator");

  const roleLabel = profile?.role ?? user?.role ?? "Admin";
  const tenantHost = useHostname();

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto">
      <header className="flex items-end justify-between gap-4 mb-6 flex-wrap animate-in fade-in slide-in-from-bottom-3 duration-700">
        <div className="space-y-1">
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Branding &amp; Settings</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">Manage the identity and operating preferences for this institution.</p>
        </div>
      </header>

      <div className="flex gap-6 border-b border-border mb-6 overflow-x-auto">
        {TABS.map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={cn(
              "pb-3 text-[13.5px] font-medium whitespace-nowrap border-b-2 -mb-px transition-colors",
              tab === t ? "text-primary border-primary font-semibold" : "text-muted-foreground border-transparent hover:text-foreground"
            )}
          >
            {t}
          </button>
        ))}
      </div>

      {tab === "Institution profile" && (
        <div className="grid grid-cols-1 lg:grid-cols-[1.4fr_1fr] gap-4 items-start">
          <Card className="border-border/40">
            <CardContent className="p-6 space-y-4">
              <div className="flex items-center gap-2 mb-1">
                <Building2 size={16} className="text-primary" />
                <p className="font-semibold text-[15px]">Institution profile</p>
                <Lock size={13} className="text-muted-foreground" aria-hidden="true" />
              </div>
              <p className="text-[12.5px] text-muted-foreground -mt-2">These details appear across your member-facing portals.</p>
              <div className="flex items-start gap-2 rounded-lg border border-border/60 bg-muted/50 px-3 py-2.5">
                <Lock size={14} className="text-muted-foreground shrink-0 mt-0.5" aria-hidden="true" />
                <p className="text-[12px] text-muted-foreground leading-relaxed">
                  These fields are locked. Branding, portal titles, and login page content are managed by the platform team — contact support to request a change.
                </p>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
                <div className="space-y-1.5">
                  <Label className="text-[13px] font-semibold">Display name</Label>
                  <Input value={institution?.name ?? ""} disabled />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-[13px] font-semibold">Portal name</Label>
                  <Input value={institution?.portalName ?? ""} disabled />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-[13px] font-semibold">Contact email</Label>
                  <Input value={institution?.contactEmail ?? ""} disabled />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-[13px] font-semibold">Tagline</Label>
                  <Input value={institution?.tagline ?? ""} disabled />
                </div>
              </div>
              <div className="space-y-1.5">
                <Label className="text-[13px] font-semibold">Primary color</Label>
                <div className="flex items-center gap-2">
                  <span className="w-7 h-7 rounded-md border border-border" style={{ backgroundColor: institution?.primaryColorHex ?? "#2563eb" }} />
                  <Input value={institution?.primaryColorHex ?? ""} disabled className="w-[140px]" />
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="border-border/40 h-fit">
            <CardContent className="p-5">
              <p className="font-semibold text-[14px] mb-1">Brand preview</p>
              <p className="text-[12px] text-muted-foreground mb-3">How your primary color reads across the portal.</p>
              <BrandPreview color={institution?.primaryColorHex ?? "#2563eb"} name={institution?.portalName || institution?.name || "Institution"} />
            </CardContent>
          </Card>

          <Card className="border-border/40 lg:col-span-2">
            <CardContent className="p-6">
              <div className="flex items-center gap-2 mb-1">
                <Shield size={16} className="text-primary" />
                <p className="font-semibold text-[15px]">Active member policy</p>
              </div>
              {isScopedAdmin ? (
                <p className="text-muted-foreground mt-2">Only Admin and SuperAdmin users can manage the active member policy.</p>
              ) : (
                <>
                  <p className="text-[12.5px] text-muted-foreground -mt-1">
                    Decide what makes a member &quot;active&quot; across both portals — this changes the messages and restrictions members see.
                  </p>
                  <Toggle
                    checked={institution?.memberActivePolicy === "DuesRequired"}
                    onChange={(checked) => policyMutation.mutate(checked ? "DuesRequired" : "ApprovedOnly")}
                    label="Require dues payment for active status"
                    description={
                      institution?.memberActivePolicy === "ApprovedOnly"
                        ? "Off — any approved member is active regardless of dues paid."
                        : "On — a member is active only once current and past dues are paid."
                    }
                  />
                </>
              )}
            </CardContent>
          </Card>
        </div>
      )}

      {tab === "Landing content" && isScopedAdmin && (
        <Card className="border-border/40">
          <CardContent className="p-6">
            <div className="flex items-center gap-2 mb-1">
              <Globe size={16} className="text-primary" />
              <p className="font-semibold text-[15px]">Landing content</p>
            </div>
            <p className="text-muted-foreground mt-2">Only Admin and SuperAdmin users can manage landing page content.</p>
          </CardContent>
        </Card>
      )}

      {tab === "Landing content" && !isScopedAdmin && stories && banner && (
        <div className="space-y-4">
          <Card className="border-border/40">
            <CardContent className="p-6 space-y-4">
              <div className="flex items-center gap-2 mb-1">
                <Globe size={16} className="text-primary" />
                <p className="font-semibold text-[15px]">Hero photo</p>
              </div>
              <p className="text-[12.5px] text-muted-foreground -mt-2">The photo(s) and headline at the top of your Member Portal landing page. Add more than one to show a carousel. Leave empty for the generic default.</p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label>Photos</Label>
                  <MultiImageUpload
                    files={heroImageFiles}
                    existingUrls={heroExistingImageUrls ?? []}
                    onAddFile={(f) => setHeroImageFiles((prev) => [...prev, f])}
                    onRemoveFile={(i) => setHeroImageFiles((prev) => prev.filter((_, idx) => idx !== i))}
                    onRemoveExisting={(i) => setHeroExistingImageUrls((prev) => (prev ?? []).filter((_, idx) => idx !== i))}
                    label="Upload hero photos"
                  />
                </div>
                <div className="space-y-1.5">
                  <Label>Headline</Label>
                  <Textarea
                    rows={3}
                    value={heroHeadline ?? ""}
                    onChange={(e) => setHeroHeadline(e.target.value)}
                    placeholder="One network. Every graduate, wherever they are."
                  />
                </div>
              </div>
            </CardContent>
          </Card>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 items-start">
            <Card className="border-border/40">
              <CardContent className="p-6 space-y-4">
                <div className="flex items-center gap-2 mb-1">
                  <Megaphone size={16} className="text-primary" />
                  <p className="font-semibold text-[15px]">News banner</p>
                </div>
                <p className="text-[12.5px] text-muted-foreground -mt-2">The dismissible strip at the top of your Member Portal landing page.</p>
                <div className="flex items-center justify-between pt-1">
                  <p className="text-[13px] font-semibold">Show banner</p>
                  <button
                    type="button"
                    role="switch"
                    aria-checked={banner.enabled}
                    onClick={() => setBanner((b) => ({ ...b!, enabled: !b!.enabled }))}
                    className={cn("relative inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors", banner.enabled ? "bg-primary" : "bg-muted")}
                  >
                    <span className={cn("pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow-lg transition-transform", banner.enabled ? "translate-x-5" : "translate-x-0")} />
                  </button>
                </div>
                <div className="space-y-1.5">
                  <Label>Banner text</Label>
                  <Textarea rows={2} value={banner.text} onChange={(e) => setBanner((b) => ({ ...b!, text: e.target.value }))} placeholder="A new Vice Chancellor has been appointed — effective this year." />
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
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

            <Card className="border-border/40">
              <div className="px-6 pt-5 pb-1 flex items-center justify-between">
                <div>
                  <p className="font-semibold text-[15px]">Stories</p>
                  <p className="text-[12.5px] text-muted-foreground mt-0.5">The &quot;why alumni join&quot; cards on your landing page. Leave empty for generic default copy.</p>
                </div>
                <Button variant="outline" size="sm" onClick={() => setStories((s) => [...(s ?? []), { ...EMPTY_STORY }])}>
                  <Plus size={14} className="mr-1.5" /> Add story
                </Button>
              </div>
              <CardContent className="p-6 space-y-5">
                {stories.length === 0 && (
                  <p className="text-[13px] text-muted-foreground">No custom stories — your landing page falls back to built-in default copy.</p>
                )}
                {stories.map((story, i) => (
                  <div key={i} className="space-y-3 pb-5 border-b border-border/40 last:border-0 last:pb-0">
                    <div className="flex items-center justify-between">
                      <p className="text-[12px] font-bold text-muted-foreground uppercase tracking-wide">Story {i + 1}</p>
                      <button type="button" onClick={() => setStories((s) => s!.filter((_, idx) => idx !== i))} className="text-destructive hover:opacity-70">
                        <Trash2 size={14} />
                      </button>
                    </div>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
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
                    <div className="space-y-1.5">
                      <Label>Image URL</Label>
                      <Input value={story.imageUrl ?? ""} onChange={(e) => setStories((s) => s!.map((it, idx) => (idx === i ? { ...it, imageUrl: e.target.value } : it)))} placeholder="https://…" />
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>

          <FormError message={contentError} />
          <Button onClick={() => contentMutation.mutate()} disabled={contentMutation.isPending}>
            {contentMutation.isPending ? "Saving…" : "Save content"}
          </Button>
        </div>
      )}

      {tab === "Domain" && (
        <Card className="border-border/40 max-w-2xl">
          <CardContent className="p-6">
            <div className="flex items-center gap-2 mb-1">
              <Globe size={16} className="text-primary" />
              <p className="font-semibold text-[15px]">Domain</p>
            </div>
            <p className="text-[12.5px] text-muted-foreground mb-4">Where staff and members reach this workspace.</p>

            <div className="flex items-center justify-between border-t border-border py-3">
              <div>
                <p className="font-mono text-[13px] font-semibold">{tenantHost ?? "Loading…"}</p>
                <p className="text-[12px] text-muted-foreground mt-0.5">Institution staff portal &middot; this page &middot; always active, cannot be removed</p>
              </div>
              <Badge variant="success">Live</Badge>
            </div>

            <div className="flex items-center justify-between border-t border-border py-3 gap-3">
              <div className="min-w-0">
                <p className="font-mono text-[13px] font-semibold truncate">
                  {institution?.memberPortalUrl?.replace(/^https?:\/\//, "") ?? "Not configured"}
                </p>
                <p className="text-[12px] text-muted-foreground mt-0.5">Member portal &middot; share this with your alumni</p>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                {institution?.memberPortalUrl && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={async () => {
                      await navigator.clipboard.writeText(institution.memberPortalUrl!);
                      toast.success("Member portal link copied");
                    }}
                  >
                    <Copy size={14} className="mr-1.5" />
                    Copy link
                  </Button>
                )}
                <Badge variant={institution?.memberPortalUrl ? "success" : "warning"}>
                  {institution?.memberPortalUrl ? "Live" : "Not configured"}
                </Badge>
              </div>
            </div>

          </CardContent>
        </Card>
      )}

      {tab === "Notifications" && (
        <Card className="border-border/40 max-w-2xl">
          <CardContent className="p-6">
            <div className="flex items-center gap-2 mb-1">
              <Bell size={16} className="text-primary" />
              <p className="font-semibold text-[15px]">Notifications</p>
              {notifMutation.isPending && (
                <span className="ml-auto flex items-center gap-1 text-[11px] text-muted-foreground font-medium">
                  <Loader2 size={13} className="animate-spin" /> Saving…
                </span>
              )}
              {!notifMutation.isPending && notifSaved && (
                <span className="ml-auto flex items-center gap-1 text-[11px] text-success font-bold animate-in fade-in duration-300">
                  <CheckCircle2 size={13} /> Saved
                </span>
              )}
            </div>
            <p className="text-[12.5px] text-muted-foreground mb-2">Choose what platform events trigger alerts.</p>
            <div className={cn("divide-y divide-border/40", notifMutation.isPending && "opacity-60 pointer-events-none")}>
              <Toggle
                checked={notifPrefs.newMemberRegistrations}
                onChange={(v) => saveNotifPrefs({ ...notifPrefs, newMemberRegistrations: v })}
                label="New Registrations"
                description="Get notified when a new member registers on the platform"
              />
              <Toggle
                checked={notifPrefs.pendingApprovals}
                onChange={(v) => saveNotifPrefs({ ...notifPrefs, pendingApprovals: v })}
                label="Pending Approvals"
                description="Daily summary of members awaiting approval"
              />
              <Toggle
                checked={notifPrefs.newContributions}
                onChange={(v) => saveNotifPrefs({ ...notifPrefs, newContributions: v })}
                label="New Contributions"
                description="Notify when a contribution is confirmed"
              />
              <Toggle
                checked={notifPrefs.systemAlerts}
                onChange={(v) => saveNotifPrefs({ ...notifPrefs, systemAlerts: v })}
                label="System Alerts"
                description="Critical platform alerts and errors"
              />
            </div>
          </CardContent>
        </Card>
      )}

      {tab === "Security" && (
        <div className="grid grid-cols-1 lg:grid-cols-[1fr_.8fr] gap-4">
          <Card className="border-border/40">
            <CardContent className="p-6">
              <div className="flex items-center gap-2 mb-4">
                <Lock size={16} className="text-primary" />
                <p className="font-semibold text-[15px]">Security</p>
              </div>
              <form onSubmit={handlePwSubmit} className="space-y-4 max-w-sm">
                <div className="space-y-1.5">
                  <Label htmlFor="currentPw" className="text-[13px] font-semibold">Current Password</Label>
                  <div className="relative">
                    <Input
                      id="currentPw"
                      type={showPw ? "text" : "password"}
                      placeholder="••••••••"
                      value={pwForm.currentPassword}
                      onChange={(e) => setPwForm((f) => ({ ...f, currentPassword: e.target.value }))}
                      className="h-11 pr-11"
                      required
                    />
                    <button type="button" onClick={() => setShowPw(!showPw)}
                      className="absolute right-0 top-0 h-full w-11 flex items-center justify-center text-muted-foreground hover:text-foreground">
                      {showPw ? <EyeOff size={16} /> : <Eye size={16} />}
                    </button>
                  </div>
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="newPw" className="text-[13px] font-semibold">New Password</Label>
                  <Input
                    id="newPw"
                    type={showPw ? "text" : "password"}
                    placeholder="Min. 8 characters"
                    value={pwForm.newPassword}
                    onChange={(e) => setPwForm((f) => ({ ...f, newPassword: e.target.value }))}
                    className="h-11"
                    required
                    minLength={8}
                  />
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="confirmPw" className="text-[13px] font-semibold">Confirm New Password</Label>
                  <Input
                    id="confirmPw"
                    type={showPw ? "text" : "password"}
                    placeholder="Repeat new password"
                    value={pwForm.confirm}
                    onChange={(e) => setPwForm((f) => ({ ...f, confirm: e.target.value }))}
                    className={cn("h-11", pwForm.confirm && pwForm.confirm !== pwForm.newPassword && "border-destructive")}
                    required
                  />
                  {pwForm.confirm && pwForm.confirm !== pwForm.newPassword && (
                    <p className="text-[12px] text-destructive flex items-center gap-1 animate-in fade-in">
                      <AlertCircle size={12} /> Passwords do not match
                    </p>
                  )}
                </div>

                <Button
                  type="submit"
                  size="sm"
                  className="h-10"
                  disabled={pwMut.isPending || !pwForm.currentPassword || !pwForm.newPassword || !pwForm.confirm}
                >
                  {pwMut.isPending ? <Loader2 size={14} className="animate-spin mr-2" /> : null}
                  Update Password
                </Button>
              </form>
            </CardContent>
          </Card>

          <Card className="border-border/40 h-fit">
            <CardContent className="p-5 flex items-center gap-4">
              <Avatar size="lg" className="ring-2 ring-primary/20">
                <AvatarFallback name={fullName} className="text-[16px]">{getInitials(fullName)}</AvatarFallback>
              </Avatar>
              <div className="min-w-0">
                <p className="text-[15px] font-bold truncate">{fullName}</p>
                <p className="text-[12.5px] text-muted-foreground truncate">{profile?.email ?? user?.email ?? "—"}</p>
                <div className="flex items-center gap-2 mt-2">
                  <Badge variant="info" className="text-[10px]">{roleLabel}</Badge>
                  <Badge variant="secondary" className="text-[10px]">
                    <Shield size={10} className="mr-1" />
                    Institution Portal
                  </Badge>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Account-ending action — the one card here that shouldn't blend in
              with routine preference cards, so it gets its own tint. */}
          <Card className="h-fit" style={{ borderColor: "color-mix(in oklch, var(--destructive) 30%, transparent)", background: "color-mix(in oklch, var(--destructive) 4%, var(--card))" }}>
            <CardContent className="p-5 flex items-center justify-between gap-4">
              <div>
                <p className="text-[14px] font-semibold">Log out</p>
                <p className="text-[12.5px] text-muted-foreground mt-0.5">Sign out of your staff account on this device.</p>
              </div>
              <Button variant="outline" size="sm" className="shrink-0 text-destructive border-destructive/40 hover:bg-destructive/10 hover:text-destructive active:scale-[0.97]" onClick={logout}>
                Log out
              </Button>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
