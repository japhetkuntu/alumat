"use client";

import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { Lock, Eye, EyeOff, AlertCircle, Loader2 } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { changePlatformPassword } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

export default function PlatformSettingsPage() {
  const { user, logout, setSession } = useAuth();
  const [pwForm, setPwForm] = useState({ currentPassword: "", newPassword: "", confirm: "" });
  const [showPw, setShowPw] = useState(false);

  const pwMut = useMutation({
    mutationFn: () => changePlatformPassword(pwForm.currentPassword, pwForm.newPassword),
    onSuccess: (data) => {
      // The backend invalidates the old refresh token and issues fresh ones
      // alongside the password change — without persisting these, the next
      // silent refresh would fail and silently log the user out.
      if (user) setSession(user, data.tokens);
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

  return (
    <div className="p-7 max-w-[1100px]">
      <h1 className="text-[24px] font-bold">Settings</h1>
      <p className="text-muted-foreground text-[13px] mt-1 mb-6">Global platform configuration.</p>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 mb-4">
        <Card>
          <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Your account</p></div>
          <CardContent className="p-5 space-y-4">
            <div className="space-y-1.5">
              <Label>Name</Label>
              <Input value={user?.name ?? ""} disabled />
            </div>
            <div className="space-y-1.5">
              <Label>Email</Label>
              <Input value={user?.email ?? ""} disabled />
            </div>
            <div className="space-y-1.5">
              <Label>Role</Label>
              <Input value={user?.role ?? ""} disabled />
            </div>
            <div className="pt-2 flex items-center justify-between gap-4 border-t border-border">
              <div className="pt-3">
                <p className="text-[13px] font-semibold">Log out</p>
                <p className="text-[11.5px] text-muted-foreground mt-0.5">Sign out of this device.</p>
              </div>
              <Button variant="outline" size="sm" className="shrink-0 text-destructive hover:bg-destructive/10 hover:text-destructive" onClick={logout}>
                Log out
              </Button>
            </div>
          </CardContent>
        </Card>

        <Card>
          <div className="px-5 py-4 border-b border-border flex items-center gap-2">
            <Lock size={15} className="text-primary" />
            <p className="text-[14px] font-semibold">Security</p>
          </div>
          <CardContent className="p-5">
            <form onSubmit={handlePwSubmit} className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="currentPw" className="text-[13px] font-semibold">Current password</Label>
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
                <Label htmlFor="newPw" className="text-[13px] font-semibold">New password</Label>
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
                <Label htmlFor="confirmPw" className="text-[13px] font-semibold">Confirm new password</Label>
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
                Update password
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardContent className="p-5 flex items-center justify-between">
          <div>
            <p className="text-[13.5px] font-semibold">Platform Portal</p>
            <p className="text-[12px] text-muted-foreground mt-0.5">Internal tool for onboarding and supporting every institution on the platform.</p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
