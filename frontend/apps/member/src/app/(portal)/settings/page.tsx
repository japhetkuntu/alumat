"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  User, Lock, Bell, Link as LinkIcon,
} from "lucide-react";
import { Button } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Avatar, AvatarFallback } from "@alumni/ui";
import { Separator } from "@alumni/ui";
import { getInitials, cn } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { getMyProfile, getNotificationPreferences, updateNotificationPreferences } from "@/lib/member-api";
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
  const { user, logout } = useAuth();
  const qc = useQueryClient();

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

  function toggleNotif(key: keyof Omit<import("@/types").NotificationPreference, "id">, value: boolean) {
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
      [key]: value,
    });
  }

  const fullName = profile ? `${profile.firstName} ${profile.lastName}` : (user?.name ?? "");

  return (
    <div className="p-4 sm:p-6 lg:p-8 xl:p-10 max-w-4xl mx-auto">
      <PageHeader eyebrow="Account controls" title="Settings" description="Manage your account preferences and security." />

      <div className="grid grid-cols-1 gap-5 mt-6">

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
          <Toggle
            checked={notifPrefs?.smsAlerts ?? false}
            onChange={(v) => toggleNotif("smsAlerts", v)}
            label="SMS Notifications"
            description={profile?.phone ? "Also send important alerts to your phone via SMS" : "Add a phone number to your profile to enable SMS alerts"}
          />
          <Toggle
            checked={notifPrefs?.whatsAppAlerts ?? false}
            onChange={(v) => toggleNotif("whatsAppAlerts", v)}
            label="WhatsApp Notifications"
            description={profile?.phone ? "Also send important alerts to your phone via WhatsApp" : "Add a phone number to your profile to enable WhatsApp alerts"}
          />
        </CardContent>
      </Card>

      {/* Security + About */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
      <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-200">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Lock size={18} className="text-primary" />
            Security
          </CardTitle>
          <CardDescription>Update your password to keep your account secure</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <p className="text-sm text-muted-foreground max-w-sm">
              Password changes are managed from your profile page.
            </p>
            <Link href="/profile">
              <Button variant="outline" size="sm" className="shrink-0">Change Password</Button>
            </Link>
          </div>
          <Separator className="my-5" />
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <p className="text-sm text-muted-foreground max-w-sm">
              Sign out of your account on this device.
            </p>
            <Button variant="outline" size="sm" className="shrink-0 text-destructive hover:bg-destructive/10 hover:text-destructive" onClick={logout}>
              Log out
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* About */}
      <Card className="border-border/40 bg-muted/20 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-250">
        <CardContent className="p-6 flex items-center justify-between">
          <div>
            <p className="text-sm font-bold">Alumni Member Portal</p>
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

      </div>
    </div>
  );
}
