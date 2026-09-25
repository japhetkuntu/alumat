"use client";

import { useState, useRef } from "react";
import { InfoTip } from "@alumni/ui";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Camera, Eye, EyeOff, Loader2, Briefcase, Armchair, Award,
  User, Lock, Bell, Link as LinkIcon, AlertCircle, RefreshCcw, UsersRound,
} from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { PhoneInput } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { TagInput } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Separator } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import {
  getMyProfile, updateMyProfile, changePassword, getMyBadges, getCurrentMembershipCampaign,
  getNotificationPreferences, updateNotificationPreferences,
} from "@/lib/member-api";
import { getRoundedLocation } from "@/lib/geolocation";
import { formatCurrency } from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { usePushNotifications } from "@/hooks/use-push-notifications";
import { useNavTheme, useDisabledFeatures } from "@/components/member/member-layout";
import type { NotificationPreference } from "@/types";

/* ─────────────────────────────────────────────────────────────────────────
   SHARED BITS
   ───────────────────────────────────────────────────────────────────────── */
function EmploymentOption({
  icon: Icon, label, description, active, disabled, onClick,
}: {
  icon: React.ElementType; label: string; description: string;
  active: boolean; disabled: boolean; onClick: () => void;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className={cn(
        "relative flex flex-col items-center gap-2 rounded-xl border-2 p-5 text-center transition-colors",
        active   ? "border-accent bg-accent/10"      : "border-border bg-background",
        !disabled && !active && "hover:border-accent/40",
        disabled && "cursor-not-allowed opacity-50",
      )}
    >
      <Icon size={22} style={{ color: active ? "var(--accent)" : "var(--muted-foreground)" }} />
      <span className="text-[13.5px] font-semibold" style={{ color: active ? "var(--primary)" : "var(--foreground)" }}>
        {label}
      </span>
      <span className="text-[11.5px]" style={{ color: "var(--muted-foreground)" }}>
        {description}
      </span>
      {active && <div className="absolute top-2.5 right-2.5 w-2 h-2 rounded-full" style={{ background: "var(--primary)" }} />}
    </button>
  );
}

function Toggle({ checked, onChange, label, description, disabled, tip }: { checked: boolean; onChange: (v: boolean) => void; label: string; description?: string; disabled?: boolean; tip?: string }) {
  return (
    <div className="flex items-start justify-between gap-4 py-4">
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold text-foreground">{label}{tip && <InfoTip text={tip} className="ml-1.5" />}</p>
        {description && <p className="text-[12px] text-muted-foreground mt-0.5">{description}</p>}
      </div>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        aria-label={label}
        disabled={disabled}
        onClick={() => onChange(!checked)}
        className={cn(
          "relative inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-all duration-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary",
          disabled ? "cursor-not-allowed opacity-50" : "cursor-pointer",
          checked ? "bg-primary" : "bg-muted"
        )}
      >
        <span
          className={cn(
            "pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow-lg transition-transform duration-300",
            checked ? "translate-x-5" : "translate-x-0"
          )}
        />
      </button>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PAGE — profile (editable identity, professional info, employment, badges)
   first, then account settings (notifications, security, about) — one
   unified page instead of two separate destinations for what's really a
   single "manage my account" task.
   ───────────────────────────────────────────────────────────────────────── */
export default function MemberProfilePage() {
  const { logout, tokens, isLoading: authLoading } = useAuth();
  const profileQueriesEnabled = !authLoading && !!tokens?.accessToken;
  const [profileForm, setProfileForm] = useState({
    program: "", company: "", jobTitle: "", location: "", linkedInUrl: "", bio: "", phone: "", dateOfBirth: "",
  });
  const [schoolRecordsForm, setSchoolRecordsForm] = useState({
    yearOfEntry: "", house: "", studentStatus: "", prefectStatus: "", achievements: "",
  });
  const [clubsAndSocieties, setClubsAndSocieties] = useState<string[]>([]);
  const [leadershipRoles, setLeadershipRoles] = useState<string[]>([]);
  const [connectionType, setConnectionType] = useState("");
  const [skills, setSkills] = useState<string[]>([]);
  const [interests, setInterests] = useState<string[]>([]);
  const [visibility, setVisibility] = useState({ email: false, phone: false, company: true, bio: true });
  const [showOnAlumniMap, setShowOnAlumniMap] = useState(false);
  const [employmentStatus, setEmploymentStatus] = useState("Employed");
  const [confirmPensioner, setConfirmPensioner]  = useState(false);
  const [pwForm,  setPwForm]   = useState({ currentPassword: "", newPassword: "", confirm: "" });
  const [pwVisible, setPwVisible] = useState(false);
  const [avatarUploading, setAvatarUploading] = useState(false);
  const avatarInputRef = useRef<HTMLInputElement>(null);
  const qc = useQueryClient();

  const { data: profile, isLoading, isError, refetch, isRefetching } = useQuery({
    queryKey: ["m-profile"],
    queryFn:  getMyProfile,
    enabled: profileQueriesEnabled,
  });

  const { data: badges } = useQuery({
    queryKey: ["m-badges"],
    queryFn:  getMyBadges,
    enabled: profileQueriesEnabled,
  });

  const { data: membershipCampaign } = useQuery({
    queryKey: ["m-current-membership-campaign"],
    queryFn:  getCurrentMembershipCampaign,
    enabled: profileQueriesEnabled,
  });

  const { data: notifPrefs } = useQuery({
    queryKey: ["m-notif-prefs"],
    queryFn:  getNotificationPreferences,
    enabled: profileQueriesEnabled,
  });

  const { data: navTheme } = useNavTheme();
  const isCommunity = navTheme?.organizationType === "Community";
  const institutionName = navTheme?.displayName || "Member Portal";
  const disabledFeatures = useDisabledFeatures();
  const digestEnabled = !disabledFeatures.has("Digest");
  const smsNotificationsEnabled = navTheme?.smsNotificationsEnabled ?? true;
  const pushNotifications = usePushNotifications();

  // Adjust local form state when the fetched profile changes — done during
  // render (React's documented alternative to an effect for this case)
  // rather than in a useEffect, guarded so it only fires once per distinct
  // `profile` reference.
  const [syncedProfile, setSyncedProfile] = useState(profile);
  if (profile && profile !== syncedProfile) {
    setSyncedProfile(profile);
    setProfileForm({
      program:     profile.program     ?? "",
      company:     profile.company     ?? "",
      jobTitle:    profile.jobTitle    ?? "",
      location:    profile.location    ?? "",
      linkedInUrl: profile.linkedInUrl ?? "",
      bio:         profile.bio         ?? "",
      phone:       profile.phone       ?? "",
      dateOfBirth: profile.dateOfBirth ? profile.dateOfBirth.slice(0, 10) : "",
    });
    setSchoolRecordsForm({
      yearOfEntry:   profile.yearOfEntry != null ? String(profile.yearOfEntry) : "",
      house:         profile.house         ?? "",
      studentStatus: profile.studentStatus ?? "",
      prefectStatus: profile.prefectStatus ?? "",
      achievements:  profile.achievements  ?? "",
    });
    setClubsAndSocieties(profile.clubsAndSocieties ?? []);
    setLeadershipRoles(profile.leadershipRoles ?? []);
    setConnectionType(profile.connectionType ?? "");
    setSkills(profile.skills ?? []);
    setInterests(profile.interests ?? []);
    setVisibility({
      email: profile.showEmailOnDirectory ?? false,
      phone: profile.showPhoneOnDirectory ?? false,
      company: profile.showCompanyOnDirectory ?? true,
      bio: profile.showBioOnDirectory ?? true,
    });
    setEmploymentStatus(profile.employmentStatus ?? "Employed");
    setShowOnAlumniMap(profile.showOnAlumniMap ?? false);
  }

  const updateMut = useMutation({
    mutationFn: () => updateMyProfile({
      program:          profileForm.program     || undefined,
      company:          profileForm.company     || undefined,
      jobTitle:         profileForm.jobTitle    || undefined,
      location:         profileForm.location    || undefined,
      linkedInUrl:      profileForm.linkedInUrl || undefined,
      bio:              profileForm.bio         || undefined,
      phone:            profileForm.phone       || undefined,
      dateOfBirth:      profileForm.dateOfBirth || undefined,
      employmentStatus,
      showOnAlumniMap,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-profile"] });
      toast.success("Profile updated.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateSchoolRecordsMut = useMutation({
    mutationFn: () => updateMyProfile({
      yearOfEntry:    schoolRecordsForm.yearOfEntry ? Number(schoolRecordsForm.yearOfEntry) : undefined,
      house:          schoolRecordsForm.house         || undefined,
      studentStatus:  schoolRecordsForm.studentStatus || undefined,
      prefectStatus:  schoolRecordsForm.prefectStatus || undefined,
      achievements:   schoolRecordsForm.achievements  || undefined,
      clubsAndSocieties: clubsAndSocieties.length ? clubsAndSocieties : undefined,
      leadershipRoles:   leadershipRoles.length   ? leadershipRoles   : undefined,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-profile"] });
      toast.success("Community details saved.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateCommunityProfileMut = useMutation({
    mutationFn: () => updateMyProfile({
      connectionType: connectionType || undefined,
      skills: skills.length ? skills : undefined,
      interests: interests.length ? interests : undefined,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-profile"] });
      toast.success("Community profile updated.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateVisibilityMut = useMutation({
    mutationFn: () => updateMyProfile({
      showEmailOnDirectory: visibility.email,
      showPhoneOnDirectory: visibility.phone,
      showCompanyOnDirectory: visibility.company,
      showBioOnDirectory: visibility.bio,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-profile"] });
      toast.success("Profile visibility updated.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  // Turning the map on requires a fresh, real location — captured from the
  // browser's geolocation API and rounded to ~11km precision before it ever
  // leaves the device (see getRoundedLocation), not guessed from the
  // free-text "location" field above. Turning it off needs no location at
  // all; the backend clears any stored coordinates as soon as this is false.
  const mapToggleMut = useMutation({
    mutationFn: async (value: boolean) => {
      if (!value) {
        await updateMyProfile({ showOnAlumniMap: false });
        return false;
      }
      const { latitude, longitude } = await getRoundedLocation();
      await updateMyProfile({ showOnAlumniMap: true, mapLatitude: latitude, mapLongitude: longitude });
      return true;
    },
    onSuccess: (value) => {
      setShowOnAlumniMap(value);
      qc.invalidateQueries({ queryKey: ["m-profile"] });
      toast.success(value ? "You're now visible on the Community Map." : "Removed from the Community Map.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  // Re-captures location without touching the toggle — lets an already-visible
  // member refresh their pin any time (e.g. after moving), per the "should
  // be able to change their location anytime" requirement.
  const updateLocationMut = useMutation({
    mutationFn: async () => {
      const { latitude, longitude } = await getRoundedLocation();
      await updateMyProfile({ showOnAlumniMap: true, mapLatitude: latitude, mapLongitude: longitude });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-profile"] });
      toast.success("Your location on the Community Map has been updated.");
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

  const notifMut = useMutation({
    mutationFn: updateNotificationPreferences,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-notif-prefs"] });
      toast.success("Notification preferences saved");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function toggleNotif(key: keyof Omit<NotificationPreference, "id">, value: boolean) {
    if (!notifPrefs) return;
    notifMut.mutate({
      membershipReminders: notifPrefs.membershipReminders,
      campaignAlerts: notifPrefs.campaignAlerts,
      eventReminders: notifPrefs.eventReminders,
      jobAlerts: notifPrefs.jobAlerts,
      classNoteAlerts: notifPrefs.classNoteAlerts,
      spotlightAlerts: notifPrefs.spotlightAlerts,
      smsAlerts: notifPrefs.smsAlerts,
      whatsAppAlerts: notifPrefs.whatsAppAlerts,
      digestFrequency: notifPrefs.digestFrequency,
      [key]: value,
    });
  }

  function setDigestFrequency(frequency: NotificationPreference["digestFrequency"]) {
    if (!notifPrefs) return;
    notifMut.mutate({
      membershipReminders: notifPrefs.membershipReminders,
      campaignAlerts: notifPrefs.campaignAlerts,
      eventReminders: notifPrefs.eventReminders,
      jobAlerts: notifPrefs.jobAlerts,
      classNoteAlerts: notifPrefs.classNoteAlerts,
      spotlightAlerts: notifPrefs.spotlightAlerts,
      smsAlerts: notifPrefs.smsAlerts,
      whatsAppAlerts: notifPrefs.whatsAppAlerts,
      digestFrequency: frequency,
    });
  }

  // A transient network hiccup right after navigating here (retry budget is 1,
  // set globally in Providers) used to leave this page stuck on skeletons
  // forever, with a full page reload the only way out — isError fires once
  // TanStack Query gives up retrying, so this gives the user a visible way to
  // try again instead.
  if (isError) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 xl:p-10 max-w-6xl mx-auto">
        <EmptyState
          icon={<AlertCircle size={40} />}
          title="Couldn't load your profile"
          description="Something went wrong while fetching your profile. Please try again."
          action={
            <Button onClick={() => refetch()} disabled={isRefetching} className="gap-2 font-semibold">
              <RefreshCcw size={14} className={isRefetching ? "animate-spin" : undefined} />
              {isRefetching ? "Retrying..." : "Try again"}
            </Button>
          }
        />
      </div>
    );
  }

  if (isLoading || !profile) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 xl:p-10 max-w-6xl mx-auto space-y-5">
        <CardSkeleton />
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 xl:p-10 max-w-6xl mx-auto">
      <PageHeader eyebrow="Account" title="Profile" description="Your profile, visible to the people in your community, and how the portal works for you." />

      <div className="grid grid-cols-1 lg:grid-cols-[300px_minmax(0,1fr)] gap-5 lg:gap-6 mt-6 items-start">

        {/* ═══════════════════ SIDEBAR ═══════════════════ */}
        <div className="space-y-5 lg:sticky lg:top-6">

        {/* ── Identity ── */}
        <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <User size={18} className="text-primary" />
              Identity
            </CardTitle>
            <CardDescription>How you appear to people in your community</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="flex flex-col items-center text-center gap-4">
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
                  aria-label="Change profile photo"
                  onClick={() => avatarInputRef.current?.click()}
                >
                  {avatarUploading ? <Loader2 size={12} className="animate-spin" /> : <Camera size={12} />}
                </Button>
              </div>

              <div className="min-w-0 w-full">
                <p className="text-[17px] font-semibold truncate" style={{ color: "var(--foreground)" }}>
                  {profile.firstName} {profile.lastName}
                </p>
                <p className="text-[13.5px] mt-0.5 break-all" style={{ color: "var(--muted-foreground)" }}>
                  {profile.email}
                </p>
                <div className="flex flex-wrap items-center justify-center gap-2 mt-3">
                  {!isCommunity && (
                    <Badge variant="outline" className="text-[11px] font-semibold">
                      Cohort {profile.graduationYear}
                    </Badge>
                  )}
                  <Badge variant={profile.status === "Active" ? "success" : "warning"} className="text-[11px] font-semibold">
                    {profile.status}
                  </Badge>
                  {badges?.map(b => (
                    <Badge key={b.id} variant="secondary" className="gap-1 text-[11px] font-semibold">
                      🏅 {b.badgeType.replace(/([A-Z])/g, " $1").trim()}
                    </Badge>
                  ))}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
        </div>{/* end sidebar (Identity) — Employment status + main column follow below */}

        {/* ── Main column ── */}
        <div className="space-y-5">

        {/* ── Professional info ── */}
        <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-150">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Professional info</CardTitle>
            <CardDescription>Shown on your profile and the member directory</CardDescription>
          </CardHeader>
          <CardContent className="pt-0">
            <form className="space-y-4" onSubmit={e => { e.preventDefault(); updateMut.mutate(); }}>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {[
                  { id: "program",  label: "Program / course of study", placeholder: "e.g. BSc Mining Engineering", type: "text" },
                  { id: "company",  label: "Company",  placeholder: "Where you work",    type: "text"  },
                  { id: "jobTitle", label: "Job title", placeholder: "Your current role", type: "text"  },
                  { id: "location", label: "Location",  placeholder: "City, Country",     type: "text"  },
                ].map(({ id, label, placeholder, type }) => (
                  <div key={id} className="space-y-1.5">
                    <Label htmlFor={id} className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                      {label}
                    </Label>
                    <Input
                      id={id}
                      type={type}
                      autoComplete={id === "company" ? "organization" : undefined}
                      placeholder={placeholder}
                      value={profileForm[id as keyof typeof profileForm]}
                      onChange={e => setProfileForm(f => ({ ...f, [id]: e.target.value }))}
                      className="h-11 text-[14px]"
                    />
                  </div>
                ))}
                <div className="space-y-1.5">
                  <Label htmlFor="phone" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Phone
                  </Label>
                  <PhoneInput
                    id="phone"
                    value={profileForm.phone}
                    onChange={val => setProfileForm(f => ({ ...f, phone: val }))}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="dateOfBirth" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Date of birth
                  </Label>
                  <Input
                    id="dateOfBirth"
                    type="date"
                    value={profileForm.dateOfBirth}
                    onChange={e => setProfileForm(f => ({ ...f, dateOfBirth: e.target.value }))}
                    className="h-11 text-[14px]"
                  />
                  <p className="text-[11.5px] text-muted-foreground">Optional — only the month and day are ever used, to celebrate your birthday with a spotlight.</p>
                </div>
              </div>

              <div className="rounded-xl p-3.5 space-y-2.5" style={{ background: "var(--muted)" }}>
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>Show me on the Community Map</p>
                    <p className="text-[12px] text-muted-foreground mt-0.5">
                      Uses your device&apos;s real location (rounded to roughly your city/region, never your exact address) to plot a pin, visible to fellow members. Off by default: your location stays private, and nothing is stored unless you turn this on.
                    </p>
                  </div>
                  <button
                    type="button"
                    role="switch"
                    aria-label="Show me on the Community Map"
                    aria-checked={showOnAlumniMap}
                    disabled={mapToggleMut.isPending}
                    onClick={() => mapToggleMut.mutate(!showOnAlumniMap)}
                    className="relative inline-flex h-6 w-11 shrink-0 mt-0.5 cursor-pointer rounded-full border-2 border-transparent transition-all duration-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary disabled:opacity-60 disabled:cursor-not-allowed"
                    style={{ background: showOnAlumniMap ? "var(--primary)" : "var(--border)" }}
                  >
                    <span
                      className="pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow-lg transition-transform duration-300"
                      style={{ transform: showOnAlumniMap ? "translateX(20px)" : "translateX(0)" }}
                    />
                  </button>
                </div>
                {showOnAlumniMap && (
                  <button
                    type="button"
                    disabled={updateLocationMut.isPending}
                    onClick={() => updateLocationMut.mutate()}
                    className="text-[12px] font-semibold text-primary hover:underline disabled:opacity-60 disabled:cursor-not-allowed"
                  >
                    {updateLocationMut.isPending ? "Updating location…" : "Update my location"}
                  </button>
                )}
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
                  placeholder="Tell your community about yourself…"
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

        <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-175">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Community profile</CardTitle>
            <CardDescription>Help people understand how you contribute and what you care about.</CardDescription>
          </CardHeader>
          <CardContent className="pt-0">
            <form className="space-y-4" onSubmit={e => { e.preventDefault(); updateCommunityProfileMut.mutate(); }}>
              <div className="space-y-1.5">
                <Label htmlFor="connectionType" className="text-[13px] font-semibold">How are you connected?</Label>
                <select
                  id="connectionType"
                  value={connectionType}
                  onChange={e => setConnectionType(e.target.value)}
                  className="h-11 w-full rounded-md border border-border bg-background px-3 text-[14px] text-foreground"
                >
                  <option value="">Choose one</option>
                  {["Member", "Volunteer", "Leader", "Staff", "Supporter", "Mentor", "Partner", "Former member", "Other"].map(option => (
                    <option key={option} value={option}>{option}</option>
                  ))}
                </select>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="skills" className="text-[13px] font-semibold">Skills</Label>
                <TagInput id="skills" value={skills} onChange={setSkills} placeholder="Add a skill and press Enter…" />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="interests" className="text-[13px] font-semibold">Interests and causes</Label>
                <TagInput id="interests" value={interests} onChange={setInterests} placeholder="Add an interest and press Enter…" />
              </div>
              <Button type="submit" className="font-semibold text-[13.5px]" isLoading={updateCommunityProfileMut.isPending} loadingText="Saving…">
                Save community profile
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-200">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Directory visibility</CardTitle>
            <CardDescription>Choose what other members can see on your profile.</CardDescription>
          </CardHeader>
          <CardContent className="divide-y divide-border/40">
            {[
              ["email", "Email address", "Let members contact you by email."],
              ["phone", "Phone number", "Let members contact you by phone."],
              ["company", "Company and role", "Show your workplace and current role."],
              ["bio", "Bio", "Show your introduction on your directory profile."],
            ].map(([key, label, description]) => (
              <Toggle
                key={key}
                checked={visibility[key as keyof typeof visibility]}
                onChange={(value) => setVisibility(current => ({ ...current, [key]: value }))}
                label={label}
                description={description}
              />
            ))}
            <Button type="button" className="mt-4 font-semibold text-[13.5px]" onClick={() => updateVisibilityMut.mutate()} isLoading={updateVisibilityMut.isPending} loadingText="Saving…">
              Save visibility
            </Button>
          </CardContent>
        </Card>

        {/* ── Community records — cohort-based organizations only ── */}
        {!isCommunity && (
        <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-175">
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <UsersRound size={18} className="text-primary" />
              Community details
            </CardTitle>
            <CardDescription>Add the groups, cohort, and contributions that help people know you</CardDescription>
          </CardHeader>
          <CardContent className="pt-0">
            <form className="space-y-4" onSubmit={e => { e.preventDefault(); updateSchoolRecordsMut.mutate(); }}>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label htmlFor="yearOfEntry" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Year joined
                  </Label>
                  <Input
                    id="yearOfEntry"
                    type="number"
                    inputMode="numeric"
                    placeholder="e.g. 2015"
                    value={schoolRecordsForm.yearOfEntry}
                    onChange={e => setSchoolRecordsForm(f => ({ ...f, yearOfEntry: e.target.value }))}
                    className="h-11 text-[14px]"
                  />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Cohort year
                  </Label>
                  <Input value={profile.graduationYear} disabled className="h-11 text-[14px]" />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="house" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Group or chapter
                  </Label>
                  <Input
                    id="house"
                    placeholder="e.g. Young Adults, Chapter A"
                    value={schoolRecordsForm.house}
                    onChange={e => setSchoolRecordsForm(f => ({ ...f, house: e.target.value }))}
                    className="h-11 text-[14px]"
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="prefectStatus" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Leadership role
                  </Label>
                  <Input
                    id="prefectStatus"
                    placeholder="e.g. Chapter coordinator"
                    value={schoolRecordsForm.prefectStatus}
                    onChange={e => setSchoolRecordsForm(f => ({ ...f, prefectStatus: e.target.value }))}
                    className="h-11 text-[14px]"
                  />
                </div>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="clubsAndSocieties" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                  Groups &amp; interests
                </Label>
                <TagInput
                  id="clubsAndSocieties"
                  value={clubsAndSocieties}
                  onChange={setClubsAndSocieties}
                  placeholder="Type a group or interest and press Enter…"
                />
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="leadershipRoles" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                  Leadership roles
                </Label>
                <TagInput
                  id="leadershipRoles"
                  value={leadershipRoles}
                  onChange={setLeadershipRoles}
                  placeholder="Type a role and press Enter…"
                />
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="achievements" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                  Contributions and achievements
                </Label>
                <Textarea
                  id="achievements"
                  placeholder="Awards, honors, notable accomplishments…"
                  rows={3}
                  value={schoolRecordsForm.achievements}
                  onChange={e => setSchoolRecordsForm(f => ({ ...f, achievements: e.target.value }))}
                  className="text-[14px] resize-none"
                />
              </div>

              <Button
                type="submit"
                className="font-semibold text-[13.5px] gap-2"
                style={{ height: 42 }}
                isLoading={updateSchoolRecordsMut.isPending}
                loadingText="Saving…"
              >
                Save community details
              </Button>
            </form>
          </CardContent>
        </Card>
        )}

        {/* ── Employment status ── */}
        <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-200">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Employment status</CardTitle>
            <CardDescription>Determines your membership renewal amount</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 pt-0">
            {employmentStatus === "Pensioner" && (
              <div className="rounded-xl p-3.5 text-[13px] font-medium bg-warning/10 border border-warning/25 text-warning">
                Your status is set to <strong>Pensioner</strong>. This cannot be changed back.
              </div>
            )}

            <div className="grid grid-cols-2 gap-3">
              <EmploymentOption
                icon={Briefcase}
                label="Employed"
                description={
                  membershipCampaign?.amountPerMember != null
                    ? `Currently working or self-employed: ${formatCurrency(membershipCampaign.amountPerMember)}/year`
                    : "Currently working or self-employed"
                }
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
                description={
                  membershipCampaign?.pensionerAmountPerMember != null
                    ? `Retired and receiving pension: ${formatCurrency(membershipCampaign.pensionerAmountPerMember)}/year`
                    : "Retired and receiving pension"
                }
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

        {/* ═══════════════════ SETTINGS ═══════════════════ */}

        {/* ── Notifications ── */}
        <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-250">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Bell size={18} className="text-primary" />
              Notifications
            </CardTitle>
            <CardDescription>Choose what updates you want to receive</CardDescription>
          </CardHeader>
          <CardContent className="divide-y divide-border/40">
            {digestEnabled && (
              <div className="flex flex-col gap-3 py-4 sm:flex-row sm:items-start sm:justify-between sm:gap-4">
                <div className="min-w-0">
                  <p className="text-sm font-semibold text-foreground">Digest Email</p>
                  <p className="text-[12px] text-muted-foreground mt-0.5">
                    A roundup of new jobs, upcoming events, and what you&apos;ve missed
                  </p>
                </div>
                <div className="flex w-full shrink-0 rounded-lg border border-border p-0.5 sm:w-auto" role="radiogroup" aria-label="Digest email frequency">
                  {(["Weekly", "Monthly", "None"] as const).map((freq) => {
                    const active = (notifPrefs?.digestFrequency ?? "Weekly") === freq;
                    return (
                      <button
                        key={freq}
                        type="button"
                        role="radio"
                        aria-checked={active}
                        onClick={() => setDigestFrequency(freq)}
                        className={cn(
                          "flex-1 px-3 py-1.5 text-[12.5px] font-semibold rounded-md transition-colors sm:flex-none",
                          active ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:text-foreground"
                        )}
                      >
                        {freq === "None" ? "Off" : freq}
                      </button>
                    );
                  })}
                </div>
              </div>
            )}
            <Toggle
              checked={notifPrefs?.membershipReminders ?? true}
              onChange={(v) => toggleNotif("membershipReminders", v)}
              label="Membership Reminders"
              tip="A nudge before your membership dues expire."
              description="Reminders about upcoming membership renewals"
            />
            <Toggle
              checked={notifPrefs?.campaignAlerts ?? true}
              onChange={(v) => toggleNotif("campaignAlerts", v)}
              label="Fundraiser Alerts"
              description="Notifications when new fundraisers are launched"
            />
            <Toggle
              checked={notifPrefs?.eventReminders ?? true}
              onChange={(v) => toggleNotif("eventReminders", v)}
              label="Event Reminders"
              description="Get notified about upcoming community events"
            />
            <Toggle
              checked={notifPrefs?.jobAlerts ?? true}
              onChange={(v) => toggleNotif("jobAlerts", v)}
              label="Job Alerts"
              description="Notifications for new job postings"
            />
            <Toggle
              checked={notifPrefs?.spotlightAlerts ?? true}
              onChange={(v) => toggleNotif("spotlightAlerts", v)}
              label="Spotlight Updates"
              description="Get notified about new member spotlights"
            />
            {smsNotificationsEnabled && (
              <Toggle
                checked={notifPrefs?.smsAlerts ?? false}
                onChange={(v) => toggleNotif("smsAlerts", v)}
                label="SMS Notifications"
              tip="Alerts are texted to the phone number on your profile."
                description={profileForm.phone ? "Also send important alerts to your phone via SMS" : "Add a phone number above to enable SMS alerts"}
              />
            )}
            {pushNotifications.isSupported && (
              <Toggle
                checked={pushNotifications.isSubscribed}
                disabled={pushNotifications.isBusy || pushNotifications.permission === "denied"}
                onChange={(v) => {
                  const action = v ? pushNotifications.subscribe() : pushNotifications.unsubscribe();
                  action.catch(() => toast.error("Couldn't update push notification settings"));
                }}
                label="Push Notifications"
              tip="Alerts appear on this device even when the site is closed."
                description={
                  pushNotifications.permission === "denied"
                    ? "Notifications are blocked for this site in your browser settings"
                    : "Get browser notifications for jobs, events, and updates"
                }
              />
            )}
            {/* WhatsApp notifications are wired up but hidden from the UI for
                the pilot — email and SMS only for now. The underlying
                whatsAppAlerts preference is left untouched, not forced off. */}
          </CardContent>
        </Card>

        {/* ── Security + About ── */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-5 items-start">
          <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-300">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <Lock size={18} className="text-primary" />
                Security
              </CardTitle>
              <CardDescription>Change your password to keep your account secure</CardDescription>
            </CardHeader>
            <CardContent>
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

              <Separator className="my-5" />

              <div className="flex flex-col gap-3">
                <p className="text-sm text-muted-foreground">
                  Sign out of your account on this device.
                </p>
                <Button
                  variant="outline"
                  size="sm"
                  className="self-start text-destructive hover:bg-destructive/10 hover:text-destructive"
                  onClick={logout}
                >
                  Log out
                </Button>
              </div>
            </CardContent>
          </Card>

          {/* About */}
          <Card className="border-border/40 bg-muted/20 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-350">
            <CardContent className="p-6 flex items-center justify-between h-full">
              <div>
                <p className="text-sm font-bold">{institutionName}</p>
                <p className="text-[12px] text-muted-foreground mt-0.5">Member Portal</p>
              </div>
              <div className="flex items-center gap-2">
                <LinkIcon size={13} className="text-muted-foreground" />
              </div>
            </CardContent>
          </Card>
        </div>

        </div>{/* end main column */}

      </div>

      {/* ── Confirm pensioner modal ── */}
      <ConfirmModal
        open={confirmPensioner}
        title="Switch to Pensioner?"
        message="This is permanent: you will not be able to switch back to Employed. Only confirm if you are retired and receiving a pension."
        confirmLabel="Yes, I am a pensioner"
        variant="destructive"
        isLoading={employmentMut.isPending}
        onConfirm={() => { employmentMut.mutate("Pensioner"); setConfirmPensioner(false); }}
        onCancel={() => setConfirmPensioner(false)}
      />
    </div>
  );
}
