"use client";

import { shareMessages } from "@alumni/ui";
import { richTextToPlain } from "@alumni/ui";
import { RichTextEditor } from "@alumni/ui";
import { FormError } from "@alumni/ui";
import { useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Plus, Pencil, Trash2, FileText, X, ArrowUp, ArrowDown, Link2, Download,
} from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { FormSheet, FormSection } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { cn } from "@alumni/ui";
import {
  getServiceTypes, createServiceType, updateServiceType, deleteServiceType,
  getServiceRequests, updateServiceRequest,
  type ServiceFieldDefinitionBody, type ServiceTypeBody,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import type { ServiceType, ServiceRequest, ServiceFieldType } from "@/types";
import { MemberShareButton } from "@/components/institution/member-share-button";

const TABS = ["Service types", "Requests"] as const;
type Tab = (typeof TABS)[number];

const statusVariant: Record<string, "success" | "secondary" | "warning"> = {
  Active: "success",
  Draft: "warning",
  Archived: "secondary",
};

const paymentVariant: Record<string, "success" | "secondary" | "warning" | "destructive"> = {
  Successful: "success",
  NotRequired: "secondary",
  Pending: "warning",
  Failed: "destructive",
};

function paymentLabel(status: string) {
  return status === "NotRequired" ? "Free" : status;
}

const FIELD_TYPES: { value: ServiceFieldType; label: string }[] = [
  { value: "Text", label: "Text" },
  { value: "TextArea", label: "Long text" },
  { value: "Number", label: "Number" },
  { value: "Date", label: "Date" },
  { value: "Select", label: "Dropdown" },
  { value: "File", label: "File upload" },
];

interface FieldFormRow extends ServiceFieldDefinitionBody {
  optionsRaw: string;
}

interface FormState {
  name: string;
  description: string;
  price: string;
  status: ServiceType["status"];
  fields: FieldFormRow[];
  stages: string[];
}

const emptyForm: FormState = {
  name: "", description: "", price: "", status: "Active",
  fields: [], stages: ["Submitted"],
};

function toFormState(s: ServiceType): FormState {
  return {
    name: s.name,
    description: s.description ?? "",
    price: String(s.price),
    status: s.status,
    fields: s.fields.map((f) => ({ key: f.key, label: f.label, type: f.type, required: f.required, options: f.options, optionsRaw: (f.options ?? []).join(", ") })),
    stages: s.stages.length > 0 ? [...s.stages] : ["Submitted"],
  };
}

export default function ServicesPage() {
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<Tab>("Service types");

  // ── Service types ──
  const [typePage, setTypePage] = useState(1);
  const { data: typesResult, isLoading: typesLoading } = useQuery({
    queryKey: ["service-types", typePage],
    queryFn: () => getServiceTypes(typePage, 20),
  });

  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [deleteTarget, setDeleteTarget] = useState<ServiceType | null>(null);

  function openCreate() {
    setEditingId(null);
    setForm(emptyForm);
    setFormOpen(true);
  }
  function openEdit(s: ServiceType) {
    setEditingId(s.id);
    setForm(toFormState(s));
    setFormOpen(true);
  }

  const [formError, setFormError] = useState<string | null>(null);
  const saveMutation = useMutation({
    onMutate: () => setFormError(null),
    mutationFn: (body: ServiceTypeBody) => (editingId ? updateServiceType(editingId, body) : createServiceType(body)),
    onSuccess: () => {
      toast.success(editingId ? "Service updated" : "Service created");
      queryClient.invalidateQueries({ queryKey: ["service-types"] });
      setFormOpen(false);
    },
    onError: (e) => { const message = handleApiError(e); setFormError(message); toast.error(message); },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteServiceType(id),
    onSuccess: (res) => {
      toast.success(res.message ?? "Service removed");
      queryClient.invalidateQueries({ queryKey: ["service-types"] });
      setDeleteTarget(null);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function addField() {
    setForm((f) => ({ ...f, fields: [...f.fields, { label: "", type: "Text", required: false, optionsRaw: "" }] }));
  }
  function updateField(i: number, patch: Partial<FieldFormRow>) {
    setForm((f) => ({ ...f, fields: f.fields.map((fl, idx) => (idx === i ? { ...fl, ...patch } : fl)) }));
  }
  function removeField(i: number) {
    setForm((f) => ({ ...f, fields: f.fields.filter((_, idx) => idx !== i) }));
  }
  function moveField(i: number, dir: -1 | 1) {
    setForm((f) => {
      const fields = [...f.fields];
      const j = i + dir;
      if (j < 0 || j >= fields.length) return f;
      [fields[i], fields[j]] = [fields[j], fields[i]];
      return { ...f, fields };
    });
  }

  const [newStage, setNewStage] = useState("");
  function addStage() {
    const v = newStage.trim();
    if (!v || form.stages.some((s) => s.toLowerCase() === v.toLowerCase())) return;
    setForm((f) => ({ ...f, stages: [...f.stages, v] }));
    setNewStage("");
  }
  function removeStage(i: number) {
    setForm((f) => ({ ...f, stages: f.stages.filter((_, idx) => idx !== i) }));
  }
  function moveStage(i: number, dir: -1 | 1) {
    setForm((f) => {
      const stages = [...f.stages];
      const j = i + dir;
      if (j < 0 || j >= stages.length) return f;
      [stages[i], stages[j]] = [stages[j], stages[i]];
      return { ...f, stages };
    });
  }

  function submitForm() {
    if (!form.name.trim()) { toast.error("Name is required"); return; }
    const price = Number(form.price || 0);
    if (Number.isNaN(price) || price <= 0) { toast.error("Enter a price greater than zero — services can't be free"); return; }
    if (form.fields.some((fl) => !fl.label.trim())) { toast.error("Every field needs a label"); return; }
    if (form.stages.length === 0) { toast.error("Add at least one stage"); return; }

    const body: ServiceTypeBody = {
      name: form.name.trim(),
      description: form.description.trim() || undefined,
      price,
      status: form.status,
      stages: form.stages,
      fields: form.fields.map((fl) => ({
        key: fl.key,
        label: fl.label.trim(),
        type: fl.type,
        required: fl.required,
        helpText: fl.helpText?.trim() || undefined,
        options: fl.type === "Select" ? fl.optionsRaw.split(",").map((o) => o.trim()).filter(Boolean) : undefined,
      })),
    };
    saveMutation.mutate(body);
  }

  // ── Requests ──
  const [requestPage, setRequestPage] = useState(1);
  const { data: requestsResult, isLoading: requestsLoading } = useQuery({
    queryKey: ["service-requests", requestPage],
    queryFn: () => getServiceRequests(requestPage, 20),
    enabled: tab === "Requests",
  });

  const [selectedRequest, setSelectedRequest] = useState<ServiceRequest | null>(null);
  const [updateStage, setUpdateStage] = useState("");
  const [updateNote, setUpdateNote] = useState("");
  const [updateFile, setUpdateFile] = useState<File | null>(null);

  const serviceTypeForRequest = useMemo(
    () => typesResult?.results.find((s) => s.id === selectedRequest?.serviceTypeId),
    [typesResult, selectedRequest]
  );

  function openRequest(r: ServiceRequest) {
    setSelectedRequest(r);
    setUpdateStage(r.currentStage || "");
    setUpdateNote("");
    setUpdateFile(null);
  }

  const updateMutation = useMutation({
    mutationFn: () => updateServiceRequest(selectedRequest!.id, { stage: updateStage || undefined, note: updateNote || undefined, attachment: updateFile }),
    onSuccess: (updated) => {
      toast.success("Request updated");
      queryClient.invalidateQueries({ queryKey: ["service-requests"] });
      setSelectedRequest(updated);
      setUpdateNote("");
      setUpdateFile(null);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto">
      <header className="flex items-end justify-between gap-4 mb-6 flex-wrap">
        <div className="space-y-1">
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Member Services</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">
            Transcripts, attestations, certificate reissues, and more — define what members can request, price it, and track fulfillment.
          </p>
        </div>
        {tab === "Service types" && (
          <Button onClick={openCreate}><Plus size={15} className="mr-1.5" /> New service</Button>
        )}
      </header>

      <div className="flex gap-6 border-b border-border mb-6">
        {TABS.map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={cn(
              "pb-3 text-[13.5px] font-medium whitespace-nowrap border-b-2 -mb-px transition-colors",
              tab === t ? "border-accent text-foreground" : "border-transparent text-muted-foreground hover:text-foreground"
            )}
          >
            {t}
          </button>
        ))}
      </div>

      {tab === "Service types" && (
        <>
          {typesLoading ? (
            <CardSkeleton />
          ) : !typesResult || typesResult.results.length === 0 ? (
            <EmptyState icon={<FileText size={28} />} title="Let members request official services online" description="Create a service such as a transcript request or an attestation letter. Members fill in a form, pay online, and you move the request through its stages." action={<Button onClick={openCreate}><Plus size={15} className="mr-1.5" /> Create a service</Button>} />
          ) : (
            <Card className="border-border/40">
              <CardContent className="p-0">
                <Table stackOnMobile>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Name</TableHead>
                      <TableHead>Price</TableHead>
                      <TableHead>Fields</TableHead>
                      <TableHead>Stages</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {typesResult.results.map((s) => (
                      <TableRow key={s.id}>
                        <TableCell>
                          <div className="font-medium">{s.name}</div>
                          {s.description && <div className="text-[12px] text-muted-foreground line-clamp-1">{richTextToPlain(s.description)}</div>}
                        </TableCell>
                        <TableCell className="tabular-nums">{s.price > 0 ? formatCurrency(s.price) : "Free"}</TableCell>
                        <TableCell>{s.fields.length}</TableCell>
                        <TableCell className="text-[12.5px] text-muted-foreground">{s.stages.join(" → ")}</TableCell>
                        <TableCell><Badge variant={statusVariant[s.status]}>{s.status}</Badge></TableCell>
                        <TableCell className="text-right">
                          {s.status === "Active" && <MemberShareButton memberPath={`/services/${s.id}`} title={s.name} message={shareMessages.service({ name: s.name, description: s.description })} variant="ghost" />}
                          <Button size="icon" variant="ghost" onClick={() => openEdit(s)}><Pencil size={14} /></Button>
                          <Button size="icon" variant="ghost" onClick={() => setDeleteTarget(s)}><Trash2 size={14} className="text-destructive" /></Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
          {typesResult && typesResult.totalPages > 1 && (
            <div className="mt-4"><Pagination page={typePage} totalPages={typesResult.totalPages} onPageChange={setTypePage} /></div>
          )}
        </>
      )}

      {tab === "Requests" && (
        <>
          {requestsLoading ? (
            <CardSkeleton />
          ) : !requestsResult || requestsResult.results.length === 0 ? (
            <EmptyState icon={<FileText size={28} />} title="Requests appear here" description="After members submit a service, their requests show up here and you update their progress from this page." />
          ) : (
            <Card className="border-border/40">
              <CardContent className="p-0">
                <Table stackOnMobile>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Request #</TableHead>
                      <TableHead>Member</TableHead>
                      <TableHead>Service</TableHead>
                      <TableHead>Amount</TableHead>
                      <TableHead>Payment</TableHead>
                      <TableHead>Stage</TableHead>
                      <TableHead>Requested</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {requestsResult.results.map((r) => (
                      <TableRow key={r.id} className="cursor-pointer hover:bg-accent/5" onClick={() => openRequest(r)}>
                        <TableCell className="font-mono text-[12.5px]">{r.requestNumber}</TableCell>
                        <TableCell>{r.memberName ?? r.memberId}</TableCell>
                        <TableCell>{r.serviceTypeName}</TableCell>
                        <TableCell className="tabular-nums">{r.amount > 0 ? formatCurrency(r.amount) : "Free"}</TableCell>
                        <TableCell><Badge variant={paymentVariant[r.paymentStatus]}>{paymentLabel(r.paymentStatus)}</Badge></TableCell>
                        <TableCell><span className="text-[13px]">{r.currentStage || "—"}</span></TableCell>
                        <TableCell className="text-[12.5px] text-muted-foreground">{formatDate(r.createdAt)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
          {requestsResult && requestsResult.totalPages > 1 && (
            <div className="mt-4"><Pagination page={requestPage} totalPages={requestsResult.totalPages} onPageChange={setRequestPage} /></div>
          )}
        </>
      )}

      {/* ── Service type form dialog ── */}
      <FormSheet
        open={formOpen}
        onClose={() => setFormOpen(false)}
        title={editingId ? "Edit service" : "New service"}
        description="Members request it from their portal. You see each request in your queue and move it through your stages."
        formId="service-form"
        submitLabel={editingId ? "Save changes" : "Create service"}
        savingLabel="Saving"
        saving={saveMutation.isPending}
        error={formError || null}
        size="lg"
      >
        <form id="service-form" className="space-y-5" onSubmit={(e) => { e.preventDefault(); submitForm(); }}>
          <FormSection title="The service">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="e.g. Academic transcript request" />
            </div>
            <div className="space-y-2">
              <Label>Description <span className="font-normal text-muted-foreground">(optional)</span></Label>
              <RichTextEditor variant="compact" value={form.description} onChange={(v) => setForm((f) => ({ ...f, description: v }))} minHeight={110} placeholder="What the service is and how long it usually takes." />
            </div>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>Price (GHS)</Label>
                <Input type="number" min={0.01} step="0.01" placeholder="e.g. 25.00" value={form.price} onChange={(e) => setForm((f) => ({ ...f, price: e.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label>Status</Label>
                <FormSelect
                  className="w-full"
                  value={form.status}
                  onValueChange={(v) => setForm((f) => ({ ...f, status: v as ServiceType["status"] }))}
                  options={[{ value: "Active", label: "Active, members can request it" }, { value: "Draft", label: "Draft, hidden" }, { value: "Archived", label: "Archived" }]}
                />
              </div>
            </div>
          </FormSection>

          <FormSection title="What you ask the member" hint="The details you need to fulfil a request. Members fill these in when they request it, in this order.">
            {form.fields.length === 0 && (
              <p className="border border-dashed border-border p-3 text-[13px] text-muted-foreground">No questions yet. Add the details you need, such as an index number or a graduation year.</p>
            )}
            {form.fields.map((fl, i) => (
              <div key={i} className="space-y-3 border border-border bg-card p-3.5">
                <div className="flex items-start gap-2">
                  <div className="min-w-0 flex-1 space-y-1.5">
                    <Label className="text-[12.5px]">Question {i + 1}</Label>
                    <Input placeholder="e.g. Index number" value={fl.label} onChange={(e) => updateField(i, { label: e.target.value })} />
                  </div>
                  <div className="flex shrink-0 gap-0.5 pt-6">
                    <button type="button" onClick={() => moveField(i, -1)} disabled={i === 0} className="flex size-9 items-center justify-center text-muted-foreground hover:bg-muted hover:text-foreground disabled:cursor-not-allowed disabled:opacity-30" aria-label="Move question up"><ArrowUp size={14} /></button>
                    <button type="button" onClick={() => moveField(i, 1)} disabled={i === form.fields.length - 1} className="flex size-9 items-center justify-center text-muted-foreground hover:bg-muted hover:text-foreground disabled:cursor-not-allowed disabled:opacity-30" aria-label="Move question down"><ArrowDown size={14} /></button>
                    <button type="button" onClick={() => removeField(i)} className="flex size-9 items-center justify-center text-destructive hover:bg-destructive/10" aria-label={`Remove ${fl.label || "question"}`}><X size={15} /></button>
                  </div>
                </div>
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 sm:items-end">
                  <div className="space-y-1.5">
                    <Label className="text-[12.5px]">Answer type</Label>
                    <FormSelect className="w-full" value={fl.type} onValueChange={(v) => updateField(i, { type: v as ServiceFieldType })} options={FIELD_TYPES} />
                  </div>
                  <label className="flex h-10 cursor-pointer items-center gap-2.5 text-[13.5px]">
                    <input type="checkbox" checked={fl.required} onChange={(e) => updateField(i, { required: e.target.checked })} className="size-4 accent-[var(--primary)]" />
                    Required
                  </label>
                </div>
                {fl.type === "Select" && (
                  <div className="space-y-1.5">
                    <Label className="text-[12.5px]">Choices</Label>
                    <Input placeholder="Separate with commas, e.g. Local, International" value={fl.optionsRaw} onChange={(e) => updateField(i, { optionsRaw: e.target.value })} />
                  </div>
                )}
              </div>
            ))}
            <Button type="button" variant="outline" onClick={addField}><Plus size={14} className="mr-1.5" /> Add a question</Button>
          </FormSection>

          <FormSection title="Fulfilment stages" hint="Your own pipeline. Every new request starts at the first stage, and members follow it as it moves.">
            <div className="flex gap-2">
              <Input
                value={newStage}
                onChange={(e) => setNewStage(e.target.value)}
                onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); addStage(); } }}
                placeholder="e.g. Under review"
              />
              <Button type="button" variant="outline" onClick={addStage} disabled={!newStage.trim()}><Plus size={14} className="mr-1.5" /> Add</Button>
            </div>
            <ol className="space-y-1.5">
              {form.stages.map((s, i) => (
                <li key={i} className="flex items-center gap-2 border border-border bg-card px-3 py-2 text-[13.5px]">
                  <span className="w-5 tabular-nums text-muted-foreground">{i + 1}</span>
                  <span className="flex-1">{s}</span>
                  <button type="button" onClick={() => moveStage(i, -1)} disabled={i === 0} className="flex size-8 items-center justify-center text-muted-foreground hover:bg-muted disabled:opacity-30" aria-label="Move stage up"><ArrowUp size={13} /></button>
                  <button type="button" onClick={() => moveStage(i, 1)} disabled={i === form.stages.length - 1} className="flex size-8 items-center justify-center text-muted-foreground hover:bg-muted disabled:opacity-30" aria-label="Move stage down"><ArrowDown size={13} /></button>
                  <button type="button" onClick={() => removeStage(i)} className="flex size-8 items-center justify-center text-destructive hover:bg-destructive/10" aria-label={`Remove stage ${s}`}><X size={14} /></button>
                </li>
              ))}
            </ol>
          </FormSection>
        </form>
      </FormSheet>

      <ConfirmModal
        open={!!deleteTarget}
        title="Remove service?"
        message={`"${deleteTarget?.name}" will be deleted. If it already has requests, it will be archived instead so past requests keep their history.`}
        confirmLabel="Remove"
        variant="destructive"
        isLoading={deleteMutation.isPending}
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />

      {/* ── Request detail dialog ── */}
      <Dialog open={!!selectedRequest} onOpenChange={(open) => !open && setSelectedRequest(null)}>
        <DialogContent className="max-w-[600px] max-h-[85vh] overflow-y-auto">
          {selectedRequest && (
            <>
              <DialogHeader>
                <DialogTitle>{selectedRequest.serviceTypeName} — #{selectedRequest.requestNumber}</DialogTitle>
              </DialogHeader>

              <div className="space-y-4 py-2">
                <div className="flex items-center gap-2 flex-wrap">
                  <Badge variant={paymentVariant[selectedRequest.paymentStatus]}>{paymentLabel(selectedRequest.paymentStatus)}</Badge>
                  <Badge variant="secondary">{selectedRequest.currentStage || "Awaiting payment"}</Badge>
                  <span className="text-[12.5px] text-muted-foreground ml-auto">
                    {selectedRequest.memberName ?? selectedRequest.memberId} · {selectedRequest.memberEmail}
                  </span>
                </div>

                {Object.keys(selectedRequest.fieldAnswers).length > 0 && (
                  <div className="space-y-1.5">
                    <Label>Submitted answers</Label>
                    <div className="border border-border/60 divide-y divide-border/60">
                      {Object.entries(selectedRequest.fieldAnswers).map(([key, value]) => {
                        const field = serviceTypeForRequest?.fields.find((f) => f.key === key);
                        return (
                          <div key={key} className="px-3 py-2 text-[13px]">
                            <div className="text-[12px] uppercase tracking-wide text-muted-foreground">{field?.label ?? key}</div>
                            <div>{value || "—"}</div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}

                {Object.keys(selectedRequest.attachments).filter((k) => !k.startsWith("admin_")).length > 0 && (
                  <div className="space-y-1.5">
                    <Label>Member attachments</Label>
                    {Object.entries(selectedRequest.attachments).filter(([k]) => !k.startsWith("admin_")).map(([key, url]) => (
                      <a key={key} href={url} target="_blank" rel="noreferrer" className="flex items-center gap-2 text-[13px] text-accent hover:underline">
                        <Link2 size={13} /> {serviceTypeForRequest?.fields.find((f) => f.key === key)?.label ?? key}
                      </a>
                    ))}
                  </div>
                )}

                <div className="space-y-1.5">
                  <Label>Timeline</Label>
                  <ol className="space-y-2">
                    {[...selectedRequest.updates].reverse().map((u, i) => (
                      <li key={i} className="border-l-2 border-border/60 pl-3 py-0.5 text-[13px]">
                        <div className="flex items-center gap-2 flex-wrap">
                          {u.stage && <span className="font-medium">{u.stage}</span>}
                          <span className="text-[12px] text-muted-foreground">{formatDate(u.changedAt)}{u.changedByStaffName ? ` · ${u.changedByStaffName}` : ""}</span>
                        </div>
                        {u.note && <p className="text-muted-foreground mt-0.5">{u.note}</p>}
                        {u.attachmentUrl && (
                          <a href={u.attachmentUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-accent hover:underline mt-0.5">
                            <Download size={12} /> Attached file
                          </a>
                        )}
                      </li>
                    ))}
                  </ol>
                </div>

                <div className="space-y-2.5 pt-2 border-t border-border/60">
                  <Label>Post an update</Label>
                  <p className="text-[12px] text-muted-foreground -mt-1">
                    Can&apos;t fulfill this request? Add a stage like &quot;Unable to Process&quot; under this service&apos;s Fulfillment stages, then use it here with a note explaining why — the member has already paid, so there&apos;s no reject action.
                  </p>
                  <FormSelect
                    value={updateStage}
                    onValueChange={setUpdateStage}
                    placeholder="Keep current stage"
                    options={(serviceTypeForRequest?.stages ?? []).map((s) => ({ value: s, label: s }))}
                  />
                  <Textarea rows={2} placeholder="Note for the member (optional)" value={updateNote} onChange={(e) => setUpdateNote(e.target.value)} />
                  <input type="file" onChange={(e) => setUpdateFile(e.target.files?.[0] ?? null)} className="text-[13px]" />

                  <Button
                    isLoading={updateMutation.isPending}
                    disabled={!updateNote.trim() && !updateFile && updateStage === selectedRequest.currentStage}
                    onClick={() => updateMutation.mutate()}
                  >
                    Post update
                  </Button>
                </div>
              </div>
            </>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
