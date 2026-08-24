"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { FormError } from "@alumni/ui";
import { createPlan, getAuditLog, getPlans } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

export default function PlansPage() {
  const queryClient = useQueryClient();
  const { data: plans = [] } = useQuery({ queryKey: ["plans"], queryFn: getPlans });
  const { data: auditPage } = useQuery({
    queryKey: ["audit-log", { page: 1, pageSize: 4 }],
    queryFn: () => getAuditLog({ page: 1, pageSize: 4 }),
  });
  const recentActivity = auditPage?.results ?? [];

  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ name: "", price: "", memberLimit: "", storageLimitGb: "", modules: "", supportLevel: "Standard support" });
  const [error, setError] = useState<string | null>(null);

  const createMutation = useMutation({
    mutationFn: () =>
      createPlan({
        name: form.name,
        price: form.price ? Number(form.price) : undefined,
        memberLimit: form.memberLimit ? Number(form.memberLimit) : undefined,
        storageLimitGb: form.storageLimitGb ? Number(form.storageLimitGb) : undefined,
        modules: form.modules.split(",").map((m) => m.trim()).filter(Boolean),
        supportLevel: form.supportLevel,
      }),
    onSuccess: () => {
      toast.success("Plan created");
      queryClient.invalidateQueries({ queryKey: ["plans"] });
      setOpen(false);
      setForm({ name: "", price: "", memberLimit: "", storageLimitGb: "", modules: "", supportLevel: "Standard support" });
      setError(null);
    },
    onError: (e) => {
      const msg = handleApiError(e);
      setError(msg);
      toast.error(msg);
    },
  });

  return (
    <div className="p-7 max-w-[1500px]">
      <div className="flex items-end justify-between mb-6">
        <div>
          <h1 className="text-[24px] font-bold">Plans &amp; Features</h1>
          <p className="text-muted-foreground text-[13px] mt-1">Define what institutions can buy and what each tier includes.</p>
        </div>
        <Button onClick={() => setOpen(true)}>Create plan</Button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[1.3fr_1fr] gap-4">
        <Card>
          <div className="px-5 py-4 border-b border-border">
            <p className="text-[14px] font-semibold">Published plans</p>
            <p className="text-[12px] text-muted-foreground">{plans.length} active tiers &middot; {plans.reduce((s, p) => s + p.subscriberCount, 0)} subscribed institutions</p>
          </div>
          <CardContent className="p-5 space-y-3">
            {plans.map((p) => (
              <div key={p.id} className="border border-border rounded-lg p-4">
                <div className="flex justify-between items-start">
                  <div>
                    <div className="font-semibold text-[15px] flex items-center gap-2">
                      {p.name}
                      {p.isMostUsed && <Badge variant="success">Most used</Badge>}
                    </div>
                    <p className="text-[12.5px] text-muted-foreground mt-1">{p.modules.join(", ")} &middot; {p.subscriberCount} subscribers</p>
                  </div>
                  <p className="text-[19px] font-bold">{p.price != null ? `$${p.price}` : "Custom"}{p.price != null && <span className="text-[12px] font-normal text-muted-foreground"> /mo</span>}</p>
                </div>
                <div className="flex gap-2 flex-wrap mt-3">
                  <Badge variant="info">{p.memberLimit != null ? `${p.memberLimit.toLocaleString()} members` : "Unlimited members"}</Badge>
                  <Badge variant="info">{p.storageLimitGb != null ? `${p.storageLimitGb} GB storage` : "Unlimited storage"}</Badge>
                  <Badge variant="info">{p.supportLevel}</Badge>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Recent governance activity</p></div>
          <CardContent className="p-0">
            {recentActivity.length === 0 && <p className="px-5 py-6 text-[13px] text-muted-foreground">No activity recorded yet.</p>}
            {recentActivity.map((a) => (
              <div key={a.id} className="px-5 py-3.5 border-b border-border last:border-0 text-[13px]">
                <p><b>{a.actor}</b> {a.action}</p>
                <p className="text-muted-foreground text-[12px]">{new Date(a.timestamp).toLocaleString()} &middot; {a.target}</p>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <Dialog open={open} onOpenChange={(o) => { setOpen(o); if (!o) setError(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create plan</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 mt-2">
            <div className="space-y-1.5">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="Pro" />
            </div>
            <div className="space-y-1.5">
              <Label>Monthly price (USD, leave blank for custom pricing)</Label>
              <Input type="number" value={form.price} onChange={(e) => setForm((f) => ({ ...f, price: e.target.value }))} />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Member limit (blank = unlimited)</Label>
                <Input type="number" value={form.memberLimit} onChange={(e) => setForm((f) => ({ ...f, memberLimit: e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>Storage GB (blank = unlimited)</Label>
                <Input type="number" value={form.storageLimitGb} onChange={(e) => setForm((f) => ({ ...f, storageLimitGb: e.target.value }))} />
              </div>
            </div>
            <div className="space-y-1.5">
              <Label>Modules (comma-separated)</Label>
              <Input value={form.modules} onChange={(e) => setForm((f) => ({ ...f, modules: e.target.value }))} placeholder="Campaigns, Jobs, Resources" />
            </div>
            <div className="space-y-1.5">
              <Label>Support level</Label>
              <Input value={form.supportLevel} onChange={(e) => setForm((f) => ({ ...f, supportLevel: e.target.value }))} />
            </div>
            <FormError message={error} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button onClick={() => createMutation.mutate()} disabled={createMutation.isPending || !form.name}>
              {createMutation.isPending ? "Creating…" : "Create plan"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
