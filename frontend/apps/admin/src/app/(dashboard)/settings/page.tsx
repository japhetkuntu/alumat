"use client";

import { useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import {
  User, Lock, Bell, Shield, Eye, EyeOff, Loader2, CheckCircle2, AlertCircle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { getInitials, cn } from "@/lib/utils";
import { getAdminProfile, changeAdminPassword } from "@/lib/admin-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import { toast } from "sonner";

const NOTIF_KEY = "alumniAdminNotifications";

const defaultNotifPrefs = {
  newMemberRegistrations: true,
  pendingApprovals: true,
  newContributions: false,
  systemAlerts: true,
};

type NotifPrefs = typeof defaultNotifPrefs;

function loadNotifPrefs(): NotifPrefs {
  try {
    const stored = localStorage.getItem(NOTIF_KEY);
    if (stored) return { ...defaultNotifPrefs, ...JSON.parse(stored) };
  } catch { /* silent */ }
  return defaultNotifPrefs;
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

export default function AdminSettingsPage() {
  const { user } = useAuth();

  const [pwForm, setPwForm] = useState({ currentPassword: "", newPassword: "", confirm: "" });
  const [showPw, setShowPw] = useState(false);
  const [notifPrefs, setNotifPrefs] = useState<NotifPrefs>(() => loadNotifPrefs());
  const [notifSaved, setNotifSaved] = useState(false);

  const { data: profile } = useQuery({
    queryKey: ["admin-profile"],
    queryFn: getAdminProfile,
  });

  const pwMut = useMutation({
    mutationFn: () => changeAdminPassword({ currentPassword: pwForm.currentPassword, newPassword: pwForm.newPassword }),
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

  function saveNotifPrefs(prefs: NotifPrefs) {
    setNotifPrefs(prefs);
    localStorage.setItem(NOTIF_KEY, JSON.stringify(prefs));
    setNotifSaved(true);
    setTimeout(() => setNotifSaved(false), 2500);
  }

  const fullName = profile
    ? `${profile.firstName} ${profile.lastName}`
    : (user?.name ?? "Administrator");

  const roleLabel = profile?.role ?? user?.role ?? "Admin";

  return (
    <div className="p-6 lg:p-10 space-y-8 max-w-3xl mx-auto">
      <header className="space-y-1 animate-in fade-in slide-in-from-bottom-3 duration-700">
        <h1 className="text-3xl font-extrabold tracking-tight">Settings</h1>
        <p className="text-muted-foreground text-sm font-medium">Manage your admin account preferences and security.</p>
      </header>

      {/* Account overview */}
      <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <User size={18} className="text-primary" />
            Account
          </CardTitle>
          <CardDescription>Your admin profile information</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex items-center gap-5">
            <Avatar size="lg" className="ring-2 ring-primary/20">
              <AvatarFallback name={fullName} className="text-[16px]">{getInitials(fullName)}</AvatarFallback>
            </Avatar>
            <div className="flex-1 min-w-0">
              <p className="text-lg font-bold truncate">{fullName}</p>
              <p className="text-sm text-muted-foreground truncate">{profile?.email ?? user?.email ?? "—"}</p>
              <div className="flex items-center gap-2 mt-2">
                <Badge variant="info" className="text-[10px]">{roleLabel}</Badge>
                <Badge variant="secondary" className="text-[10px]">
                  <Shield size={10} className="mr-1" />
                  Admin Portal
                </Badge>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Notifications */}
      <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-150">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Bell size={18} className="text-primary" />
            Notifications
            {notifSaved && (
              <span className="ml-auto flex items-center gap-1 text-[11px] text-success font-bold animate-in fade-in duration-300">
                <CheckCircle2 size={13} /> Saved
              </span>
            )}
          </CardTitle>
          <CardDescription>Choose what platform events trigger alerts</CardDescription>
        </CardHeader>
        <CardContent className="divide-y divide-border/40">
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
        </CardContent>
      </Card>

      {/* Security */}
      <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-200">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Lock size={18} className="text-primary" />
            Security
          </CardTitle>
          <CardDescription>Update your admin password</CardDescription>
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

      {/* About platform */}
      <Card className="border-border/40 bg-muted/20 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-250">
        <CardContent className="p-6 flex items-center justify-between">
          <div>
            <p className="text-sm font-bold">UMaT Alumni Admin Portal</p>
            <p className="text-[12px] text-muted-foreground mt-0.5">
              University of Mines &amp; Technology · Administration
            </p>
          </div>
          <Badge variant="secondary" className="text-[11px] gap-1.5">
            <Shield size={11} />
            Secure Admin Access
          </Badge>
        </CardContent>
      </Card>
    </div>
  );
}
