"use client";

import { useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Plus, Store, Trash2, Pencil, CheckCircle, XCircle, Ban, ShieldCheck, Loader2,
  Phone, Mail, Globe, Link2, MapPin, AlertCircle,
} from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { ImageUpload } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { formatDate, cn } from "@alumni/ui";
import {
  getBusinessListings, createBusinessListing, updateBusinessListing, deleteBusinessListing,
  approveBusinessListing, rejectBusinessListing, approveBusinessListingEdit, rejectBusinessListingEdit,
  blacklistBusinessListing, unblacklistBusinessListing,
  type BusinessListing, type BusinessListingBody, type BusinessListingPendingChanges,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";

const statusVariant: Record<string, "success" | "warning" | "destructive" | "neutral"> = {
  Approved: "success",
  Pending: "warning",
  Rejected: "destructive",
  Blacklisted: "neutral",
};

interface FormState {
  businessName: string;
  description: string;
  location: string;
  phoneNumber: string;
  email: string;
  websiteUrl: string;
  externalLinkUrl: string;
  logo: File | null;
  existingLogoUrl: string;
  banner: File | null;
  existingBannerUrl: string;
}

const emptyForm: FormState = {
  businessName: "", description: "", location: "", phoneNumber: "", email: "", websiteUrl: "", externalLinkUrl: "",
  logo: null, existingLogoUrl: "", banner: null, existingBannerUrl: "",
};

function hasContactInfo(f: FormState): boolean {
  return !!(f.phoneNumber.trim() || f.email.trim() || f.websiteUrl.trim());
}

function toBody(f: FormState): BusinessListingBody {
  return {
    businessName: f.businessName,
    description: f.description,
    location: f.location,
    phoneNumber: f.phoneNumber.trim() || undefined,
    email: f.email.trim() || undefined,
    websiteUrl: f.websiteUrl.trim() || undefined,
    externalLinkUrl: f.externalLinkUrl.trim() || undefined,
    logo: f.logo ?? undefined,
    banner: f.banner ?? undefined,
  };
}

const PENDING_FIELD_LABELS: Record<keyof BusinessListingPendingChanges, string> = {
  businessName: "Business name",
  description: "Description",
  logoUrl: "Logo",
  bannerUrl: "Banner",
  location: "Location",
  phoneNumber: "Phone",
  email: "Email",
  websiteUrl: "Website",
  externalLinkUrl: "External link",
};

function PendingEditDiff({ listing }: { listing: BusinessListing }) {
  const pc = listing.pendingChanges;
  if (!pc) return null;
  const currentByKey: Record<string, string | null | undefined> = {
    businessName: listing.businessName,
    description: listing.description,
    logoUrl: listing.logoUrl,
    bannerUrl: listing.bannerUrl,
    location: listing.location,
    phoneNumber: listing.phoneNumber,
    email: listing.email,
    websiteUrl: listing.websiteUrl,
    externalLinkUrl: listing.externalLinkUrl,
  };
  const changedKeys = (Object.keys(pc) as (keyof BusinessListingPendingChanges)[]).filter((k) => pc[k] != null);
  if (changedKeys.length === 0) return null;
  return (
    <div className="space-y-2 rounded-md border border-warning/30 bg-warning/5 p-3">
      <p className="text-[11.5px] font-bold text-warning uppercase tracking-wide flex items-center gap-1.5">
        <AlertCircle size={12} />Pending edit — proposed changes
      </p>
      <div className="space-y-1.5">
        {changedKeys.map((k) => (
          <div key={k} className="text-[12px] grid grid-cols-[100px_1fr] gap-2">
            <span className="font-semibold text-muted-foreground">{PENDING_FIELD_LABELS[k]}</span>
            <div className="min-w-0">
              <span className="text-muted-foreground line-through decoration-destructive/50 break-words">{currentByKey[k] || "—"}</span>
              {" -> "}
              <span className="font-medium text-foreground break-words">{pc[k]}</span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function ListingForm({ title, init, onSave, onCancel, saving }: {
  title: string; init: FormState; onSave: (f: FormState) => void; onCancel: () => void; saving: boolean;
}) {
  const [form, setForm] = useState(init);
  const f = <K extends keyof FormState>(k: K, v: FormState[K]) => setForm((prev) => ({ ...prev, [k]: v }));
  const valid = form.businessName.trim() && form.description.trim() && form.location.trim() && hasContactInfo(form);

  return (
    <DialogContent size="lg">
      <DialogHeader>
        <DialogTitle>{title}</DialogTitle>
      </DialogHeader>
      <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); if (valid) onSave(form); }}>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div className="space-y-2 sm:col-span-2">
            <Label>Business name</Label>
            <Input value={form.businessName} onChange={(e) => f("businessName", e.target.value)} placeholder="e.g. Kofi's Bakery" required />
          </div>
          <div className="space-y-2 sm:col-span-2">
            <Label>Description</Label>
            <Textarea rows={3} value={form.description} onChange={(e) => f("description", e.target.value)} placeholder="What does this business do?" required />
          </div>
          <div className="space-y-2 sm:col-span-2">
            <Label>Location</Label>
            <Input value={form.location} onChange={(e) => f("location", e.target.value)} placeholder="e.g. Accra, Ghana" required />
          </div>
          <div className="space-y-2">
            <Label>Phone</Label>
            <Input value={form.phoneNumber} onChange={(e) => f("phoneNumber", e.target.value)} placeholder="+233..." />
          </div>
          <div className="space-y-2">
            <Label>Email</Label>
            <Input type="email" value={form.email} onChange={(e) => f("email", e.target.value)} placeholder="business@example.com" />
          </div>
          <div className="space-y-2">
            <Label>Website</Label>
            <Input value={form.websiteUrl} onChange={(e) => f("websiteUrl", e.target.value)} placeholder="https://..." />
          </div>
          <div className="space-y-2">
            <Label>External link (optional)</Label>
            <Input value={form.externalLinkUrl} onChange={(e) => f("externalLinkUrl", e.target.value)} placeholder="https://..." />
          </div>
        </div>
        {!hasContactInfo(form) && (
          <p className="text-[12px] text-destructive">Provide at least one of phone, email, or website.</p>
        )}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label>Logo</Label>
            <ImageUpload
              file={form.logo}
              existingUrl={form.existingLogoUrl || undefined}
              onChange={(file) => f("logo", file)}
              onClearExisting={() => f("existingLogoUrl", "")}
              label="Upload logo"
            />
          </div>
          <div className="space-y-2">
            <Label>Banner</Label>
            <ImageUpload
              file={form.banner}
              existingUrl={form.existingBannerUrl || undefined}
              onChange={(file) => f("banner", file)}
              onClearExisting={() => f("existingBannerUrl", "")}
              label="Upload banner"
            />
          </div>
        </div>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onCancel}>Cancel</Button>
          <Button type="submit" disabled={!valid || saving} isLoading={saving} loadingText="Saving">Save</Button>
        </DialogFooter>
      </form>
    </DialogContent>
  );
}

export default function AdminBusinessDirectoryPage() {
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [showCreate, setShowCreate] = useState(false);
  const [editTarget, setEditTarget] = useState<BusinessListing | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<BusinessListing | null>(null);
  const [rejectTarget, setRejectTarget] = useState<BusinessListing | null>(null);
  const [rejectEditTarget, setRejectEditTarget] = useState<BusinessListing | null>(null);
  const [rejectNotes, setRejectNotes] = useState("");
  const [approveEditTarget, setApproveEditTarget] = useState<BusinessListing | null>(null);
  const pageSize = 20;
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["admin-business-directory", statusFilter, page],
    queryFn: () => getBusinessListings(statusFilter || undefined, page, pageSize),
    placeholderData: (prev) => prev,
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: ["admin-business-directory"] });

  const createMut = useMutation({
    mutationFn: (f: FormState) => createBusinessListing(toBody(f)),
    onSuccess: () => { invalidate(); setShowCreate(false); toast.success("Business listing added"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, f }: { id: string; f: FormState }) => updateBusinessListing(id, toBody(f)),
    onSuccess: () => { invalidate(); setEditTarget(null); toast.success("Listing updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => deleteBusinessListing(id),
    onSuccess: () => { invalidate(); setDeleteTarget(null); toast.success("Listing deleted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const approveMut = useMutation({
    mutationFn: (id: string) => approveBusinessListing(id),
    onSuccess: () => { invalidate(); toast.success("Listing approved"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const rejectMut = useMutation({
    mutationFn: ({ id, notes }: { id: string; notes?: string }) => rejectBusinessListing(id, notes),
    onSuccess: () => { invalidate(); setRejectTarget(null); setRejectNotes(""); toast.success("Listing rejected"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const approveEditMut = useMutation({
    mutationFn: (id: string) => approveBusinessListingEdit(id),
    onSuccess: () => { invalidate(); setApproveEditTarget(null); toast.success("Edit approved"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const rejectEditMut = useMutation({
    mutationFn: ({ id, notes }: { id: string; notes?: string }) => rejectBusinessListingEdit(id, notes),
    onSuccess: () => { invalidate(); setRejectEditTarget(null); setRejectNotes(""); toast.success("Edit rejected"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const blacklistMut = useMutation({
    mutationFn: (id: string) => blacklistBusinessListing(id),
    onSuccess: () => { invalidate(); toast.success("Listing blacklisted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const unblacklistMut = useMutation({
    mutationFn: (id: string) => unblacklistBusinessListing(id),
    onSuccess: () => { invalidate(); toast.success("Listing unblacklisted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const listings = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  const editInit = useMemo<FormState | null>(() => {
    if (!editTarget) return null;
    return {
      businessName: editTarget.businessName,
      description: editTarget.description,
      location: editTarget.location,
      phoneNumber: editTarget.phoneNumber ?? "",
      email: editTarget.email ?? "",
      websiteUrl: editTarget.websiteUrl ?? "",
      externalLinkUrl: editTarget.externalLinkUrl ?? "",
      logo: null,
      existingLogoUrl: editTarget.logoUrl ?? "",
      banner: null,
      existingBannerUrl: editTarget.bannerUrl ?? "",
    };
  }, [editTarget]);

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <header className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Business directory</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">Review member submissions and manage alumni-owned businesses.</p>
        </div>
        <Button onClick={() => setShowCreate(true)}>
          <Plus size={16} />Add business
        </Button>
      </header>

      <div className="flex items-center gap-2 flex-wrap">
        {["", "Pending", "Approved", "Rejected", "Blacklisted"].map((s) => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setPage(1); }}
            className={cn(
              "px-3 py-1.5 border text-[12.5px] font-semibold transition-colors",
              statusFilter === s ? "bg-primary/10 text-primary border-blue-300" : "bg-white text-foreground border-border hover:bg-muted"
            )}
          >
            {s === "" ? "All" : s}
          </button>
        ))}
      </div>

      {isLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 5 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : listings.length === 0 ? (
        <EmptyState
          icon={<Store size={40} />}
          title="No listings found"
          description={statusFilter ? `No ${statusFilter.toLowerCase()} listings yet.` : "No business listings yet."}
        />
      ) : (
        <div className="space-y-3">
          {listings.map((l) => (
            <Card key={l.id} className="overflow-hidden border-border/40">
              <CardContent className="p-4 space-y-3">
                <div className="flex items-start justify-between gap-3 flex-wrap">
                  <div className="flex items-start gap-3 min-w-0">
                    <div className="h-12 w-12 shrink-0 overflow-hidden border border-border/40 bg-muted/30 flex items-center justify-center">
                      {l.logoUrl ? <img src={l.logoUrl} alt="" className="w-full h-full object-cover" /> : <Store size={18} className="text-muted-foreground" />}
                    </div>
                    <div className="min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <h3 className="font-bold text-[14px] leading-snug">{l.businessName}</h3>
                        <Badge variant={statusVariant[l.status] ?? "neutral"} size="sm">{l.status}</Badge>
                        {l.hasPendingEdit && <Badge variant="warning" size="sm">Edit pending</Badge>}
                      </div>
                      <p className="text-[12px] text-muted-foreground mt-0.5 flex items-center gap-1">
                        <MapPin size={11} />{l.location}
                      </p>
                      <p className="text-[12px] text-muted-foreground mt-0.5">
                        {l.memberId ? (l.memberName ?? l.memberEmail ?? "Member") : "Added by admin"} &middot; {formatDate(l.createdAt)}
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-1.5 flex-wrap justify-end">
                    {l.status === "Pending" && (
                      <>
                        <Button size="sm" className="h-8 text-[11px] font-bold" onClick={() => approveMut.mutate(l.id)} isLoading={approveMut.isPending && approveMut.variables === l.id}>
                          <CheckCircle size={12} className="mr-1" />Approve
                        </Button>
                        <Button size="sm" variant="outline" className="h-8 text-[11px] font-bold text-destructive hover:text-destructive" onClick={() => setRejectTarget(l)}>
                          <XCircle size={12} className="mr-1" />Reject
                        </Button>
                      </>
                    )}
                    {l.hasPendingEdit && (
                      <>
                        <Button size="sm" variant="outline" className="h-8 text-[11px] font-bold" onClick={() => setApproveEditTarget(l)}>
                          <CheckCircle size={12} className="mr-1" />Approve edit
                        </Button>
                        <Button size="sm" variant="outline" className="h-8 text-[11px] font-bold text-destructive hover:text-destructive" onClick={() => setRejectEditTarget(l)}>
                          <XCircle size={12} className="mr-1" />Reject edit
                        </Button>
                      </>
                    )}
                    {l.status === "Blacklisted" ? (
                      <Button size="sm" variant="outline" className="h-8 text-[11px] font-bold" onClick={() => unblacklistMut.mutate(l.id)} isLoading={unblacklistMut.isPending && unblacklistMut.variables === l.id}>
                        <ShieldCheck size={12} className="mr-1" />Unblacklist
                      </Button>
                    ) : (
                      <Button size="sm" variant="ghost" className="h-8 text-[11px] font-bold text-destructive hover:bg-destructive/10" onClick={() => blacklistMut.mutate(l.id)} isLoading={blacklistMut.isPending && blacklistMut.variables === l.id}>
                        <Ban size={12} className="mr-1" />Blacklist
                      </Button>
                    )}
                    <Button size="sm" variant="ghost" className="h-8 px-2" onClick={() => setEditTarget(l)} title="Edit">
                      <Pencil size={13} />
                    </Button>
                    <Button size="sm" variant="ghost" className="h-8 px-2 text-destructive hover:bg-destructive/10" onClick={() => setDeleteTarget(l)} title="Delete">
                      <Trash2 size={13} />
                    </Button>
                  </div>
                </div>

                {l.description && <p className="text-[12.5px] text-muted-foreground line-clamp-2">{l.description}</p>}

                <div className="flex flex-wrap gap-3 text-[11.5px] text-muted-foreground">
                  {l.phoneNumber && <span className="flex items-center gap-1"><Phone size={11} />{l.phoneNumber}</span>}
                  {l.email && <span className="flex items-center gap-1"><Mail size={11} />{l.email}</span>}
                  {l.websiteUrl && <span className="flex items-center gap-1"><Globe size={11} />{l.websiteUrl}</span>}
                  {l.externalLinkUrl && <span className="flex items-center gap-1"><Link2 size={11} />{l.externalLinkUrl}</span>}
                </div>

                {l.adminNotes && (
                  <p className="text-[11.5px] text-muted-foreground italic border-l-2 border-border pl-2">Admin notes: {l.adminNotes}</p>
                )}

                {l.hasPendingEdit && <PendingEditDiff listing={l} />}
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {totalPages > 1 && <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />}

      {/* Create dialog */}
      <Dialog open={showCreate} onOpenChange={(v) => { if (!v) setShowCreate(false); }}>
        <ListingForm
          title="Add a business"
          init={emptyForm}
          saving={createMut.isPending}
          onSave={(f) => createMut.mutate(f)}
          onCancel={() => setShowCreate(false)}
        />
      </Dialog>

      {/* Edit dialog */}
      <Dialog open={!!editTarget} onOpenChange={(v) => { if (!v) setEditTarget(null); }}>
        {editInit && (
          <ListingForm
            title={`Edit — ${editTarget?.businessName}`}
            init={editInit}
            saving={updateMut.isPending}
            onSave={(f) => editTarget && updateMut.mutate({ id: editTarget.id, f })}
            onCancel={() => setEditTarget(null)}
          />
        )}
      </Dialog>

      <ConfirmModal
        open={!!deleteTarget}
        title="Delete listing"
        message={`Delete "${deleteTarget?.businessName}"? This cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        isLoading={deleteMut.isPending}
        onConfirm={() => deleteTarget && deleteMut.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />

      <ConfirmModal
        open={!!rejectTarget}
        title="Reject submission"
        message={`Reject "${rejectTarget?.businessName}"?`}
        confirmLabel="Reject"
        variant="destructive"
        isLoading={rejectMut.isPending}
        onConfirm={() => rejectTarget && rejectMut.mutate({ id: rejectTarget.id, notes: rejectNotes || undefined })}
        onCancel={() => { setRejectTarget(null); setRejectNotes(""); }}
      >
        <div className="space-y-2">
          <Label>Notes (optional)</Label>
          <Input placeholder="Why is this being rejected?" value={rejectNotes} onChange={(e) => setRejectNotes(e.target.value)} />
        </div>
      </ConfirmModal>

      <ConfirmModal
        open={!!approveEditTarget}
        title="Approve pending edit"
        message={`Apply ${approveEditTarget?.businessName}'s proposed changes to the live listing?`}
        confirmLabel="Approve edit"
        variant="default"
        isLoading={approveEditMut.isPending}
        onConfirm={() => approveEditTarget && approveEditMut.mutate(approveEditTarget.id)}
        onCancel={() => setApproveEditTarget(null)}
      >
        {approveEditTarget && <PendingEditDiff listing={approveEditTarget} />}
      </ConfirmModal>

      <ConfirmModal
        open={!!rejectEditTarget}
        title="Reject pending edit"
        message={`Reject ${rejectEditTarget?.businessName}'s proposed changes? The live listing stays as-is.`}
        confirmLabel="Reject edit"
        variant="destructive"
        isLoading={rejectEditMut.isPending}
        onConfirm={() => rejectEditTarget && rejectEditMut.mutate({ id: rejectEditTarget.id, notes: rejectNotes || undefined })}
        onCancel={() => { setRejectEditTarget(null); setRejectNotes(""); }}
      >
        <div className="space-y-3">
          {rejectEditTarget && <PendingEditDiff listing={rejectEditTarget} />}
          <div className="space-y-2">
            <Label>Notes (optional)</Label>
            <Input placeholder="Why is this being rejected?" value={rejectNotes} onChange={(e) => setRejectNotes(e.target.value)} />
          </div>
        </div>
      </ConfirmModal>
    </div>
  );
}
