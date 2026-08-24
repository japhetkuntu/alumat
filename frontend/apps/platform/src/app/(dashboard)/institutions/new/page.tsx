"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { BrandPreview } from "@alumni/ui";
import { createInstitution, getPlans } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

const STEPS = ["Institution details", "Branding", "Plan", "First admin", "Review"] as const;

function slugify(v: string) {
  return v.toLowerCase().trim().replace(/[^a-z0-9]+/g, "-").replace(/(^-|-$)/g, "");
}

function generatePassword() {
  return Math.random().toString(36).slice(-6) + Math.random().toString(36).slice(-6).toUpperCase() + "!1";
}

export default function NewInstitutionPage() {
  const router = useRouter();
  const { data: plans = [] } = useQuery({ queryKey: ["plans"], queryFn: getPlans });
  const [step, setStep] = useState(0);
  const [submitting, setSubmitting] = useState(false);
  const [form, setForm] = useState({
    name: "",
    slug: "",
    country: "Ghana",
    contactName: "",
    contactEmail: "",
    appName: "",
    supportEmail: "",
    primaryColor: "#2563eb",
    plan: "Growth",
    adminFirstName: "",
    adminLastName: "",
    adminEmail: "",
    adminPassword: generatePassword(),
  });

  function update<K extends keyof typeof form>(key: K, value: (typeof form)[K]) {
    setForm((f) => ({ ...f, [key]: value }));
  }

  async function next() {
    if (step === STEPS.length - 1) {
      setSubmitting(true);
      try {
        const created = await createInstitution({
          name: form.name,
          slug: form.slug,
          contactName: form.contactName,
          contactEmail: form.contactEmail,
          plan: form.plan,
          portalName: form.appName || undefined,
          supportEmail: form.supportEmail || undefined,
          primaryColorHex: form.primaryColor,
          adminFirstName: form.adminFirstName || form.contactName.split(" ")[0] || "Admin",
          adminLastName: form.adminLastName || form.contactName.split(" ").slice(1).join(" ") || "User",
          adminEmail: form.adminEmail || form.contactEmail,
          adminPassword: form.adminPassword,
        });
        toast.success("Institution created", {
          description: `${created.name} has been onboarded on the ${created.plan} plan. First admin: ${form.adminEmail || form.contactEmail}.`,
        });
        router.push(`/institutions/${created.id}`);
      } catch (error) {
        toast.error("Could not create institution", { description: handleApiError(error) });
      } finally {
        setSubmitting(false);
      }
      return;
    }
    setStep((s) => s + 1);
  }

  const selectedPlan = plans.find((p) => p.name === form.plan) ?? plans[0];

  return (
    <div className="p-7 max-w-[1100px]">
      <p className="text-[12px] text-primary font-semibold mb-2">&larr; Institutions</p>
      <div className="flex items-end justify-between mb-5">
        <div>
          <h1 className="text-[24px] font-bold">Add institution</h1>
          <p className="text-muted-foreground text-[13px] mt-1">Create a tenant and establish its first Institution Portal administrator.</p>
        </div>
        <Button variant="outline" onClick={() => router.push("/institutions")}>Cancel</Button>
      </div>

      <div className="flex border border-border rounded-lg overflow-hidden mb-6 text-[13px]">
        {STEPS.map((s, i) => (
          <div
            key={s}
            className={`flex-1 text-center py-2.5 border-r border-border last:border-r-0 ${
              i === step ? "bg-primary/10 text-primary font-semibold" : "text-muted-foreground"
            }`}
          >
            {i + 1}. {s}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[1fr_340px] gap-5">
        <Card>
          <CardContent className="p-6 space-y-4">
            {step === 0 && (
              <>
                <h2 className="text-[16px] font-semibold mb-1">Institution details</h2>
                <p className="text-[13px] text-muted-foreground mb-4">Start with the record and the person accountable for the tenant relationship.</p>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="sm:col-span-2 space-y-1.5">
                    <Label>Institution name</Label>
                    <Input value={form.name} onChange={(e) => { update("name", e.target.value); update("slug", slugify(e.target.value)); }} placeholder="Greenfield University" />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Subdomain slug</Label>
                    <Input value={form.slug} onChange={(e) => update("slug", slugify(e.target.value))} placeholder="greenfield" />
                    {form.slug && <p className="text-[12px] text-muted-foreground">{form.slug}.yourplatform.com</p>}
                  </div>
                  <div className="space-y-1.5">
                    <Label>Country</Label>
                    <Input value={form.country} onChange={(e) => update("country", e.target.value)} />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Primary contact name</Label>
                    <Input value={form.contactName} onChange={(e) => update("contactName", e.target.value)} placeholder="Dr. Naomi Boateng" />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Primary contact email</Label>
                    <Input value={form.contactEmail} onChange={(e) => update("contactEmail", e.target.value)} placeholder="naomi.boateng@greenfield.edu.gh" />
                  </div>
                </div>
              </>
            )}

            {step === 1 && (
              <>
                <h2 className="text-[16px] font-semibold mb-1">Branding</h2>
                <p className="text-[13px] text-muted-foreground mb-4">Use platform defaults now or configure a customer-facing identity.</p>
                <div className="space-y-4">
                  <div className="space-y-1.5">
                    <Label>Application name</Label>
                    <Input value={form.appName} onChange={(e) => update("appName", e.target.value)} placeholder={`${form.name || "Institution"} Alumni`} />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Support contact</Label>
                    <Input value={form.supportEmail} onChange={(e) => update("supportEmail", e.target.value)} placeholder="support@greenfield.edu.gh" />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Primary color</Label>
                    <div className="flex items-center gap-2">
                      <span className="w-7 h-7 rounded-md border border-border" style={{ background: form.primaryColor }} />
                      <Input value={form.primaryColor} onChange={(e) => update("primaryColor", e.target.value)} className="w-[140px]" />
                    </div>
                  </div>
                  <p className="text-[12.5px] rounded-md p-3" style={{ background: "var(--brand-primary-light)", color: "var(--color-text-info)" }}>
                    Optional for activation — platform defaults apply if you continue without custom assets.
                  </p>
                  <div className="space-y-1.5 pt-2">
                    <Label>Preview</Label>
                    <p className="text-[12px] text-muted-foreground -mt-0.5">
                      This is the actual palette that will be generated from your color — same math used for the live portals and outbound email.
                    </p>
                    <BrandPreview color={form.primaryColor} name={form.appName || form.name || "Institution"} className="pt-1" />
                  </div>
                </div>
              </>
            )}

            {step === 2 && (
              <>
                <h2 className="text-[16px] font-semibold mb-1">Plan</h2>
                <p className="text-[13px] text-muted-foreground mb-4">Choose a starting plan tier. This can be changed later.</p>
                <div className="space-y-3">
                  {plans.map((p) => (
                    <button
                      key={p.id}
                      onClick={() => update("plan", p.name)}
                      className={`w-full text-left border rounded-lg p-4 transition-colors ${
                        form.plan === p.name ? "border-primary bg-primary/5" : "border-border hover:bg-muted/40"
                      }`}
                    >
                      <div className="flex justify-between items-center">
                        <span className="font-semibold text-[14px]">{p.name}</span>
                        <span className="font-bold">{p.price != null ? `$${p.price}/mo` : "Custom"}</span>
                      </div>
                      <p className="text-[12.5px] text-muted-foreground mt-1">
                        {p.memberLimit != null ? p.memberLimit.toLocaleString() : "Unlimited"} members &middot; {p.modules.join(", ")}
                      </p>
                    </button>
                  ))}
                </div>
              </>
            )}

            {step === 3 && (
              <>
                <h2 className="text-[16px] font-semibold mb-1">First admin</h2>
                <p className="text-[13px] text-muted-foreground mb-4">This person becomes the institution&apos;s first Institution Portal SuperAdmin.</p>
                <div className="space-y-4">
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <Label>First name</Label>
                      <Input value={form.adminFirstName} onChange={(e) => update("adminFirstName", e.target.value)} placeholder={form.contactName.split(" ")[0] || "Amelia"} />
                    </div>
                    <div className="space-y-1.5">
                      <Label>Last name</Label>
                      <Input value={form.adminLastName} onChange={(e) => update("adminLastName", e.target.value)} placeholder={form.contactName.split(" ").slice(1).join(" ") || "Owusu"} />
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <Label>Email</Label>
                    <Input value={form.adminEmail} onChange={(e) => update("adminEmail", e.target.value)} placeholder={form.contactEmail || "amelia.owusu@greenfield.edu.gh"} />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Temporary password</Label>
                    <Input value={form.adminPassword} onChange={(e) => update("adminPassword", e.target.value)} />
                    <p className="text-[12px] text-muted-foreground">Share this securely with the admin — they should change it after first login.</p>
                  </div>
                </div>
              </>
            )}

            {step === 4 && (
              <>
                <h2 className="text-[16px] font-semibold mb-1">Review</h2>
                <p className="text-[13px] text-muted-foreground mb-4">Confirm everything looks right before creating this tenant.</p>
                <div className="space-y-3 text-[13.5px]">
                  <div className="flex justify-between border-b border-border pb-2"><span className="text-muted-foreground">Institution</span><span className="font-semibold">{form.name || "—"}</span></div>
                  <div className="flex justify-between border-b border-border pb-2"><span className="text-muted-foreground">Tenant URL</span><span className="font-semibold font-mono">{form.slug || "—"}.yourplatform.com</span></div>
                  <div className="flex justify-between border-b border-border pb-2"><span className="text-muted-foreground">Plan</span><span className="font-semibold">{form.plan}</span></div>
                  <div className="flex justify-between"><span className="text-muted-foreground">First admin</span><span className="font-semibold">{form.adminFirstName || "—"} {form.adminLastName} ({form.adminEmail || form.contactEmail || "—"})</span></div>
                </div>
              </>
            )}

            <div className="flex justify-between pt-4">
              <Button variant="outline" disabled={step === 0 || submitting} onClick={() => setStep((s) => s - 1)}>Back</Button>
              <Button onClick={next} disabled={submitting}>
                {submitting ? "Creating…" : step === STEPS.length - 1 ? "Create institution" : `Continue to ${STEPS[step + 1]?.toLowerCase()}`}
              </Button>
            </div>
          </CardContent>
        </Card>

        <Card className="h-fit">
          <CardContent className="p-5">
            <div className="flex justify-between items-start mb-3">
              <div>
                <p className="text-[14px] font-semibold">Creation summary</p>
                <p className="text-[12px] text-muted-foreground">Updates as the setup progresses.</p>
              </div>
            </div>
            <div className="space-y-2.5 text-[13px]">
              <div className="flex justify-between border-t border-border pt-2.5"><span className="text-muted-foreground">Tenant URL</span><span className="font-semibold font-mono">{form.slug || "—"}.yourplatform.com</span></div>
              <div className="flex justify-between border-t border-border pt-2.5"><span className="text-muted-foreground">Initial status</span><span className="font-semibold">Trial &middot; 14 days</span></div>
              <div className="flex justify-between border-t border-border pt-2.5"><span className="text-muted-foreground">Plan</span><span className="font-semibold">{form.plan}</span></div>
              <div className="flex justify-between border-t border-border pt-2.5"><span className="text-muted-foreground">First admin</span><span className="font-semibold">{form.adminEmail || form.contactEmail || "Not added"}</span></div>
            </div>
            {selectedPlan && (
              <div className="mt-4 bg-muted/40 rounded-md p-3 text-[12.5px]">
                <p className="font-semibold mb-1">{selectedPlan.name} trial includes</p>
                <p className="text-muted-foreground">
                  {selectedPlan.memberLimit != null ? selectedPlan.memberLimit.toLocaleString() : "Unlimited"} members &middot; {selectedPlan.storageLimitGb != null ? `${selectedPlan.storageLimitGb} GB storage` : "Unlimited storage"} &middot; {selectedPlan.modules.join(", ")}.
                </p>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
