"use client";

import { useState, useEffect, useRef } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Camera, Eye, EyeOff, Loader2, Briefcase, Armchair, Award } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { UserAvatar } from "@/components/ui/user-avatar";
import { Badge } from "@/components/ui/badge";
import { getMyProfile, updateMyProfile, changePassword, getMyBadges } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@/components/ui/skeleton";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { cn } from "@/lib/utils";

/* ─────────────────────────────────────────────────────────────────────────
   EMPLOYMENT OPTION BUTTON
   ───────────────────────────────────────────────────────────────────────── */
function EmploymentOption({
  icon: Icon,
  label,
  description,
  active,
  disabled,
  onClick,
}: {
  icon: React.ElementType;
  label: string;
  description: string;
  active: boolean;
  disabled: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className={cn(
        "relative flex flex-col items-center gap-2 rounded-xl border-2 p-5 text-center transition-colors",
        active   ? "border-primary"      : "border-border",
        !disabled && !active && "hover:border-primary/40",
        disabled && "cursor-not-allowed opacity-50",
      )}
      style={active ? { background: "var(--color-background-info)" } : { background: "var(--background)" }}
    >
      <Icon size={22} style={{ color: active ? "var(--primary)" : "var(--muted-foreground)" }} />
      <span
        className="text-[13.5px] font-semibold"
        style={{ color: active ? "var(--primary)" : "var(--foreground)" }}
      >
        {label}
      </span>
      <span className="text-[11.5px]" style={{ color: "var(--muted-foreground)" }}>
        {description}
      </span>
      {active && (
        <div
          className="absolute top-2.5 right-2.5 w-2 h-2 rounded-full"
          style={{ background: "var(--primary)" }}
        />
      )}
    </button>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   SECTION HEADING — consistent with other pages
   ───────────────────────────────────────────────────────────────────────── */
function SectionTitle({ children }: { children: React.ReactNode }) {
  return (
    <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
      {children}
    </p>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PAGE
   ───────────────────────────────────────────────────────────────────────── */
export default function MemberProfilePage() {
  const [profileForm, setProfileForm] = useState({
    company: "", jobTitle: "", location: "", linkedInUrl: "", bio: "", phone: "",
  });
  const [employmentStatus, setEmploymentStatus] = useState("Employed");
  const [confirmPensioner, setConfirmPensioner]  = useState(false);
  const [pwForm,  setPwForm]   = useState({ currentPassword: "", newPassword: "", confirm: "" });
  const [pwVisible, setPwVisible] = useState(false);
  const [avatarUploading, setAvatarUploading] = useState(false);
  const avatarInputRef = useRef<HTMLInputElement>(null);
  const qc = useQueryClient();

  const { data: profile, isLoading } = useQuery({
    queryKey: ["m-profile"],
    queryFn:  getMyProfile,
  });

  const { data: badges } = useQuery({
    queryKey: ["m-badges"],
    queryFn:  getMyBadges,
  });

  useEffect(() => {
    if (profile) {
      setProfileForm({
        company:     profile.company     ?? "",
        jobTitle:    profile.jobTitle    ?? "",
        location:    profile.location    ?? "",
        linkedInUrl: profile.linkedInUrl ?? "",
        bio:         profile.bio         ?? "",
        phone:       profile.phone       ?? "",
      });
      setEmploymentStatus(profile.employmentStatus ?? "Employed");
    }
  }, [profile]);

  const updateMut = useMutation({
    mutationFn: () => updateMyProfile({
      company:          profileForm.company     || undefined,
      jobTitle:         profileForm.jobTitle    || undefined,
      location:         profileForm.location    || undefined,
      linkedInUrl:      profileForm.linkedInUrl || undefined,
      bio:              profileForm.bio         || undefined,
      phone:            profileForm.phone       || undefined,
      employmentStatus,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-profile"] });
      toast.success("Profile updated.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const employmentMut = useMutation({
    mutationFn: (status: string) => updateMyProfile({ employmentStatus: status }),
    onSuccess: (_, status) => {
      setEmploymentStatus(status);
      qc.invalidateQueries({ queryKey: ["m-profile"] });
      toast.success(`Employment status updated to ${status}.`);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const pwMut = useMutation({
    mutationFn: () => {
      if (pwForm.newPassword !== pwForm.confirm) throw new Error("Passwords do not match");
      return changePassword(pwForm.currentPassword, pwForm.newPassword);
    },
    onSuccess: () => {
      setPwForm({ currentPassword: "", newPassword: "", confirm: "" });
      toast.success("Password changed.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (isLoading || !profile) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 space-y-6 max-w-2xl mx-auto">
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 space-y-5 max-w-2xl mx-auto">

      {/* ── Page heading ── */}
      <div>
        <h1
          className="font-[family-name:var(--font-display)] tracking-tight"
          style={{ fontSize: "1.4rem", fontWeight: 700, color: "var(--foreground)" }}
        >
          My profile
        </h1>
        <p className="text-[13.5px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
          Update your information visible to fellow alumni.
        </p>
      </div>

      {/* ── Identity card ── */}
      <Card>
        <CardContent className="p-4 sm:p-5 flex flex-col sm:flex-row items-start sm:items-center gap-4">
          {/* Avatar with upload */}
          <div className="relative shrink-0">
            <input
              ref={avatarInputRef}
              type="file"
              accept="image/*"
              className="hidden"
              onChange={async e => {
                const file = e.target.files?.[0];
                if (!file) return;
                setAvatarUploading(true);
                try {
                  await updateMyProfile({ profilePicture: file });
                  qc.invalidateQueries({ queryKey: ["m-profile"] });
                  toast.success("Profile picture updated.");
                } catch (err) {
                  toast.error(handleApiError(err));
                } finally {
                  setAvatarUploading(false);
                  if (avatarInputRef.current) avatarInputRef.current.value = "";
                }
              }}
            />
            <UserAvatar
              src={profile.profilePictureUrl}
              name={`${profile.firstName} ${profile.lastName}`}
              size="xl"
            />
            <Button
              size="icon"
              variant="secondary"
              className="absolute -bottom-1 -right-1 h-7 w-7 rounded-full"
              disabled={avatarUploading}
              onClick={() => avatarInputRef.current?.click()}
            >
              {avatarUploading
                ? <Loader2 size={12} className="animate-spin" />
                : <Camera size={12} />}
            </Button>
          </div>

          {/* Name + meta */}
          <div>
            <p className="text-[17px] font-semibold" style={{ color: "var(--foreground)" }}>
              {profile.firstName} {profile.lastName}
            </p>
            <p className="text-[13.5px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
              {profile.email}
            </p>
            <div className="flex flex-wrap items-center gap-2 mt-2">
              <Badge variant="outline" className="text-[11px] font-semibold">
                Class of {profile.graduationYear}
              </Badge>
              <Badge
                variant={profile.status === "Active" ? "success" : "warning"}
                className="text-[11px] font-semibold"
              >
                {profile.status}
              </Badge>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* ── Badges ── */}
      {badges && badges.length > 0 && (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-[14px] flex items-center gap-2">
              <Award size={15} style={{ color: "var(--primary)" }} /> Badges
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-0">
            <div className="flex flex-wrap gap-2">
              {badges.map(b => (
                <Badge key={b.id} variant="secondary" className="gap-1.5 py-1.5 px-3 text-[12px] font-semibold">
                  🏅 {b.badgeType.replace(/([A-Z])/g, " $1").trim()}
                </Badge>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* ── Employment status ── */}
      <Card>
        <CardHeader className="pb-3">
          <SectionTitle>Employment status</SectionTitle>
        </CardHeader>
        <CardContent className="space-y-4 pt-0">
          <p className="text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
            Your status determines your membership renewal amount.
          </p>

          {employmentStatus === "Pensioner" && (
            <div
              className="rounded-xl p-3.5 text-[13px] font-medium"
              style={{
                background: "rgba(245,158,11,0.08)",
                border:     "1px solid rgba(245,158,11,0.25)",
                color:      "#92400e",
              }}
            >
              Your status is set to <strong>Pensioner</strong>. This cannot be changed back.
            </div>
          )}

          <div className="grid grid-cols-2 gap-3">
            <EmploymentOption
              icon={Briefcase}
              label="Employed"
              description="Currently working or self-employed"
              active={employmentStatus === "Employed"}
              disabled={employmentMut.isPending || employmentStatus === "Pensioner" || employmentStatus === "Employed"}
              onClick={() => {
                if (employmentStatus !== "Pensioner" && employmentStatus !== "Employed") {
                  employmentMut.mutate("Employed");
                }
              }}
            />
            <EmploymentOption
              icon={Armchair}
              label="Pensioner"
              description="Retired and receiving pension"
              active={employmentStatus === "Pensioner"}
              disabled={employmentMut.isPending || employmentStatus === "Pensioner"}
              onClick={() => {
                if (employmentStatus !== "Pensioner") setConfirmPensioner(true);
              }}
            />
          </div>

          {employmentMut.isPending && (
            <p className="text-[12.5px] animate-pulse" style={{ color: "var(--muted-foreground)" }}>
              Updating…
            </p>
          )}
        </CardContent>
      </Card>

      {/* ── Professional info ── */}
      <Card>
        <CardHeader className="pb-3">
          <SectionTitle>Professional info</SectionTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <form className="space-y-4" onSubmit={e => { e.preventDefault(); updateMut.mutate(); }}>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              {[
                { id: "company",  label: "Company",  placeholder: "Where you work"           },
                { id: "jobTitle", label: "Job title", placeholder: "Your current role"        },
                { id: "location", label: "Location",  placeholder: "City, Country"            },
                { id: "phone",    label: "Phone",     placeholder: "+233 xx xxx xxxx"         },
              ].map(({ id, label, placeholder }) => (
                <div key={id} className="space-y-1.5">
                  <Label htmlFor={id} className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    {label}
                  </Label>
                  <Input
                    id={id}
                    placeholder={placeholder}
                    value={profileForm[id as keyof typeof profileForm]}
                    onChange={e => setProfileForm(f => ({ ...f, [id]: e.target.value }))}
                    className="h-11 text-[14px]"
                  />
                </div>
              ))}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="linkedInUrl" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                LinkedIn URL
              </Label>
              <Input
                id="linkedInUrl"
                type="url"
                placeholder="https://linkedin.com/in/…"
                value={profileForm.linkedInUrl}
                onChange={e => setProfileForm(f => ({ ...f, linkedInUrl: e.target.value }))}
                className="h-11 text-[14px]"
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="bio" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                Bio
              </Label>
              <Textarea
                id="bio"
                placeholder="Tell your fellow alumni about yourself…"
                rows={3}
                value={profileForm.bio}
                onChange={e => setProfileForm(f => ({ ...f, bio: e.target.value }))}
                className="text-[14px] resize-none"
              />
            </div>
            <Button
              type="submit"
              className="font-semibold text-[13.5px] gap-2"
              style={{ height: 42 }}
              isLoading={updateMut.isPending}
              loadingText="Saving…"
            >
              Save changes
            </Button>
          </form>
        </CardContent>
      </Card>

      {/* ── Change password ── */}
      <Card>
        <CardHeader className="pb-3">
          <SectionTitle>Change password</SectionTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <form className="space-y-4" onSubmit={e => { e.preventDefault(); pwMut.mutate(); }}>
            <div className="space-y-1.5">
              <Label htmlFor="currentPassword" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                Current password
              </Label>
              <div className="relative">
                <Input
                  id="currentPassword"
                  type={pwVisible ? "text" : "password"}
                  placeholder="Current password"
                  value={pwForm.currentPassword}
                  onChange={e => setPwForm(f => ({ ...f, currentPassword: e.target.value }))}
                  className="h-11 text-[14px] pr-11"
                  required
                />
                <button
                  type="button"
                  className="absolute right-0 top-0 h-full w-11 flex items-center justify-center transition-colors"
                  style={{ color: "var(--muted-foreground)" }}
                  onClick={() => setPwVisible(v => !v)}
                  aria-label={pwVisible ? "Hide password" : "Show password"}
                >
                  {pwVisible ? <EyeOff size={16} /> : <Eye size={16} />}
                </button>
              </div>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="newPassword" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                New password
              </Label>
              <Input
                id="newPassword"
                type="password"
                placeholder="New password"
                value={pwForm.newPassword}
                onChange={e => setPwForm(f => ({ ...f, newPassword: e.target.value }))}
                className="h-11 text-[14px]"
                required
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="confirmPassword" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                Confirm new password
              </Label>
              <Input
                id="confirmPassword"
                type="password"
                placeholder="Repeat your new password"
                value={pwForm.confirm}
                onChange={e => setPwForm(f => ({ ...f, confirm: e.target.value }))}
                className="h-11 text-[14px]"
                required
              />
              {pwForm.confirm && pwForm.newPassword !== pwForm.confirm && (
                <p className="text-[12px] font-medium text-destructive animate-in fade-in slide-in-from-top-1">
                  Passwords do not match
                </p>
              )}
            </div>
            <Button
              type="submit"
              className="font-semibold text-[13.5px]"
              style={{ height: 42 }}
              isLoading={pwMut.isPending}
              loadingText="Changing…"
              disabled={!pwForm.currentPassword || !pwForm.newPassword || pwForm.newPassword !== pwForm.confirm}
            >
              Change password
            </Button>
          </form>
        </CardContent>
      </Card>

      {/* ── Confirm pensioner modal ── */}
      <ConfirmModal
        open={confirmPensioner}
        title="Switch to Pensioner?"
        message="This is permanent — you will not be able to switch back to Employed. Only confirm if you are retired and receiving a pension."
        confirmLabel="Yes, I am a pensioner"
        variant="destructive"
        isLoading={employmentMut.isPending}
        onConfirm={() => { employmentMut.mutate("Pensioner"); setConfirmPensioner(false); }}
        onCancel={() => setConfirmPensioner(false)}
      />
    </div>
  );
}
