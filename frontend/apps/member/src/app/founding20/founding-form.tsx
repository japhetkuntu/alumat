"use client";

import { useState, type FormEvent } from "react";
import { ArrowRight, Check, Input, Label, Button, FormError, Textarea, Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@alumni/ui";
import { memberClient, handleApiError } from "@/lib/api-client";
import { getMarketingShareId } from "@/lib/marketing-attribution";
import { trackMarketing } from "../_marketing/analytics";

const TYPES = ["University", "Senior High School", "Church", "Professional association", "Alumni association", "Other"];
const SIZES = ["Under 500", "500 to 2,000", "2,000 to 10,000", "More than 10,000", "Not sure"];
const AUTHORITY = ["Yes", "Part of leadership", "No"];

const empty = { institutionName: "", organizationType: "", contactName: "", contactRole: "", contactPhone: "", contactEmail: "", estimatedMemberCount: "", challenge: "", authority: "" };

export function FoundingForm() {
  const [form, setForm] = useState(empty);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);
  const set = (k: keyof typeof empty) => (v: string) => setForm((f) => ({ ...f, [k]: v }));

  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!form.institutionName.trim() || !form.contactName.trim() || !form.contactEmail.trim() || !form.contactPhone.trim()) {
      setError("Enter the institution, your name, phone or WhatsApp number, and email so we can reach you."); return;
    }
    if (!form.authority) { setError("Tell us whether you are authorised to represent the institution."); return; }
    setError(null); setSubmitting(true);
    try {
      await memberClient.post("/public/founding-20-applications", {
        marketingShareId: getMarketingShareId(),
        institutionName: form.institutionName.trim(), organizationType: form.organizationType || undefined,
        contactName: form.contactName.trim(), contactRole: form.contactRole.trim() || undefined,
        contactPhone: form.contactPhone.trim(), contactEmail: form.contactEmail.trim(),
        estimatedMemberCount: form.estimatedMemberCount || undefined, challenge: form.challenge.trim() || undefined,
        authority: form.authority,
      });
      setDone(true); trackMarketing("founding20_apply_success");
    } catch (err) { setError(handleApiError(err)); }
    finally { setSubmitting(false); }
  }

  if (done) return (
    <div role="status" className="border border-border bg-background p-7 sm:p-9">
      <Check size={26} className="mb-5 text-primary" />
      <h3 className="text-2xl font-semibold">Thank you. Your application is in.</h3>
      <p className="mt-3 leading-relaxed text-muted-foreground">
        We will contact you personally at <span className="font-medium text-foreground">{form.contactEmail}</span> or on {form.contactPhone} to arrange a 20-minute conversation about {form.institutionName}.
      </p>
    </div>
  );

  return (
    <form onSubmit={submit} className="space-y-5 border border-border bg-background p-5 sm:p-8">
      <div>
        <Label htmlFor="f20-institution" required>Institution or community name</Label>
        <Input id="f20-institution" autoComplete="organization" required maxLength={200} value={form.institutionName} onChange={(e) => set("institutionName")(e.target.value)} />
      </div>
      <div>
        <Label htmlFor="f20-type">Institution type</Label>
        <Select value={form.organizationType || undefined} onValueChange={set("organizationType")}>
          <SelectTrigger id="f20-type" className="w-full max-w-none"><SelectValue placeholder="Choose one" /></SelectTrigger>
          <SelectContent>{TYPES.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
        </Select>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div><Label htmlFor="f20-name" required>Your name</Label><Input id="f20-name" autoComplete="name" required maxLength={200} value={form.contactName} onChange={(e) => set("contactName")(e.target.value)} /></div>
        <div><Label htmlFor="f20-role">Your role</Label><Input id="f20-role" maxLength={150} placeholder="e.g. President, Headmaster" value={form.contactRole} onChange={(e) => set("contactRole")(e.target.value)} /></div>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div><Label htmlFor="f20-phone" required>Phone or WhatsApp</Label><Input id="f20-phone" type="tel" autoComplete="tel" required maxLength={40} value={form.contactPhone} onChange={(e) => set("contactPhone")(e.target.value)} /></div>
        <div><Label htmlFor="f20-email" required>Email</Label><Input id="f20-email" type="email" autoComplete="email" required maxLength={254} value={form.contactEmail} onChange={(e) => set("contactEmail")(e.target.value)} /></div>
      </div>
      <div>
        <Label htmlFor="f20-size">Approximate community size</Label>
        <Select value={form.estimatedMemberCount || undefined} onValueChange={set("estimatedMemberCount")}>
          <SelectTrigger id="f20-size" className="w-full max-w-none"><SelectValue placeholder="Choose one" /></SelectTrigger>
          <SelectContent>{SIZES.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
        </Select>
      </div>
      <div>
        <Label htmlFor="f20-challenge">What is your biggest challenge managing your community?</Label>
        <Textarea id="f20-challenge" rows={4} maxLength={1500} value={form.challenge} onChange={(e) => set("challenge")(e.target.value)} />
      </div>
      <fieldset>
        <legend className="mb-2 text-sm font-medium">Are you authorised to represent the institution? <span className="text-destructive">*</span></legend>
        <div className="flex flex-wrap gap-x-6 gap-y-2">
          {AUTHORITY.map((a) => (
            <label key={a} className="flex cursor-pointer items-center gap-2 text-sm">
              <input type="radio" name="f20-authority" className="h-4 w-4" checked={form.authority === a} onChange={() => set("authority")(a)} />
              {a}
            </label>
          ))}
        </div>
      </fieldset>
      <FormError message={error} />
      <Button type="submit" disabled={submitting} isLoading={submitting} loadingText="Sending your application…" className="min-h-11 w-full font-semibold">
        Apply to become a Founding Institution <ArrowRight size={15} />
      </Button>
      <p className="text-sm leading-relaxed text-muted-foreground">This is an application, not an agreement. We use these details only to respond to you.</p>
    </form>
  );
}
