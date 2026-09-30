"use client";
import Link from "next/link";
import { useState, type FormEvent } from "react";
import { ArrowRight, Check, Input, Label, Button, FormError, Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@alumni/ui";
import { memberClient, handleApiError } from "@/lib/api-client";
import { trackMarketing } from "./analytics";

export function WalkthroughForm() {
  const [form, setForm] = useState({ institutionName: "", contactName: "", contactEmail: "", mainInterest: "" });
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [started, setStarted] = useState(false);
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!form.institutionName.trim() || !form.contactName.trim() || !form.contactEmail.trim()) {
      setError("Enter your organisation, name and email so we can get back to you."); return;
    }
    setError(null); setSubmitting(true);
    try {
      await memberClient.post("/public/walkthrough-requests", {
        institutionName: form.institutionName.trim(), contactName: form.contactName.trim(),
        contactEmail: form.contactEmail.trim(), mainInterest: form.mainInterest || undefined,
      });
      setSubmitted(true); trackMarketing("walkthrough_request_success");
    } catch (err) { setError(handleApiError(err)); }
    finally { setSubmitting(false); }
  }
  if (submitted) return (
    <div role="status" className="border border-border rounded-2xl bg-background p-7 sm:p-9">
      <Check size={28} className="text-primary mb-5" />
      <h3 className="text-2xl font-semibold mb-3">Your request is in.</h3>
      <p className="text-muted-foreground leading-relaxed">We’ll contact you at <span className="font-medium text-foreground">{form.contactEmail}</span> to arrange a walkthrough for {form.institutionName}.</p>
      <p className="text-base text-muted-foreground mt-4">You can ask about member records, payments and setup before deciding to proceed.</p>
      <Button variant="outline" className="mt-6" onClick={() => { setSubmitted(false); setForm({ institutionName: "", contactName: "", contactEmail: "", mainInterest: "" }); setStarted(false); }}>Send another enquiry</Button>
    </div>
  );
  return (
    <form onSubmit={submit} onFocus={() => { if (!started) { setStarted(true); trackMarketing("walkthrough_request_start"); } }}
      className="border border-border rounded-2xl bg-background p-6 sm:p-8 space-y-5">
      <div><h3 className="text-xl font-semibold">Arrange a walkthrough</h3><p className="text-base text-muted-foreground mt-2">Tell us where to reach you. We’ll take the setup details later.</p></div>
      <div><Label htmlFor="walkthrough-organisation" required>Organisation name</Label><Input id="walkthrough-organisation" name="organization" autoComplete="organization" required maxLength={200} value={form.institutionName} onChange={e => setForm({...form, institutionName: e.target.value})} /></div>
      <div className="grid sm:grid-cols-2 gap-4">
        <div><Label htmlFor="walkthrough-name" required>Your name</Label><Input id="walkthrough-name" name="name" autoComplete="name" required maxLength={200} value={form.contactName} onChange={e => setForm({...form, contactName: e.target.value})} /></div>
        <div><Label htmlFor="walkthrough-email" required>Email address</Label><Input id="walkthrough-email" name="email" type="email" autoComplete="email" required maxLength={254} value={form.contactEmail} onChange={e => setForm({...form, contactEmail: e.target.value})} /></div>
      </div>
      <div><Label htmlFor="walkthrough-interest">What would you like to see? <span className="font-normal text-muted-foreground">(optional)</span></Label>
        <Select value={form.mainInterest || undefined} onValueChange={mainInterest => setForm({...form, mainInterest})}>
          <SelectTrigger id="walkthrough-interest" className="w-full max-w-none"><SelectValue placeholder="Choose what you’d like to explore" /></SelectTrigger>
          <SelectContent className="mk-select-menu">
            {["Member records and registration", "Dues and fundraising", "Events and RSVPs", "Jobs and mentorship", "Groups and chapters", "A general walkthrough"].map(topic => <SelectItem className="mk-select-option" key={topic} value={topic}>{topic}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>
      <FormError message={error} />
      <Button type="submit" disabled={submitting} isLoading={submitting} loadingText="Sending your request…" className="w-full min-h-11 font-semibold">Request a walkthrough <ArrowRight size={15} /></Button>
      <p className="text-sm leading-relaxed text-muted-foreground">This is an enquiry, with no agreement to sign. We use these details to respond to your request. Read our <Link href="/privacy" className="underline underline-offset-2 text-foreground">privacy policy</Link>.</p>
    </form>
  );
}

