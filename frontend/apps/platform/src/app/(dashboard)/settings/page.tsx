"use client";

import { Card, CardContent } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";

export default function PlatformSettingsPage() {
  const { user } = useAuth();

  return (
    <div className="p-7 max-w-[900px]">
      <h1 className="text-[24px] font-bold">Settings</h1>
      <p className="text-muted-foreground text-[13px] mt-1 mb-6">Global platform configuration.</p>

      <Card className="mb-4">
        <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Your account</p></div>
        <CardContent className="p-5 space-y-4 max-w-[480px]">
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
        </CardContent>
      </Card>

      <Card className="mb-4">
        <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Default branding fallback</p></div>
        <CardContent className="p-5 space-y-4 max-w-[480px]">
          <div className="space-y-1.5">
            <Label>Default sender email</Label>
            <Input defaultValue="notifications@yourplatform.com" />
          </div>
          <div className="space-y-1.5">
            <Label>Default primary color</Label>
            <Input defaultValue="#2563EB" />
          </div>
          <Button variant="outline">Save changes</Button>
        </CardContent>
      </Card>

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
