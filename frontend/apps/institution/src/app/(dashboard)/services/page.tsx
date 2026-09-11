"use client";

import { useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Plus, Pencil, Trash2, FileText, X, ArrowUp, ArrowDown, Link2, Download,
} from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { Button } from "@alumni/ui";
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

  const saveMutation = useMutation({
    mutationFn: (body: ServiceTypeBody) => (editingId ? updateServiceType(editingId, body) : createServiceType(body)),
    onSuccess: () => {
      toast.success(editingId ? "Service updated" : "Service created");
      queryClient.invalidateQueries({ queryKey: ["service-types"] });
      setFormOpen(false);
    },
    onError: (e) => toast.error(handleApiError(e)),
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
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Alumni Services</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">
            Transcripts, attestation letters, certificate reissue — define what alumni can request, price it, and track fulfillment.
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
            <EmptyState icon={<FileText size={28} />} title="No services yet" description="Create your first service — a transcript request, an attestation letter, anything alumni can request and pay for." />
          ) : (
            <Card className="border-border/40">
              <CardContent className="p-0">
                <Table>
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
                          {s.description && <div className="text-[12px] text-muted-foreground line-clamp-1">{s.description}</div>}
                        </TableCell>
                        <TableCell className="tabular-nums">{s.price > 0 ? formatCurrency(s.price) : "Free"}</TableCell>
                        <TableCell>{s.fields.length}</TableCell>
                        <TableCell className="text-[12.5px] text-muted-foreground">{s.stages.join(" → ")}</TableCell>
                        <TableCell><Badge variant={statusVariant[s.status]}>{s.status}</Badge></TableCell>
                        <TableCell className="text-right">
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
            <EmptyState icon={<FileText size={28} />} title="No requests yet" description="Member requests for your services will show up here." />
          ) : (
            <Card className="border-border/40">
              <CardContent className="p-0">
                <Table>
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
      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="max-w-[640px] max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editingId ? "Edit service" : "New service"}</DialogTitle>
          </DialogHeader>

          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="e.g. Academic Transcript Request" />
            </div>
            <div className="space-y-1.5">
              <Label>Description <span className="font-normal text-muted-foreground">(optional)</span></Label>
              <Textarea value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} rows={2} />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Price</Label>
                <Input type="number" min={0.01} step="0.01" placeholder="e.g. 25.00" value={form.price} onChange={(e) => setForm((f) => ({ ...f, price: e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>Status</Label>
                <FormSelect
                  value={form.status}
                  onValueChange={(v) => setForm((f) => ({ ...f, status: v as ServiceType["status"] }))}
                  options={[{ value: "Active", label: "Active" }, { value: "Draft", label: "Draft" }, { value: "Archived", label: "Archived" }]}
                />
              </div>
            </div>

            {/* Fields builder */}
            <div className="space-y-2.5 pt-2 border-t border-border/60">
              <Label>Request form fields</Label>
              <p className="text-[12px] text-muted-foreground -mt-1">What you need from the alumni to fulfill this request. Use the arrows to set the order they appear in.</p>
              {form.fields.map((fl, i) => (
                <div key={i} className="border border-border/60 p-3 space-y-2.5">
                  <div className="flex gap-2 items-start">
                    <div className="flex flex-col shrink-0 -my-1">
                      <button type="button" onClick={() => moveField(i, -1)} disabled={i === 0} className="p-1 disabled:opacity-25 disabled:cursor-not-allowed text-muted-foreground hover:text-foreground" aria-label="Move field up">
                        <ArrowUp size={13} />
                      </button>
                      <button type="button" onClick={() => moveField(i, 1)} disabled={i === form.fields.length - 1} className="p-1 disabled:opacity-25 disabled:cursor-not-allowed text-muted-foreground hover:text-foreground" aria-label="Move field down">
                        <ArrowDown size={13} />
                      </button>
                    </div>
                    <Input className="flex-1" placeholder="Field label, e.g. Index Number" value={fl.label} onChange={(e) => updateField(i, { label: e.target.value })} />
                    <FormSelect
                      className="w-[140px] shrink-0"
                      value={fl.type}
                      onValueChange={(v) => updateField(i, { type: v as ServiceFieldType })}
                      options={FIELD_TYPES}
                    />
                    <button type="button" onClick={() => removeField(i)} className="text-destructive hover:opacity-70 shrink-0 p-2" aria-label={`Remove ${fl.label || "field"}`}>
                      <X size={16} />
                    </button>
                  </div>
                  {fl.type === "Select" && (
                    <Input className="ml-[26px] w-[calc(100%-26px)]" placeholder="Options, comma separated" value={fl.optionsRaw} onChange={(e) => updateField(i, { optionsRaw: e.target.value })} />
                  )}
                  <div className="flex items-center gap-2.5 ml-[26px]">
                    <button
                      type="button"
                      role="switch"
                      aria-checked={fl.required}
                      onClick={() => updateField(i, { required: !fl.required })}
                      className={cn(
                        "relative inline-flex h-[18px] w-8 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-150",
                        fl.required ? "bg-accent" : "bg-muted"
                      )}
                    >
                      <span
                        className={cn(
                          "pointer-events-none inline-block h-3.5 w-3.5 rounded-full bg-white shadow transition-transform duration-150",
                          fl.required ? "translate-x-[14px]" : "translate-x-0"
                        )}
                      />
                    </button>
                    <span className="text-[13px] text-foreground select-none">
                      {fl.required ? "Required" : "Optional"}
                    </span>
                  </div>
                </div>
              ))}
              <Button type="button" variant="outline" size="sm" onClick={addField}><Plus size={13} className="mr-1.5" /> Add field</Button>
            </div>

            {/* Stages builder */}
            <div className="space-y-2.5 pt-2 border-t border-border/60">
              <Label>Fulfillment stages</Label>
              <p className="text-[12px] text-muted-foreground -mt-1">Your own pipeline — every new request starts at the first stage.</p>
              <div className="flex gap-2">
                <Input
                  value={newStage}
                  onChange={(e) => setNewStage(e.target.value)}
                  onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); addStage(); } }}
                  placeholder="e.g. Under Review"
                />
                <Button type="button" variant="outline" onClick={addStage} disabled={!newStage.trim()}><Plus size={14} className="mr-1.5" /> Add</Button>
              </div>
              <ol className="space-y-1.5">
                {form.stages.map((s, i) => (
                  <li key={i} className="flex items-center gap-2 border border-border/60 px-3 py-2 text-[13px]">
                    <span className="text-muted-foreground tabular-nums w-5">{i + 1}.</span>
                    <span className="flex-1">{s}</span>
                    <button type="button" onClick={() => moveStage(i, -1)} disabled={i === 0} className="disabled:opacity-30 p-1"><ArrowUp size={13} /></button>
                    <button type="button" onClick={() => moveStage(i, 1)} disabled={i === form.stages.length - 1} className="disabled:opacity-30 p-1"><ArrowDown size={13} /></button>
                    <button type="button" onClick={() => removeStage(i)} className="text-destructive hover:opacity-70 p-1" aria-label={`Remove stage ${s}`}><X size={14} /></button>
                  </li>
                ))}
              </ol>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setFormOpen(false)}>Cancel</Button>
            <Button onClick={submitForm} isLoading={saveMutation.isPending}>{editingId ? "Save changes" : "Create service"}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

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
                            <div className="text-[11px] uppercase tracking-wide text-muted-foreground">{field?.label ?? key}</div>
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
                          <span className="text-[11.5px] text-muted-foreground">{formatDate(u.changedAt)}{u.changedByStaffName ? ` · ${u.changedByStaffName}` : ""}</span>
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
                    Can&apos;t fulfill this request? Add a stage like &quot;Unable to Process&quot; under this service&apos;s Fulfillment stages, then use it here with a note explaining why — the alumnus has already paid, so there&apos;s no reject action.
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
