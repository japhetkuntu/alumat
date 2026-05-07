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
import { getMyProfile, updateMyProfile, changePassword, getMyBadges, evaluateBadges } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@/components/ui/skeleton";
import { ConfirmModal } from "@/components/ui/confirm-modal";

export default function MemberProfilePage() {
  const [profileForm, setProfileForm] = useState({ company: "", jobTitle: "", location: "", linkedInUrl: "", bio: "", phone: "" });
  const [employmentStatus, setEmploymentStatus] = useState("Employed");
  const [confirmPensioner, setConfirmPensioner] = useState(false);
  const [pwForm, setPwForm] = useState({ currentPassword: "", newPassword: "", confirm: "" });
  const [pwVisible, setPwVisible] = useState(false);
  const [avatarUploading, setAvatarUploading] = useState(false);
  const avatarInputRef = useRef<HTMLInputElement>(null);
  const qc = useQueryClient();

  const { data: profile, isLoading } = useQuery({
    queryKey: ["m-profile"],
    queryFn: getMyProfile,
  });

  const { data: badges } = useQuery({
    queryKey: ["m-badges"],
    queryFn: getMyBadges,
  });

  useEffect(() => {
    if (profile) {
      setProfileForm({
        company: profile.company ?? "",
        jobTitle: profile.jobTitle ?? "",
        location: profile.location ?? "",
        linkedInUrl: profile.linkedInUrl ?? "",
        bio: profile.bio ?? "",
        phone: profile.phone ?? "",
      });
      setEmploymentStatus(profile.employmentStatus ?? "Employed");
    }
  }, [profile]);

  const updateMut = useMutation({
    mutationFn: () => updateMyProfile({
      company: profileForm.company || undefined,
      jobTitle: profileForm.jobTitle || undefined,
      location: profileForm.location || undefined,
      linkedInUrl: profileForm.linkedInUrl || undefined,
      bio: profileForm.bio || undefined,
      phone: profileForm.phone || undefined,
      employmentStatus,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["m-profile"] }); toast.success("Profile updated!"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const employmentMut = useMutation({
    mutationFn: (status: string) => updateMyProfile({ employmentStatus: status }),
    onSuccess: (_, status) => {
      setEmploymentStatus(status);
      qc.invalidateQueries({ queryKey: ["m-profile"] });
      toast.success(`Employment status updated to ${status}`);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const pwMut = useMutation({
    mutationFn: () => {
      if (pwForm.newPassword !== pwForm.confirm) throw new Error("Passwords do not match");
      return changePassword(pwForm.currentPassword, pwForm.newPassword);
    },
    onSuccess: () => { setPwForm({ currentPassword: "", newPassword: "", confirm: "" }); toast.success("Password changed!"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (isLoading || !profile) {
    return <div className="p-4 sm:p-6 lg:p-8 space-y-6 max-w-2xl mx-auto page-enter"><CardSkeleton /><CardSkeleton /></div>;
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 space-y-6 max-w-2xl mx-auto page-enter">
      <div>
          <h1 className="text-[22px] font-bold tracking-tight">My Profile</h1>
          <p className="text-muted-foreground text-[13px] mt-1">Update your information visible to fellow alumni</p>
      </div>

      <Card>
        <CardContent className="p-4 sm:p-6 flex flex-col sm:flex-row items-start sm:items-center gap-4 sm:gap-5">
          <div className="relative">
            <input ref={avatarInputRef} type="file" accept="image/*" className="hidden" onChange={async (e) => {
              const file = e.target.files?.[0];
              if (!file) return;
              setAvatarUploading(true);
              try {
                await updateMyProfile({ profilePicture: file });
                qc.invalidateQueries({ queryKey: ["m-profile"] });
                toast.success("Profile picture updated!");
              } catch (err) {
                toast.error(handleApiError(err));
              } finally {
                setAvatarUploading(false);
                if (avatarInputRef.current) avatarInputRef.current.value = "";
              }
            }} />
            <UserAvatar src={profile.profilePictureUrl} name={`${profile.firstName} ${profile.lastName}`} size="xl" />
            <Button size="icon" variant="secondary" className="absolute -bottom-1 -right-1 h-7 w-7 rounded-full"
              disabled={avatarUploading} onClick={() => avatarInputRef.current?.click()}>
              {avatarUploading ? <Loader2 size={13} className="animate-spin" /> : <Camera size={13} />}
            </Button>
          </div>
          <div>
            <h2 className="text-xl font-semibold">{profile.firstName} {profile.lastName}</h2>
            <p className="text-muted-foreground text-sm">{profile.email}</p>
            <div className="flex items-center gap-2 mt-2">
              <Badge variant="outline">Class of {profile.graduationYear}</Badge>
              <Badge variant={profile.status === "Active" ? "success" : "warning"}>{profile.status}</Badge>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Badges */}
      {badges && badges.length > 0 && (
        <Card>
          <CardHeader><CardTitle className="text-base flex items-center gap-2"><Award size={18} className="text-primary" /> Badges</CardTitle></CardHeader>
          <CardContent>
            <div className="flex flex-wrap gap-2">
              {badges.map((b) => (
                <Badge key={b.id} variant="secondary" className="gap-1.5 py-1.5 px-3 text-xs">
                  🏅 {b.badgeType.replace(/([A-Z])/g, " $1").trim()}
                </Badge>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Employment Status */}
      <Card>
        <CardHeader><CardTitle className="text-base">Employment Status</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-muted-foreground">Your employment status determines your membership renewal amount. Select the option that best describes your current situation.</p>
          {employmentStatus === "Pensioner" && (
            <div className="rounded-lg bg-amber-50 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-800 p-3 text-xs text-amber-800 dark:text-amber-300 font-medium">
              Your employment status is set to <strong>Pensioner</strong>. This cannot be changed back.
            </div>
          )}
          <div className="grid grid-cols-2 gap-3">
            <button
              type="button"
              disabled={employmentMut.isPending || employmentStatus === "Pensioner"}
              onClick={() => {}}
              className={`relative flex flex-col items-center gap-2 rounded-xl border-2 p-5 transition-all ${employmentStatus === "Employed" ? "border-primary bg-primary/5 ring-1 ring-primary/20" : "border-border"} ${employmentStatus === "Pensioner" ? "opacity-50 cursor-not-allowed" : ""}`}
            >
              <Briefcase size={24} className={employmentStatus === "Employed" ? "text-primary" : "text-muted-foreground"} />
              <span className={`font-bold text-sm ${employmentStatus === "Employed" ? "text-primary" : "text-foreground"}`}>Employed</span>
              <span className="text-[11px] text-muted-foreground text-center">Currently working or self-employed</span>
              {employmentStatus === "Employed" && <div className="absolute top-2 right-2 w-2.5 h-2.5 rounded-full bg-primary" />}
            </button>
            <button
              type="button"
              disabled={employmentMut.isPending || employmentStatus === "Pensioner"}
              onClick={() => { if (employmentStatus !== "Pensioner") setConfirmPensioner(true); }}
              className={`relative flex flex-col items-center gap-2 rounded-xl border-2 p-5 transition-all ${employmentStatus === "Pensioner" ? "border-primary bg-primary/5 ring-1 ring-primary/20" : "border-border hover:border-primary/40"}`}
            >
              <Armchair size={24} className={employmentStatus === "Pensioner" ? "text-primary" : "text-muted-foreground"} />
              <span className={`font-bold text-sm ${employmentStatus === "Pensioner" ? "text-primary" : "text-foreground"}`}>Pensioner</span>
              <span className="text-[11px] text-muted-foreground text-center">Retired and receiving pension</span>
              {employmentStatus === "Pensioner" && <div className="absolute top-2 right-2 w-2.5 h-2.5 rounded-full bg-primary" />}
            </button>
          </div>
          {employmentMut.isPending && <p className="text-xs text-muted-foreground animate-pulse">Updating...</p>}

          <ConfirmModal
            open={confirmPensioner}
            title="Switch to Pensioner"
            message="Are you sure you want to change your employment status to Pensioner? This action is irreversible — you will not be able to switch back to Employed."
            confirmLabel="Yes, I am a Pensioner"
            variant="destructive"
            isLoading={employmentMut.isPending}
            onConfirm={() => { employmentMut.mutate("Pensioner"); setConfirmPensioner(false); }}
            onCancel={() => setConfirmPensioner(false)}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Professional Info</CardTitle></CardHeader>
        <CardContent>
          <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); updateMut.mutate(); }}>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Company</Label>
                <Input placeholder="Where you work..." value={profileForm.company} onChange={(e) => setProfileForm({ ...profileForm, company: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label>Job Title</Label>
                <Input placeholder="Your current role..." value={profileForm.jobTitle} onChange={(e) => setProfileForm({ ...profileForm, jobTitle: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label>Location</Label>
                <Input placeholder="City, Country..." value={profileForm.location} onChange={(e) => setProfileForm({ ...profileForm, location: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label>Phone</Label>
                <Input placeholder="+233 xx xxx xxxx" value={profileForm.phone} onChange={(e) => setProfileForm({ ...profileForm, phone: e.target.value })} />
              </div>
            </div>
            <div className="space-y-2">
              <Label>LinkedIn URL</Label>
              <Input type="url" placeholder="https://linkedin.com/in/..." value={profileForm.linkedInUrl} onChange={(e) => setProfileForm({ ...profileForm, linkedInUrl: e.target.value })} />
            </div>
            <div className="space-y-2">
              <Label>Bio</Label>
              <Textarea placeholder="Tell your fellow alumni about yourself..." rows={3} value={profileForm.bio} onChange={(e) => setProfileForm({ ...profileForm, bio: e.target.value })} />
            </div>
            <Button type="submit" size="sm" isLoading={updateMut.isPending} loadingText="Saving">Save Changes</Button>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Change Password</CardTitle></CardHeader>
        <CardContent>
          <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); pwMut.mutate(); }}>
            <div className="space-y-2">
              <Label>Current Password</Label>
              <div className="relative">
                <Input
                  type={pwVisible ? "text" : "password"}
                  placeholder="Current password"
                  value={pwForm.currentPassword}
                  onChange={(e) => setPwForm({ ...pwForm, currentPassword: e.target.value })}
                  required
                />
                <Button type="button" size="icon" variant="ghost" className="absolute right-1 top-1/2 -translate-y-1/2 h-8 w-8" onClick={() => setPwVisible(!pwVisible)} aria-label={pwVisible ? "Hide password" : "Show password"}>
                  {pwVisible ? <EyeOff size={14} /> : <Eye size={14} />}
                </Button>
              </div>
            </div>
            <div className="space-y-2">
              <Label>New Password</Label>
              <Input type="password" placeholder="New password" value={pwForm.newPassword} onChange={(e) => setPwForm({ ...pwForm, newPassword: e.target.value })} required />
            </div>
            <div className="space-y-2">
              <Label>Confirm New Password</Label>
              <Input type="password" placeholder="Confirm new password" value={pwForm.confirm} onChange={(e) => setPwForm({ ...pwForm, confirm: e.target.value })} required />
            </div>
            <Button type="submit" size="sm" isLoading={pwMut.isPending} loadingText="Changing">Change Password</Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
