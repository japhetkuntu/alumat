"use client";

import { shareMessages } from "@alumni/ui";
import { richTextToPlain } from "@alumni/ui";
import { FormError } from "@alumni/ui";
import { useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Plus, Pencil, Trash2, FileText, Link2, Download,
} from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { Button } from "@alumni/ui";
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
  type ServiceTypeBody,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import type { ServiceType, ServiceRequest } from "@/types";
import { ServiceFormSheet } from "./service-form-sheet";
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
  const [formKey, setFormKey] = useState(0);
  const [editing, setEditing] = useState<ServiceType | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ServiceType | null>(null);

  const [formError, setFormError] = useState<string | null>(null);
  function openCreate() {
    setEditingId(null);
    setEditing(null);
    setFormError(null);
    setFormKey((k) => k + 1);
    setFormOpen(true);
  }
  function openEdit(s: ServiceType) {
    setEditingId(s.id);
    setEditing(s);
    setFormError(null);
    setFormKey((k) => k + 1);
    setFormOpen(true);
  }

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
      <ServiceFormSheet
        key={formKey}
        open={formOpen}
        editing={editing}
        saving={saveMutation.isPending}
        error={formError}
        onClose={() => setFormOpen(false)}
        onSubmit={(body) => saveMutation.mutate(body)}
      />

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
