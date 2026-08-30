"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft, Building2, CheckCircle2, Clock, XCircle, Ban, Eye, EyeOff,
  Trash2, Info, MapPin, Phone, Mail, Globe, ExternalLink,
} from "lucide-react";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { ImageUpload } from "@alumni/ui";
import { FormError } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import {
  getMyBusinessListing, submitBusinessListing, updateMyBusinessListing,
  hideMyBusinessListing, unhideMyBusinessListing, deleteMyBusinessListing,
  type BusinessListing, type BusinessListingStatus,
} from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";

const STATUS_META: Record<BusinessListingStatus, { label: string; variant: "success" | "warning" | "destructive" | "neutral"; icon: typeof CheckCircle2; explanation: string }> = {
  Approved: { label: "Approved", variant: "success", icon: CheckCircle2, explanation: "Your listing is live and visible in the public directory." },
  Pending: { label: "Pending Review", variant: "warning", icon: Clock, explanation: "An admin is reviewing your listing — it isn't visible in the public directory yet." },
  Rejected: { label: "Rejected", variant: "destructive", icon: XCircle, explanation: "This listing was rejected. Edit it and it will be resubmitted for review." },
  Blacklisted: { label: "Blacklisted", variant: "neutral", icon: Ban, explanation: "This listing has been blacklisted by an admin and can no longer be edited. You can still hide it from the directory." },
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
  banner: File | null;
}

const EMPTY_FORM: FormState = {
  businessName: "", description: "", location: "",
  phoneNumber: "", email: "", websiteUrl: "", externalLinkUrl: "",
  logo: null, banner: null,
};

function listingToForm(l: BusinessListing): FormState {
  return {
    businessName: l.businessName, description: l.description, location: l.location,
    phoneNumber: l.phoneNumber ?? "", email: l.email ?? "", websiteUrl: l.websiteUrl ?? "", externalLinkUrl: l.externalLinkUrl ?? "",
    logo: null, banner: null,
  };
}

export default function MyBusinessListingPage() {
  const qc = useQueryClient();
  const [form, setForm] = useState<FormState>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const { data: listing, isLoading } = useQuery({
    queryKey: ["m-my-business-listing"],
    queryFn: getMyBusinessListing,
  });

  useEffect(() => {
    if (listing) setForm(listingToForm(listing));
  }, [listing]);

  const invalidate = () => qc.invalidateQueries({ queryKey: ["m-my-business-listing"] });

  const submitMut = useMutation({
    mutationFn: () => submitBusinessListing({
      businessName: form.businessName,
      description: form.description,
      location: form.location,
      phoneNumber: form.phoneNumber || undefined,
      email: form.email || undefined,
      websiteUrl: form.websiteUrl || undefined,
      externalLinkUrl: form.externalLinkUrl || undefined,
      logo: form.logo,
      banner: form.banner,
    }),
    onSuccess: () => {
      toast.success("Business listing submitted for review.");
      invalidate();
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: () => updateMyBusinessListing(listing!.id, {
      businessName: form.businessName,
      description: form.description,
      location: form.location,
      phoneNumber: form.phoneNumber || undefined,
      email: form.email || undefined,
      websiteUrl: form.websiteUrl || undefined,
      externalLinkUrl: form.externalLinkUrl || undefined,
      logo: form.logo,
      banner: form.banner,
    }),
    onSuccess: (updated) => {
      toast.success(updated.hasPendingEdit ? "Edit submitted for admin approval." : "Business listing updated.");
      setEditing(false);
      invalidate();
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const hideMut = useMutation({
    mutationFn: () => (listing!.isHiddenByMember ? unhideMyBusinessListing(listing!.id) : hideMyBusinessListing(listing!.id)),
    onSuccess: (updated) => {
      toast.success(updated.isHiddenByMember ? "Listing hidden from the directory." : "Listing unhidden.");
      invalidate();
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const deleteMut = useMutation({
    mutationFn: () => deleteMyBusinessListing(listing!.id),
    onSuccess: () => {
      toast.success("Business listing withdrawn.");
      setConfirmDelete(false);
      setForm(EMPTY_FORM);
      invalidate();
    },
    onError: (e) => { toast.error(handleApiError(e)); setConfirmDelete(false); },
  });

  const validate = (): boolean => {
    if (!form.businessName.trim()) { setFormError("Business name is required."); return false; }
    if (!form.description.trim()) { setFormError("Description is required."); return false; }
    if (!form.location.trim()) { setFormError("Location is required."); return false; }
    if (!form.phoneNumber.trim() && !form.email.trim() && !form.websiteUrl.trim()) {
      setFormError("At least one contact method (phone, email, or website) is required.");
      return false;
    }
    setFormError(null);
    return true;
  };

  const handleSubmitNew = (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;
    submitMut.mutate();
  };

  const handleSubmitEdit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;
    updateMut.mutate();
  };

  if (isLoading) {
    return (
      <div className="p-8 lg:p-12 space-y-6 max-w-3xl mx-auto">
        <CardSkeleton />
      </div>
    );
  }

  const backLink = (
    <Link href="/business-directory">
      <Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg font-semibold group -ml-2 mb-4">
        <ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />
        Business Directory
      </Button>
    </Link>
  );

  // ── No listing yet — submission form ──────────────────────────────────
  if (!listing) {
    return (
      <div className="p-4 sm:p-8 lg:p-12 max-w-3xl mx-auto space-y-6">
        {backLink}
        <div>
          <h1 className="font-[family-name:var(--font-display)] text-2xl font-bold tracking-tight">List your business</h1>
          <p className="text-muted-foreground text-sm mt-1">Submit your business for admin review. Once approved, it&apos;ll appear in the public directory.</p>
        </div>
        <Card>
          <CardContent className="p-6 lg:p-8">
            <form onSubmit={handleSubmitNew} className="space-y-5">
              <BusinessFormFields form={form} setForm={setForm} />
              <FormError message={formError} />
              <Button type="submit" className="w-full h-11 font-semibold" isLoading={submitMut.isPending} loadingText="Submitting...">
                Submit for review
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    );
  }

  // ── Has a listing — status + management ───────────────────────────────
  const meta = STATUS_META[listing.status] ?? STATUS_META.Pending;
  const isBlacklisted = listing.status === "Blacklisted";
  const canDelete = listing.status === "Pending";

  return (
    <div className="p-4 sm:p-8 lg:p-12 max-w-3xl mx-auto space-y-6">
      {backLink}

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-[family-name:var(--font-display)] text-2xl font-bold tracking-tight">My Business Listing</h1>
          <p className="text-muted-foreground text-sm mt-1">Manage your listing in the alumni business directory.</p>
        </div>
        <Badge variant={meta.variant} className="gap-1 text-[11px] px-3 py-1.5">
          <meta.icon size={12} />{meta.label}
        </Badge>
      </div>

      {/* Status explanation */}
      <div className="flex items-start gap-2.5 rounded-xl border border-border/40 bg-muted/20 px-4 py-3">
        <Info size={15} className="text-muted-foreground shrink-0 mt-0.5" />
        <p className="text-[13px] text-muted-foreground leading-relaxed">{meta.explanation}</p>
      </div>

      {listing.status === "Rejected" && listing.adminNotes && (
        <FormError message={`Admin feedback: ${listing.adminNotes}`} />
      )}

      {/* Pending edit banner */}
      {listing.hasPendingEdit && listing.pendingChanges && (
        <div className="rounded-xl border border-warning/40 bg-warning/5 p-4 space-y-3">
          <div className="flex items-start gap-2.5">
            <Clock size={15} className="text-warning shrink-0 mt-0.5" />
            <div>
              <p className="text-[13.5px] font-semibold">An edit is awaiting admin approval</p>
              <p className="text-[12.5px] text-muted-foreground mt-0.5">
                Your live listing below is unchanged until an admin approves the proposed edit. Submitting another edit will replace this pending proposal.
              </p>
            </div>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-1">
            <div className="rounded-lg border border-border/40 bg-background p-3">
              <p className="text-[10px] font-bold uppercase tracking-wide text-muted-foreground/60 mb-1.5">Currently live</p>
              <p className="text-[13px] font-semibold">{listing.businessName}</p>
              <p className="text-[12px] text-muted-foreground line-clamp-2 mt-0.5">{listing.description}</p>
            </div>
            <div className="rounded-lg border border-warning/30 bg-warning/5 p-3">
              <p className="text-[10px] font-bold uppercase tracking-wide text-warning/80 mb-1.5">Proposed</p>
              <p className="text-[13px] font-semibold">{listing.pendingChanges.businessName ?? listing.businessName}</p>
              <p className="text-[12px] text-muted-foreground line-clamp-2 mt-0.5">{listing.pendingChanges.description ?? listing.description}</p>
            </div>
          </div>
        </div>
      )}

      {/* Live snapshot */}
      <Card>
        <CardContent className="p-6 space-y-4">
          <div className="flex items-start gap-4">
            <div className="h-16 w-16 rounded-xl border border-border/40 bg-muted/30 overflow-hidden flex items-center justify-center shrink-0">
              {listing.logoUrl ? <img src={listing.logoUrl} alt={listing.businessName} className="w-full h-full object-cover" /> : <Building2 size={22} className="text-primary/40" />}
            </div>
            <div className="min-w-0 flex-1">
              <h3 className="text-[15px] font-semibold truncate">{listing.businessName}</h3>
              <p className="text-[12.5px] text-muted-foreground flex items-center gap-1 mt-0.5"><MapPin size={11} />{listing.location}</p>
              <p className="text-[13px] text-foreground/80 mt-2 line-clamp-3">{listing.description}</p>
            </div>
          </div>

          <div className="flex flex-wrap gap-3 pt-3 border-t border-border/40 text-[12.5px] text-muted-foreground">
            {listing.phoneNumber && <span className="flex items-center gap-1"><Phone size={12} />{listing.phoneNumber}</span>}
            {listing.email && <span className="flex items-center gap-1"><Mail size={12} />{listing.email}</span>}
            {listing.websiteUrl && <span className="flex items-center gap-1"><Globe size={12} />{listing.websiteUrl}</span>}
            {listing.externalLinkUrl && <span className="flex items-center gap-1"><ExternalLink size={12} />{listing.externalLinkUrl}</span>}
          </div>

          <div className="flex flex-wrap gap-2 pt-2">
            <Button
              size="sm"
              variant="outline"
              onClick={() => hideMut.mutate()}
              isLoading={hideMut.isPending}
              className="gap-1.5"
            >
              {listing.isHiddenByMember ? <><Eye size={13} />Unhide from directory</> : <><EyeOff size={13} />Hide from directory</>}
            </Button>
            {!isBlacklisted && (
              <Button size="sm" variant="outline" onClick={() => setEditing((v) => !v)} className="gap-1.5">
                {editing ? "Cancel edit" : "Edit listing"}
              </Button>
            )}
            {canDelete && (
              <Button size="sm" variant="destructive" onClick={() => setConfirmDelete(true)} className="gap-1.5">
                <Trash2 size={13} />Withdraw
              </Button>
            )}
          </div>
          {listing.isHiddenByMember && (
            <p className="text-[12px] text-muted-foreground">Hidden — members won&apos;t see this listing in the directory until you unhide it.</p>
          )}
        </CardContent>
      </Card>

      {/* Blacklisted notice */}
      {isBlacklisted && (
        <FormError message="This listing is blacklisted and cannot be edited. You may still hide or unhide it from the directory." />
      )}

      {/* Edit form */}
      {editing && !isBlacklisted && (
        <Card>
          <CardContent className="p-6 lg:p-8">
            <form onSubmit={handleSubmitEdit} className="space-y-5">
              <p className="text-[13px] font-semibold">Edit listing</p>
              <BusinessFormFields form={form} setForm={setForm} existingLogoUrl={listing.logoUrl} existingBannerUrl={listing.bannerUrl} />
              <FormError message={formError} />
              <div className="flex gap-2">
                <Button type="submit" className="flex-1 h-11 font-semibold" isLoading={updateMut.isPending} loadingText="Saving...">
                  {listing.status === "Approved" ? "Submit edit for approval" : "Save changes"}
                </Button>
                <Button type="button" variant="outline" className="h-11" onClick={() => { setEditing(false); setForm(listingToForm(listing)); setFormError(null); }}>
                  Cancel
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}

      <ConfirmModal
        open={confirmDelete}
        title="Withdraw this listing?"
        message="This permanently removes your pending submission. You can submit a new listing afterward."
        confirmLabel="Withdraw"
        isLoading={deleteMut.isPending}
        onConfirm={() => deleteMut.mutate()}
        onCancel={() => setConfirmDelete(false)}
      />
    </div>
  );
}

function BusinessFormFields({
  form, setForm, existingLogoUrl, existingBannerUrl,
}: {
  form: FormState;
  setForm: React.Dispatch<React.SetStateAction<FormState>>;
  existingLogoUrl?: string;
  existingBannerUrl?: string;
}) {
  return (
    <>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <Label required>Business name</Label>
          <Input value={form.businessName} onChange={(e) => setForm((f) => ({ ...f, businessName: e.target.value }))} placeholder="e.g. Summit Consulting" />
        </div>
        <div>
          <Label required>Location</Label>
          <Input value={form.location} onChange={(e) => setForm((f) => ({ ...f, location: e.target.value }))} placeholder="e.g. Accra, Ghana" />
        </div>
      </div>

      <div>
        <Label required>Description</Label>
        <Textarea value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} placeholder="Tell alumni what your business does..." rows={4} />
      </div>

      <div className="space-y-1.5">
        <Label>Contact — at least one required</Label>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Input value={form.phoneNumber} onChange={(e) => setForm((f) => ({ ...f, phoneNumber: e.target.value }))} placeholder="Phone number" />
          <Input value={form.email} onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))} placeholder="Email address" type="email" />
          <Input value={form.websiteUrl} onChange={(e) => setForm((f) => ({ ...f, websiteUrl: e.target.value }))} placeholder="Website URL" />
          <Input value={form.externalLinkUrl} onChange={(e) => setForm((f) => ({ ...f, externalLinkUrl: e.target.value }))} placeholder="Other link (optional)" />
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <Label>Logo</Label>
          <ImageUpload file={form.logo} existingUrl={existingLogoUrl} onChange={(f) => setForm((s) => ({ ...s, logo: f }))} label="Upload logo" />
        </div>
        <div>
          <Label>Banner</Label>
          <ImageUpload file={form.banner} existingUrl={existingBannerUrl} onChange={(f) => setForm((s) => ({ ...s, banner: f }))} label="Upload banner" />
        </div>
      </div>
    </>
  );
}
