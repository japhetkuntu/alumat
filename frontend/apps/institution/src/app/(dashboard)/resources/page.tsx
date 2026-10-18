"use client";

import { LoadError } from "@alumni/ui";
import { ZoomableImage } from "@alumni/ui";
import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Download, Link2, FileText, Pencil, Eye } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { FormSheet, FormSection } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { SearchModal } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import Link from "next/link";
import { getResources, createResource, updateResource, deleteResource, getCommunities } from "@/lib/institution-api";
import { ImageUpload } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { YearGroupPicker } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import type { Resource } from "@/types";

const cats = ["Career", "Professional", "Scholarship", "Technical", "General", "Other"];

const typeColor: Record<string, string> = {
  PDF: "bg-red-600 text-white",
  Video: "bg-blue-600 text-white",
  Link: "bg-teal-600 text-white",
  Document: "bg-purple-600 text-white",
  Image: "bg-orange-600 text-white",
  File: "bg-slate-700 text-white",
};

interface FormState {
  title: string;
  description: string;
  category: string;
  type: string;
  yearGroups: number[];
  externalUrl: string;
  file: File | null;
  existingFileUrl: string;
  bannerImage: File | null;
  existingBannerUrl: string;
  communityId: string;
}
const emptyForm: FormState = { title: "", description: "", category: "General", type: "Link", yearGroups: [], externalUrl: "", file: null, existingFileUrl: "", bannerImage: null, existingBannerUrl: "", communityId: "" };

function ResourceForm({ id, init, onSave, isSuperAdmin }: {
  id: string;
  init: FormState; onSave: (f: FormState) => void;
     isSuperAdmin: boolean;
}) {
  const [form, setForm] = useState(init);
  const f = (k: string, v: string) => setForm(prev => ({ ...prev, [k]: v }));
  const { data: communities = [] } = useQuery({ queryKey: ["communities"], queryFn: getCommunities });
  return (
    <form id={id} className="space-y-5" onSubmit={(e) => { e.preventDefault(); onSave(form); }}>
          <FormSection title="The resource">
            <div className="space-y-2"><Label>Title</Label>
              <Input placeholder="e.g. Member handbook 2026" value={form.title} onChange={(e) => f("title", e.target.value)} required /></div>
            <div className="space-y-2"><Label>Description</Label>
              <Textarea placeholder="What it is and when members would use it." rows={3} value={form.description} onChange={(e) => f("description", e.target.value)} /></div>
            <div className="space-y-2 sm:max-w-[calc(50%-0.5rem)]"><Label>Category</Label>
              <FormSelect className="w-full" value={form.category} onValueChange={(v) => f("category", v)}
                options={cats.map((c) => ({ value: c, label: c }))} /></div>
          </FormSection>

          <FormSection title="What members get">
            <div className="space-y-2"><Label>Type</Label>
              <div className="grid grid-cols-2 gap-2">
                <Button type="button" variant={form.type === "Link" ? "default" : "outline"} onClick={() => f("type", "Link")}>A link</Button>
                <Button type="button" variant={form.type === "File" ? "default" : "outline"} onClick={() => f("type", "File")}>A file</Button>
              </div>
            </div>
            {form.type === "Link" && (
              <div className="space-y-2"><Label>Link</Label>
                <Input type="url" placeholder="https://…" value={form.externalUrl} onChange={(e) => f("externalUrl", e.target.value)} /></div>
            )}
            {form.type === "File" && (
              <div className="space-y-2"><Label>File</Label>
                <ImageUpload file={form.file} existingUrl={form.existingFileUrl} onChange={(file) => setForm(prev => ({ ...prev, file }))} onClearExisting={() => setForm(prev => ({ ...prev, existingFileUrl: "" }))} label="Upload file" accept="*/*" /></div>
            )}
            <div className="space-y-2"><Label>Banner image <span className="font-normal text-muted-foreground">(optional)</span></Label>
              <ImageUpload file={form.bannerImage} existingUrl={form.existingBannerUrl} onChange={(file) => setForm(prev => ({ ...prev, bannerImage: file }))} onClearExisting={() => setForm(prev => ({ ...prev, existingBannerUrl: "" }))} label="Upload banner image" /></div>
          </FormSection>

          <FormSection title="Who can see it">
            {communities.length > 0 && (
              <div className="space-y-2">
                <Label>Group <span className="font-normal text-muted-foreground">(optional)</span></Label>
                <FormSelect
                  className="w-full"
                  value={form.communityId}
                  onValueChange={(v) => f("communityId", v)}
                  options={[{ value: "", label: "Everyone in the institution" }, ...communities.map(c => ({ value: c.id, label: c.name }))]}
                />
                <p className="text-[12.5px] text-muted-foreground">Choose a group to show it only to that group&apos;s approved members.</p>
              </div>
            )}
            <div className="space-y-2"><Label>Year groups</Label>
              {isSuperAdmin ? (
                <YearGroupPicker value={form.yearGroups} onChange={(years) => setForm(prev => ({ ...prev, yearGroups: years }))} />
              ) : (
                <p className="text-[12.5px] text-muted-foreground">Regular admins cannot choose year groups. This resource is limited to your year group.</p>
              )}
            </div>
          </FormSection>
          </form>
  );
}

export default function AdminResourcesPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";

  const [showCreate, setShowCreate] = useState(false);
  const [editResource, setEditResource] = useState<Resource | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Resource | null>(null);
  const [search, setSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [addedAfter, setAddedAfter] = useState("");
  const [addedBefore, setAddedBefore] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const qc = useQueryClient();

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["admin-resources", page, search, categoryFilter, typeFilter, addedAfter, addedBefore],
    queryFn: () => getResources(
      page,
      pageSize,
      categoryFilter || undefined,
      search || undefined,
      typeFilter || undefined,
      addedAfter || undefined,
      addedBefore || undefined,
    ),
    placeholderData: (prev) => prev,
  });

  const createMut = useMutation({
    mutationFn: (f: FormState) => createResource({
      communityId: f.communityId || undefined,
      title: f.title, description: f.description || undefined, category: f.category, type: f.type,
      yearGroups: isSuperAdmin ? (f.yearGroups.length > 0 ? f.yearGroups : undefined) : undefined,
      externalUrl: f.type === "Link" ? (f.externalUrl || undefined) : undefined,
      file: f.type === "File" ? (f.file || undefined) : undefined,
      bannerImage: f.bannerImage || undefined,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-resources"] }); setShowCreate(false); toast.success("Resource created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, f }: { id: string; f: FormState }) => updateResource(id, {
      communityId: f.communityId || undefined,
      title: f.title, description: f.description || undefined, category: f.category, type: f.type,
      yearGroups: isSuperAdmin ? (f.yearGroups.length > 0 ? f.yearGroups : undefined) : undefined,
      externalUrl: f.type === "Link" ? (f.externalUrl || undefined) : undefined,
      file: f.type === "File" ? (f.file || undefined) : undefined,
      bannerImage: f.bannerImage || undefined,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-resources"] }); setEditResource(null); toast.success("Resource updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => deleteResource(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-resources"] }); setDeleteTarget(null); toast.success("Resource deleted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const resources = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <div className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Resources</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">Keep institutional knowledge easy to find and reuse.</p>
        </div>
        <Button onClick={() => setShowCreate(true)}><Plus size={14} />Add resource</Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-3">
        <div className="flex-1 min-w-0">
          <SearchModal
            title="Search resources"
            value={search}
            onChange={(value) => { setSearch(value); setPage(1); }}
            placeholder="Search resources..."
          >
            {isLoading ? (
              <p className="text-sm text-muted-foreground">Loading results…</p>
            ) : resources.length === 0 ? (
              <p className="text-sm text-muted-foreground">No resources match your search.</p>
            ) : (
              <div className="space-y-2 max-h-72 overflow-y-auto">
                {resources.slice(0, 5).map((r) => (
                  <div key={r.id} className="flex items-center justify-between gap-3 rounded-lg border border-border/40 bg-muted/30 px-3 py-2">
                    <div className="min-w-0">
                      <p className="text-sm font-semibold truncate">{r.title}</p>
                      <p className="text-xs text-muted-foreground truncate">{r.category} · {r.type}</p>
                    </div>
                    <span className="text-[12px] font-bold uppercase tracking-widest text-muted-foreground">{formatDate(r.createdAt)}</span>
                  </div>
                ))}
                {resources.length > 5 && (
                  <p className="text-xs text-muted-foreground">Showing {Math.min(5, resources.length)} of {resources.length} results. Close to view the full list.</p>
                )}
              </div>
            )}
          </SearchModal>
        </div>
        <FormSelect
          value={categoryFilter || "__all__"}
          onValueChange={(v) => { setCategoryFilter(v === "__all__" ? "" : v); setPage(1); }}
          options={[{ value: "__all__", label: "All categories" }, ...cats.map((c) => ({ value: c, label: c }))]}
        />
        <FormSelect
          value={typeFilter || "__all__"}
          onValueChange={(v) => { setTypeFilter(v === "__all__" ? "" : v); setPage(1); }}
          options={[
            { value: "__all__", label: "All types" },
            { value: "PDF", label: "PDF" },
            { value: "Video", label: "Video" },
            { value: "Link", label: "Link" },
            { value: "Document", label: "Document" },
            { value: "Image", label: "Image" },
            { value: "File", label: "File" },
          ]}
        />
        <Input type="date" value={addedAfter} onChange={(e) => { setAddedAfter(e.target.value); setPage(1); }} />
        <Input type="date" value={addedBefore} onChange={(e) => { setAddedBefore(e.target.value); setPage(1); }} />
      </div>

      <FormSheet
        open={showCreate || editResource !== null}
        onClose={() => { setShowCreate(false); setEditResource(null); }}
        title={editResource ? "Edit resource" : "Add a resource"}
        description={editResource ? editResource.title : "Files, guides and links members can search and download."}
        formId="resource-form"
        submitLabel={editResource ? "Save changes" : "Add resource"}
        saving={createMut.isPending || updateMut.isPending}
        size="lg"
      >
        {editResource ? (
          <ResourceForm
            key={editResource.id}
            id="resource-form"
            init={{ title: editResource.title, description: editResource.description ?? "",
              category: editResource.category, type: editResource.type,
              yearGroups: editResource.yearGroups ?? [],
              externalUrl: editResource.externalUrl ?? "", file: null,
              existingFileUrl: editResource.fileUrl ?? "",
              bannerImage: null, existingBannerUrl: editResource.bannerImageUrl ?? "",
              communityId: editResource.communityId ?? "" }}
            isSuperAdmin={isSuperAdmin}
            onSave={(f) => updateMut.mutate({ id: editResource.id, f })}
          />
        ) : showCreate ? (
          <ResourceForm id="resource-form" init={emptyForm} isSuperAdmin={isSuperAdmin} onSave={(f) => createMut.mutate(f)} />
        ) : null}
      </FormSheet>

      {isError || (!isLoading && !data) ? (
        <LoadError onRetry={() => refetch()} />
      ) : isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">{Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}</div>
      ) : resources.length === 0 ? (
        <EmptyState icon={<FileText size={40} />} title="A library members can use" description="Add files, guides and useful links. Members can search and download them from their portal." action={<Button size="sm" onClick={() => setShowCreate(true)}><Plus size={14} />Add resource</Button>} />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {resources.map((r) => (
            <Card key={r.id} className="group flex flex-col overflow-hidden border-border/40 hover:border-primary/30 transition-all duration-300 hover:shadow-xl hover:shadow-primary/10">
              <div className="relative h-40 overflow-hidden">
                {r.bannerImageUrl ? (
                  <ZoomableImage src={r.bannerImageUrl} alt={r.title} wrapperClassName="h-full w-full" className="w-full h-full object-cover" />
                ) : (
                  <div className="w-full h-full bg-gradient-to-br from-muted/70 to-muted/30 flex items-center justify-center">
                    {r.type === "File" ? <FileText size={34} className="text-primary/40" /> : <Link2 size={34} className="text-primary/40" />}
                  </div>
                )}
                <div className="pointer-events-none absolute top-3 left-3 flex gap-2">
                  <span className={`px-2 py-0.5 text-[12px] font-black uppercase tracking-wider ${typeColor[r.type] ?? "bg-primary text-primary-foreground"}`}>{r.type}</span>
                  <Badge variant="outline" className="bg-background/80 backdrop-blur-sm text-[12px]">{r.category}</Badge>
                </div>
              </div>
              <CardContent className="p-4 flex-1 flex flex-col gap-3">
                <div className="flex-1">
                  <h3 className="font-bold text-sm line-clamp-2 group-hover:text-primary transition-colors">{r.title}</h3>
                  {r.description && <p className="text-xs text-muted-foreground mt-1 line-clamp-2">{r.description}</p>}
                </div>
                <p className="text-xs text-muted-foreground">Added {formatDate(r.createdAt)} · <Download size={11} className="inline" /> {r.downloadCount ?? 0}</p>
                <div className="flex gap-2">
                  <Link href={`/resources/${r.id}`} className="flex-1"><Button size="sm" variant="outline" className="w-full"><Eye size={12} />View</Button></Link>
                  <Button size="sm" variant="outline" onClick={() => setEditResource(r)}><Pencil size={12} />Edit</Button>
                  <Button size="sm" variant="destructive" onClick={() => setDeleteTarget(r)}>Delete</Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      <ConfirmModal
        open={!!deleteTarget}
        title="Delete Resource"
        message={`Permanently delete "${deleteTarget?.title}"?`}
        confirmLabel="Delete"
        variant="destructive"
        isLoading={deleteMut.isPending}
        onConfirm={() => deleteTarget && deleteMut.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
