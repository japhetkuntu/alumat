"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  User, Lock, Bell, Link as LinkIcon, Eye, EyeOff, Loader2, CheckCircle2, AlertCircle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Separator } from "@/components/ui/separator";
import { getInitials, cn } from "@/lib/utils";
import { getMyProfile, changePassword, getNotificationPreferences, updateNotificationPreferences } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import { toast } from "sonner";
import Link from "next/link";

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
          "relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-all duration-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary",
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

export default function MemberSettingsPage() {
  const { user } = useAuth();
  const qc = useQueryClient();

  const [pwForm, setPwForm] = useState({ currentPassword: "", newPassword: "", confirm: "" });
  const [showPw, setShowPw] = useState(false);

  const { data: profile } = useQuery({
    queryKey: ["m-profile"],
    queryFn: getMyProfile,
  });

  const { data: notifPrefs } = useQuery({
    queryKey: ["m-notif-prefs"],
    queryFn: getNotificationPreferences,
  });

  const notifMut = useMutation({
    mutationFn: updateNotificationPreferences,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-notif-prefs"] });
      toast.success("Notification preferences saved");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const pwMut = useMutation({
    mutationFn: () => changePassword(pwForm.currentPassword, pwForm.newPassword),
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

  function toggleNotif(key: keyof Omit<import("@/types").NotificationPreference, "id">, value: boolean) {
    if (!notifPrefs) return;
    notifMut.mutate({
      membershipReminders: notifPrefs.membershipReminders,
      campaignAlerts: notifPrefs.campaignAlerts,
      eventReminders: notifPrefs.eventReminders,
      jobAlerts: notifPrefs.jobAlerts,
      classNoteAlerts: notifPrefs.classNoteAlerts,
      spotlightAlerts: notifPrefs.spotlightAlerts,
      [key]: value,
    });
  }

  const fullName = profile ? `${profile.firstName} ${profile.lastName}` : (user?.name ?? "");

  return (
    <div className="p-4 sm:p-6 lg:p-8 xl:p-12 space-y-6 sm:space-y-8 lg:space-y-10 max-w-3xl mx-auto">
      <header className="space-y-2 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <h1 className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tight">Settings</h1>
        <p className="text-muted-foreground text-sm sm:text-base lg:text-lg font-medium">Manage your account preferences and security.</p>
      </header>

      {/* Account overview */}
      <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <User size={18} className="text-primary" />
            Account
          </CardTitle>
          <CardDescription>Your profile information</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col sm:flex-row items-start sm:items-center gap-4 sm:gap-5">
            <Avatar size="lg" className="ring-2 ring-primary/20 shrink-0">
              <AvatarFallback name={fullName} className="text-[16px]">{getInitials(fullName)}</AvatarFallback>
            </Avatar>
            <div className="flex-1 min-w-0">
              <p className="text-base sm:text-lg font-bold truncate">{fullName || "—"}</p>
              <p className="text-sm text-muted-foreground truncate">{profile?.email ?? user?.email ?? "—"}</p>
              <div className="flex items-center gap-2 mt-2">
                <Badge variant="success" className="text-[10px]">Active Member</Badge>
                {profile?.graduationYear && (
                  <Badge variant="secondary" className="text-[10px]">Class of {profile.graduationYear}</Badge>
                )}
              </div>
            </div>
            <Link href="/profile">
              <Button variant="outline" size="sm" className="shrink-0">Edit Profile</Button>
            </Link>
          </div>

          {(profile?.company || profile?.jobTitle || profile?.location) && (
            <>
              <Separator className="my-5" />
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 text-sm">
                {profile.jobTitle && (
                  <div>
                    <p className="text-[11px] font-black text-muted-foreground/60 uppercase tracking-widest mb-1">Job Title</p>
                    <p className="font-semibold">{profile.jobTitle}</p>
                  </div>
                )}
                {profile.company && (
                  <div>
                    <p className="text-[11px] font-black text-muted-foreground/60 uppercase tracking-widest mb-1">Company</p>
                    <p className="font-semibold">{profile.company}</p>
                  </div>
                )}
                {profile.location && (
                  <div>
                    <p className="text-[11px] font-black text-muted-foreground/60 uppercase tracking-widest mb-1">Location</p>
                    <p className="font-semibold">{profile.location}</p>
                  </div>
                )}
              </div>
            </>
          )}
        </CardContent>
      </Card>

      {/* Notifications */}
      <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-150">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Bell size={18} className="text-primary" />
            Notifications
          </CardTitle>
          <CardDescription>Choose what updates you want to receive</CardDescription>
        </CardHeader>
        <CardContent className="divide-y divide-border/40">
          <Toggle
            checked={notifPrefs?.membershipReminders ?? true}
            onChange={(v) => toggleNotif("membershipReminders", v)}
            label="Membership Reminders"
            description="Reminders about upcoming membership renewals"
          />
          <Toggle
            checked={notifPrefs?.campaignAlerts ?? true}
            onChange={(v) => toggleNotif("campaignAlerts", v)}
            label="Campaign Alerts"
            description="Notifications when new campaigns are launched"
          />
          <Toggle
            checked={notifPrefs?.eventReminders ?? true}
            onChange={(v) => toggleNotif("eventReminders", v)}
            label="Event Reminders"
            description="Get notified about upcoming alumni events"
          />
          <Toggle
            checked={notifPrefs?.jobAlerts ?? true}
            onChange={(v) => toggleNotif("jobAlerts", v)}
            label="Job Alerts"
            description="Notifications for new job postings"
          />
          <Toggle
            checked={notifPrefs?.classNoteAlerts ?? true}
            onChange={(v) => toggleNotif("classNoteAlerts", v)}
            label="Class Notes"
            description="Notifications when classmates post to your year group wall"
          />
          <Toggle
            checked={notifPrefs?.spotlightAlerts ?? true}
            onChange={(v) => toggleNotif("spotlightAlerts", v)}
            label="Spotlight Updates"
            description="Get notified about new alumni spotlights"
          />
        </CardContent>
      </Card>

      {/* Security */}
      <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-200">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Lock size={18} className="text-primary" />
            Security
          </CardTitle>
          <CardDescription>Update your password to keep your account secure</CardDescription>
        </CardHeader>
        <CardContent>
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

      {/* About */}
      <Card className="border-border/40 bg-muted/20 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-250">
        <CardContent className="p-6 flex items-center justify-between">
          <div>
            <p className="text-sm font-bold">UMaT Alumni Member Portal</p>
            <p className="text-[12px] text-muted-foreground mt-0.5">University of Mines &amp; Technology</p>
          </div>
          <div className="flex items-center gap-2">
            <Link href="/profile">
              <Button variant="ghost" size="sm" className="text-[12px] gap-1.5">
                <LinkIcon size={13} />
                Full Profile
              </Button>
            </Link>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
