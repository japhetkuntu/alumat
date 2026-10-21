"use client";

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent, Button, Input, Label, Textarea, LinkOrUpload, FormError, Plus, Trash2, ArrowUp, ArrowDown, Users } from "@alumni/ui";
import { updatePublicProfile, uploadImage, type PublicLeader, type PublicProfile } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";

const MAX_LEADERS = 24;

interface LeaderRow extends PublicLeader { file: File | null }

/**
 * The contact details and leaders shown on the public landing page. Everything here is typed in by an administrator:
 * nobody appears because of a role they hold in the system, and contact details of a person are shown only if added.
 */
export function PublicDetailsPanel({ initial }: { initial: PublicProfile | null | undefined }) {
  const queryClient = useQueryClient();
  const [contactEmail, setContactEmail] = useState(initial?.contactEmail ?? "");
  const [contactPhone, setContactPhone] = useState(initial?.contactPhone ?? "");
  const [address, setAddress] = useState(initial?.address ?? "");
  const [leaders, setLeaders] = useState<LeaderRow[]>((initial?.leaders ?? []).map((l) => ({ ...l, file: null })));
  const [error, setError] = useState<string | null>(null);

  const patch = (i: number, p: Partial<LeaderRow>) => setLeaders((rows) => rows.map((r, idx) => (idx === i ? { ...r, ...p } : r)));
  const move = (i: number, dir: -1 | 1) => setLeaders((rows) => {
    const j = i + dir;
    if (j < 0 || j >= rows.length) return rows;
    const next = [...rows];
    [next[i], next[j]] = [next[j], next[i]];
    return next;
  });

  const save = useMutation({
    mutationFn: async () => {
      if (leaders.some((l) => !l.name.trim())) throw new Error("Every leader needs a name.");
      const resolved = await Promise.all(leaders.map(async ({ file, ...l }) => {
        if (!file) return l;
        const { url } = await uploadImage(file);
        return { ...l, photoUrl: url };
      }));
      return updatePublicProfile({ contactEmail, contactPhone, address, leaders: resolved });
    },
    onSuccess: () => {
      toast.success("Public details updated");
      setError(null);
      setLeaders((rows) => rows.map((r) => ({ ...r, file: null })));
      queryClient.invalidateQueries({ queryKey: ["institution-profile"] });
    },
    onError: (e) => { const msg = e instanceof Error && !("response" in e) ? e.message : handleApiError(e); setError(msg); toast.error(msg); },
  });

  return (
    <Card className="border-border/40">
      <CardContent className="space-y-5 p-6">
        <div>
          <div className="mb-1 flex items-center gap-2">
            <Users size={16} className="text-primary" />
            <p className="text-[15px] font-semibold">Contact and leadership</p>
          </div>
          <p className="text-[12.5px] text-muted-foreground">
            Shown to anyone who visits your public page, so people can see who runs the community and how to reach you. Only what you type here appears. Add a person only with their agreement.
          </p>
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div className="space-y-1.5"><Label>Contact email</Label><Input type="email" value={contactEmail} onChange={(e) => setContactEmail(e.target.value)} placeholder="secretariat@example.org" maxLength={200} /></div>
          <div className="space-y-1.5"><Label>Contact phone</Label><Input value={contactPhone} onChange={(e) => setContactPhone(e.target.value)} placeholder="+233 …" maxLength={40} /></div>
        </div>
        <div className="space-y-1.5"><Label>Address</Label><Textarea rows={2} value={address} onChange={(e) => setAddress(e.target.value)} placeholder="Where to find the secretariat or office" maxLength={300} /></div>

        <div className="space-y-3">
          <p className="text-[13.5px] font-semibold">Leaders</p>
          {leaders.length === 0 && <p className="border border-dashed border-border p-3 text-[13px] text-muted-foreground">No leaders shown. Add the executives or officers you want visitors to see.</p>}
          {leaders.map((l, i) => (
            <div key={i} className="space-y-3 border border-border bg-card p-3.5">
              <div className="flex items-start gap-2">
                <div className="grid min-w-0 flex-1 grid-cols-1 gap-3 sm:grid-cols-2">
                  <div className="space-y-1.5"><Label className="text-[12.5px]">Name</Label><Input value={l.name} onChange={(e) => patch(i, { name: e.target.value })} placeholder="e.g. Ama Mensah" maxLength={120} /></div>
                  <div className="space-y-1.5"><Label className="text-[12.5px]">Role</Label><Input value={l.role} onChange={(e) => patch(i, { role: e.target.value })} placeholder="e.g. President" maxLength={120} /></div>
                </div>
                <div className="flex shrink-0 gap-0.5 pt-6">
                  <button type="button" onClick={() => move(i, -1)} disabled={i === 0} className="flex size-9 items-center justify-center text-muted-foreground hover:bg-muted disabled:opacity-30" aria-label="Move up"><ArrowUp size={14} /></button>
                  <button type="button" onClick={() => move(i, 1)} disabled={i === leaders.length - 1} className="flex size-9 items-center justify-center text-muted-foreground hover:bg-muted disabled:opacity-30" aria-label="Move down"><ArrowDown size={14} /></button>
                  <button type="button" onClick={() => setLeaders((rows) => rows.filter((_, idx) => idx !== i))} className="flex size-9 items-center justify-center text-destructive hover:bg-destructive/10" aria-label={`Remove ${l.name || "leader"}`}><Trash2 size={14} /></button>
                </div>
              </div>
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <div className="space-y-1.5"><Label className="text-[12.5px]">Email <span className="font-normal text-muted-foreground">(optional, shown publicly)</span></Label><Input type="email" value={l.email ?? ""} onChange={(e) => patch(i, { email: e.target.value })} maxLength={200} /></div>
                <div className="space-y-1.5"><Label className="text-[12.5px]">Phone <span className="font-normal text-muted-foreground">(optional, shown publicly)</span></Label><Input value={l.phone ?? ""} onChange={(e) => patch(i, { phone: e.target.value })} maxLength={40} /></div>
              </div>
              <LinkOrUpload label="Photo (optional)" url={l.photoUrl ?? ""} onUrlChange={(v) => patch(i, { photoUrl: v })} file={l.file} onFileChange={(f) => patch(i, { file: f })} />
            </div>
          ))}
          <Button type="button" variant="outline" disabled={leaders.length >= MAX_LEADERS} onClick={() => setLeaders((rows) => [...rows, { name: "", role: "", photoUrl: "", email: "", phone: "", file: null }])}>
            <Plus size={14} className="mr-1.5" /> Add a leader
          </Button>
        </div>

        <FormError message={error} />
        <Button onClick={() => save.mutate()} disabled={save.isPending}>{save.isPending ? "Saving…" : "Save contact and leaders"}</Button>
      </CardContent>
    </Card>
  );
}
