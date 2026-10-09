"use client";

import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button, Input } from "@alumni/ui";
import { suggestOpportunity } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";

const TYPES = ["Full-time", "Part-time", "Contract", "Internship", "Scholarship", "Mentorship", "Volunteering", "Business", "Partnership", "Other"];
const empty = { title: "", company: "", location: "", type: "Full-time", description: "", applyUrl: "", deadline: "" };

/** A member suggesting a role, scholarship or opening. Held for an administrator's review; nobody sees it until approved. */
export function SuggestOpportunity({ initiallyOpen = false }: { initiallyOpen?: boolean }) {
  const [open, setOpen] = useState(initiallyOpen);
  const [form, setForm] = useState(empty);
  const set = (k: keyof typeof empty, v: string) => setForm((f) => ({ ...f, [k]: v }));
  const submit = useMutation({
    mutationFn: () => suggestOpportunity(form),
    onSuccess: () => { toast.success("Thank you. An administrator will review it before it is shared."); setForm(empty); setOpen(false); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (!open) return <Button variant="outline" onClick={() => setOpen(true)}>Suggest an opportunity</Button>;
  const field = "space-y-1.5";
  const label = "text-[13px] font-semibold";
  return (
    <form onSubmit={(e) => { e.preventDefault(); submit.mutate(); }} className="border border-border bg-card p-5 sm:p-6 space-y-4 max-w-2xl">
      <div>
        <h2 className="text-[17px] font-bold">Suggest an opportunity</h2>
        <p className="text-[13.5px] text-muted-foreground mt-1">Jobs, internships, scholarships, volunteering or business openings. An administrator reviews it before the community sees it.</p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className={field}><label className={label} htmlFor="so-title">Title</label><Input id="so-title" required minLength={3} maxLength={120} value={form.title} onChange={(e) => set("title", e.target.value)} /></div>
        <div className={field}><label className={label} htmlFor="so-company">Offered by</label><Input id="so-company" required minLength={2} maxLength={120} value={form.company} onChange={(e) => set("company", e.target.value)} /></div>
        <div className={field}><label className={label} htmlFor="so-type">Type</label>
          <select id="so-type" value={form.type} onChange={(e) => set("type", e.target.value)} className="h-11 w-full border border-border bg-background px-3 text-[14px]">
            {TYPES.map((t) => <option key={t}>{t}</option>)}
          </select></div>
        <div className={field}><label className={label} htmlFor="so-loc">Where</label><Input id="so-loc" placeholder="City, or Remote" value={form.location} onChange={(e) => set("location", e.target.value)} /></div>
        <div className={field}><label className={label} htmlFor="so-url">Link to apply or learn more</label><Input id="so-url" type="url" placeholder="https://" value={form.applyUrl} onChange={(e) => set("applyUrl", e.target.value)} /></div>
        <div className={field}><label className={label} htmlFor="so-dl">Closing date (optional)</label><Input id="so-dl" type="date" value={form.deadline} onChange={(e) => set("deadline", e.target.value)} /></div>
      </div>
      <div className={field}>
        <label className={label} htmlFor="so-desc">Details</label>
        <textarea id="so-desc" rows={4} maxLength={4000} value={form.description} onChange={(e) => set("description", e.target.value)} className="w-full border border-border bg-background p-3 text-[14px]" />
      </div>
      <div className="flex gap-3">
        <Button type="submit" isLoading={submit.isPending} loadingText="Sending…">Send for review</Button>
        <Button type="button" variant="ghost" onClick={() => setOpen(false)}>Cancel</Button>
      </div>
    </form>
  );
}
