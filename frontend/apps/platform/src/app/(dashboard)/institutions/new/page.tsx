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
import { createInstitution, getBaseDomains } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

const STEPS = ["Institution details", "Branding", "Payments & payouts", "First admin", "Review"] as const;

function slugify(v: string) {
  return v.toLowerCase().trim().replace(/[^a-z0-9]+/g, "-").replace(/(^-|-$)/g, "");
}

function generatePassword() {
  return Math.random().toString(36).slice(-6) + Math.random().toString(36).slice(-6).toUpperCase() + "!1";
}

export default function NewInstitutionPage() {
  const router = useRouter();
  const { data: baseDomains } = useQuery({ queryKey: ["base-domains"], queryFn: getBaseDomains, staleTime: Infinity });
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
    secondaryColor: "",
    platformFeePercentage: "5",
    settlementBankName: "",
    settlementBankCode: "",
    settlementAccountNumber: "",
    settlementAccountName: "",
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
          portalName: form.appName || undefined,
          supportEmail: form.supportEmail || undefined,
          primaryColorHex: form.primaryColor,
          secondaryColorHex: form.secondaryColor || undefined,
          platformFeePercentage: form.platformFeePercentage ? Number(form.platformFeePercentage) : undefined,
          settlementBankCode: form.settlementBankCode || undefined,
          settlementBankName: form.settlementBankName || undefined,
          settlementAccountNumber: form.settlementAccountNumber || undefined,
          settlementAccountName: form.settlementAccountName || undefined,
          adminFirstName: form.adminFirstName || form.contactName.split(" ")[0] || "Admin",
          adminLastName: form.adminLastName || form.contactName.split(" ").slice(1).join(" ") || "User",
          adminEmail: form.adminEmail || form.contactEmail,
          adminPassword: form.adminPassword,
        });
        toast.success("Institution created", {
          description: `${created.name} has been onboarded. First admin: ${form.adminEmail || form.contactEmail}.`,
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
                    {form.slug && baseDomains && (
                      <div className="text-[12px] text-muted-foreground space-y-0.5">
                        <p>Member portal: {form.slug}.{baseDomains.memberBaseDomain}</p>
                        <p>Institution portal: {form.slug}.{baseDomains.adminBaseDomain}</p>
                      </div>
                    )}
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
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <Label>Primary color</Label>
                      <div className="flex items-center gap-2">
                        <span className="w-7 h-7 rounded-md border border-border" style={{ background: form.primaryColor }} />
                        <Input value={form.primaryColor} onChange={(e) => update("primaryColor", e.target.value)} className="w-[140px]" />
                      </div>
                    </div>
                    <div className="space-y-1.5">
                      <Label>Secondary color</Label>
                      <div className="flex items-center gap-2">
                        <span className="w-7 h-7 rounded-md border border-border" style={{ background: form.secondaryColor || "#e2e8f0" }} />
                        <Input value={form.secondaryColor} onChange={(e) => update("secondaryColor", e.target.value)} className="w-[140px]" placeholder="Optional" />
                      </div>
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
                    <BrandPreview color={form.primaryColor} secondaryColor={form.secondaryColor || undefined} name={form.appName || form.name || "Institution"} className="pt-1" />
                  </div>
                </div>
              </>
            )}

            {step === 2 && (
              <>
                <h2 className="text-[16px] font-semibold mb-1">Payments &amp; payouts</h2>
                <p className="text-[13px] text-muted-foreground mb-4">
                  Set the platform fee taken on this institution&apos;s collections and where their share settles.
                </p>
                <div className="space-y-4">
                  <div className="space-y-1.5">
                    <Label>Platform fee</Label>
                    <div className="flex items-center gap-2">
                      <Input
                        type="number"
                        min={0}
                        max={100}
                        step="0.1"
                        value={form.platformFeePercentage}
                        onChange={(e) => update("platformFeePercentage", e.target.value)}
                        className="w-[120px]"
                      />
                      <span className="text-[13px] text-muted-foreground">% of each confirmed payment</span>
                    </div>
                  </div>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <Label>Settlement bank name</Label>
                      <Input value={form.settlementBankName} onChange={(e) => update("settlementBankName", e.target.value)} placeholder="GCB Bank" />
                    </div>
                    <div className="space-y-1.5">
                      <Label>Settlement bank code</Label>
                      <Input value={form.settlementBankCode} onChange={(e) => update("settlementBankCode", e.target.value)} placeholder="040100" />
                    </div>
                    <div className="space-y-1.5">
                      <Label>Account number</Label>
                      <Input value={form.settlementAccountNumber} onChange={(e) => update("settlementAccountNumber", e.target.value)} placeholder="1234567890" />
                    </div>
                    <div className="space-y-1.5">
                      <Label>Account name</Label>
                      <Input value={form.settlementAccountName} onChange={(e) => update("settlementAccountName", e.target.value)} placeholder="Greenfield University Alumni Association" />
                    </div>
                  </div>
                  <p className="text-[12.5px] rounded-md p-3" style={{ background: "var(--brand-primary-light)", color: "var(--color-text-info)" }}>
                    Optional — leave blank and configure later from the institution&apos;s Payments tab. Providing a bank code and account number here sets up their payout account automatically.
                  </p>
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
                  <div className="flex justify-between border-b border-border pb-2"><span className="text-muted-foreground">Member portal</span><span className="font-semibold font-mono">{form.slug || "—"}{baseDomains ? `.${baseDomains.memberBaseDomain}` : ""}</span></div>
                  <div className="flex justify-between border-b border-border pb-2"><span className="text-muted-foreground">Institution portal</span><span className="font-semibold font-mono">{form.slug || "—"}{baseDomains ? `.${baseDomains.adminBaseDomain}` : ""}</span></div>
                  <div className="flex justify-between border-b border-border pb-2"><span className="text-muted-foreground">Platform fee</span><span className="font-semibold">{form.platformFeePercentage || "0"}%</span></div>
                  <div className="flex justify-between border-b border-border pb-2"><span className="text-muted-foreground">Settlement account</span><span className="font-semibold">{form.settlementAccountName || "Not configured"}</span></div>
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
              <div className="flex justify-between border-t border-border pt-2.5"><span className="text-muted-foreground">Member portal</span><span className="font-semibold font-mono">{form.slug || "—"}{baseDomains ? `.${baseDomains.memberBaseDomain}` : ""}</span></div>
              <div className="flex justify-between border-t border-border pt-2.5"><span className="text-muted-foreground">Institution portal</span><span className="font-semibold font-mono">{form.slug || "—"}{baseDomains ? `.${baseDomains.adminBaseDomain}` : ""}</span></div>
              <div className="flex justify-between border-t border-border pt-2.5"><span className="text-muted-foreground">Initial status</span><span className="font-semibold">Trial &middot; 14 days</span></div>
              <div className="flex justify-between border-t border-border pt-2.5"><span className="text-muted-foreground">Platform fee</span><span className="font-semibold">{form.platformFeePercentage || "0"}%</span></div>
              <div className="flex justify-between border-t border-border pt-2.5"><span className="text-muted-foreground">First admin</span><span className="font-semibold">{form.adminEmail || form.contactEmail || "Not added"}</span></div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
