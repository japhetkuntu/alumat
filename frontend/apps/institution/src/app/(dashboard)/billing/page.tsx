"use client";

import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";

function UsageBar({ label, used, limit, unit = "" }: { label: string; used: number; limit: number; unit?: string }) {
  const pct = Math.min(100, Math.round((used / limit) * 100));
  return (
    <div className="mb-4">
      <div className="flex justify-between text-[13px] mb-1.5">
        <span className="font-semibold">{label}</span>
        <span className="text-muted-foreground">{used.toLocaleString()}{unit} / {limit.toLocaleString()}{unit}</span>
      </div>
      <div className="h-[7px] bg-muted rounded-full overflow-hidden">
        <div className={pct > 85 ? "h-full rounded-full bg-amber-500" : "h-full rounded-full bg-primary"} style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

const invoices = [
  { id: "INV-2025-06", amount: "$1,250.00" },
  { id: "INV-2025-05", amount: "$1,250.00" },
  { id: "INV-2025-04", amount: "$1,250.00" },
];

export default function PlanBillingPage() {
  return (
    <div className="p-[26px] max-w-[1240px] mx-auto">
      <header className="flex items-end justify-between gap-4 mb-6 animate-in fade-in slide-in-from-bottom-3 duration-700">
        <div className="space-y-1">
          <h1 className="text-[25px] font-bold m-0">Plan &amp; Billing</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">Manage your institution subscription without platform support.</p>
        </div>
        <Button disabled>Change plan</Button>
      </header>

      <p className="text-[12px] text-muted-foreground bg-muted/50 border border-border/40 rounded-lg px-3 py-2 mb-6">
        Billing isn&apos;t wired to the platform yet — figures below are illustrative until the subscription service exists.
      </p>

      <div className="grid grid-cols-1 lg:grid-cols-[1.2fr_.8fr] gap-4">
        <Card className="border-border/40">
          <CardContent className="p-6">
            <p className="font-semibold text-[15px] mb-1">Current plan</p>
            <p className="text-[12.5px] text-muted-foreground mb-4">Growth &middot; renews 18 Jun 2026</p>

            <div className="rounded-lg border border-primary/30 bg-primary/5 p-4 mb-5">
              <div className="flex items-start justify-between">
                <div>
                  <span className="text-[24px] font-bold">$1,250</span>
                  <span className="text-[12px] text-muted-foreground"> / month</span>
                  <p className="text-[13px] text-muted-foreground mt-1">Advanced reporting, configurable workflows, expanded support.</p>
                </div>
                <Badge variant="success">Current</Badge>
              </div>
            </div>

            <p className="font-semibold text-[14px] mb-3">Usage against limits</p>
            <UsageBar label="Members" used={8420} limit={15000} />
            <UsageBar label="Storage" used={42} limit={100} unit=" GB" />
            <UsageBar label="Active campaigns" used={4} limit={10} />
            <p className="text-[12.5px] rounded-md p-3 mt-2" style={{ background: "var(--brand-primary-light)", color: "var(--color-text-info)" }}>
              Usage is healthy. You have 6,580 member records remaining on this plan.
            </p>
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card className="border-border/40">
            <CardContent className="p-5">
              <p className="font-semibold text-[14px] mb-1">Payment method</p>
              <p className="text-[12px] text-muted-foreground mb-3">Primary payment method</p>
              <div className="flex justify-between items-center border-t border-border pt-3">
                <span className="font-semibold text-[13.5px]">Visa ending 4242</span>
                <span className="text-[12.5px] text-muted-foreground">08/27</span>
              </div>
              <Button variant="outline" size="sm" className="mt-3 w-full" disabled>Update payment details</Button>
            </CardContent>
          </Card>

          <Card className="border-border/40">
            <CardContent className="p-5">
              <p className="font-semibold text-[14px] mb-3">Invoice history</p>
              {invoices.map((inv) => (
                <div key={inv.id} className="flex justify-between items-center border-t border-border py-2.5 text-[13px]">
                  <span className="font-semibold">{inv.id}</span>
                  <span className="text-muted-foreground">{inv.amount}</span>
                  <button className="text-primary font-semibold text-[12.5px] hover:underline" disabled>Receipt</button>
                </div>
              ))}
              <p className="text-[12px] text-success font-semibold mt-2">All invoices paid</p>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
