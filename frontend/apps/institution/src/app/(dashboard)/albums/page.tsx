"use client";

import { FormError } from "@alumni/ui";
import { LoadError } from "@alumni/ui";
import { useState } from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Images, Trash2, Loader2 } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { getAlbums, createAlbum, deleteAlbum, getCommunities, type PhotoAlbum } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { AudienceScopePicker, type AudienceMode } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { useInstitutionNavTheme } from "@/components/institution/institution-layout";

const emptyForm: { title: string; description: string; audienceMode: AudienceMode; communityId: string; yearGroups: number[] } =
  { title: "", description: "", audienceMode: "everyone", communityId: "", yearGroups: [] };

/** Who an album is for, in a few words, for the card. */
function audienceLabel(a: PhotoAlbum, communityName?: string) {
  if (a.communityId) return communityName ?? "A community";
  if (a.yearGroups && a.yearGroups.length > 0) {
    const years = [...a.yearGroups].sort((x, y) => y - x);
    return `Batch${years.length === 1 ? "" : "es"} ${years.length > 3 ? `${years.slice(0, 3).join(", ")} +${years.length - 3}` : years.join(", ")}`;
  }
  return "Everyone";
}

export default function AdminAlbumsPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const [page, setPage] = useState(1);
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [deleteTarget, setDeleteTarget] = useState<PhotoAlbum | null>(null);
  const pageSize = 12;
  const qc = useQueryClient();

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["admin-albums", page],
    queryFn: () => getAlbums(page, pageSize),
    placeholderData: (prev) => prev,
  });

  const { data: communities = [] } = useQuery({ queryKey: ["communities"], queryFn: getCommunities });
  const { data: navTheme } = useInstitutionNavTheme();
  const isCommunity = navTheme?.organizationType === "Community";

  const [formError, setFormError] = useState<string | null>(null);
  const createMut = useMutation({
    onMutate: () => setFormError(null),
    mutationFn: () => createAlbum({
      title: form.title,
      description: form.description || undefined,
      communityId: isSuperAdmin && form.audienceMode === "community" ? form.communityId || undefined : undefined,
      yearGroups: isSuperAdmin && form.audienceMode === "yearGroups" && form.yearGroups.length > 0 ? form.yearGroups : undefined,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-albums"] });
      setShowCreate(false);
      setForm(emptyForm);
      toast.success("Album created");
    },
    onError: (e) => { const message = handleApiError(e); setFormError(message); toast.error(message); },
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => deleteAlbum(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-albums"] });
      setDeleteTarget(null);
      toast.success("Album deleted");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const albums = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <header className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Photo albums</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">Create albums once, then add photos to them over time.</p>
        </div>
        <Button onClick={() => setShowCreate(true)}>
          <Plus size={16} />New album
        </Button>
      </header>

      {isError || (!isLoading && !data) ? (
        <LoadError onRetry={() => refetch()} />
      ) : isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-5">
          {Array.from({ length: 8 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : albums.length === 0 ? (
        <EmptyState
          icon={<Images size={40} />}
          title="Photo albums keep memories alive"
          description="Create an album for a reunion or event and upload the photos. Members see it in their portal and can look back on the day together."
          action={<Button onClick={() => setShowCreate(true)}><Plus size={14} />New album</Button>}
        />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-5">
          {albums.map((a) => (
            <Card key={a.id} className="flex flex-col overflow-hidden group">
              <Link href={`/albums/${a.id}`} className="block">
                {a.coverImageUrl ? (
                  <div className="w-full h-40 overflow-hidden bg-muted/30">
                    <img src={a.coverImageUrl} alt={a.title} className="w-full h-full object-cover transition-transform duration-300 group-hover:scale-105" loading="lazy" />
                  </div>
                ) : (
                  <div className="w-full h-40 bg-muted/40 flex items-center justify-center">
                    <Images size={28} className="text-muted-foreground" />
                  </div>
                )}
              </Link>
              <CardContent className="flex-1 flex flex-col p-4 space-y-1.5">
                <Link href={`/albums/${a.id}`} className="hover:text-primary transition-colors">
                  <h3 className="font-bold text-[14px] leading-snug line-clamp-2">{a.title}</h3>
                </Link>
                {a.description && <p className="text-[12px] text-muted-foreground line-clamp-2">{a.description}</p>}
                <p className="text-[12px] text-muted-foreground mt-auto pt-2">
                  {a.photoCount} photo{a.photoCount === 1 ? "" : "s"} &middot; {formatDate(a.createdAt)}
                </p>
                <p className="text-[12px] font-semibold text-foreground/80">For: {audienceLabel(a, communities.find((c) => c.id === a.communityId)?.name)}</p>
                <div className="flex items-center gap-2 pt-2 mt-1 border-t border-border/40">
                  <Link href={`/albums/${a.id}`} className="flex-1">
                    <Button size="sm" variant="outline" className="w-full h-9 text-[12px] font-bold">Manage</Button>
                  </Link>
                  <Button size="sm" variant="ghost" className="h-9 px-2 text-destructive hover:bg-destructive/10" onClick={() => setDeleteTarget(a)} title="Delete">
                    <Trash2 size={13} />
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {totalPages > 1 && <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />}

      <Dialog open={showCreate} onOpenChange={(v) => { if (!v) { setShowCreate(false); setForm(emptyForm); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New album</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Title</Label>
              <Input
                placeholder="e.g. Homecoming 2026"
                value={form.title}
                onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                required
              />
            </div>
            <div className="space-y-2">
              <Label>Description (optional)</Label>
              <Textarea
                placeholder="What is this album about?"
                rows={3}
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </div>
            <AudienceScopePicker
              mode={form.audienceMode}
              onModeChange={(mode) => setForm((f) => ({ ...f, audienceMode: mode }))}
              communityId={form.communityId}
              onCommunityChange={(v) => setForm((f) => ({ ...f, communityId: v }))}
              communities={communities}
              yearGroups={form.yearGroups}
              onYearGroupsChange={(years) => setForm((f) => ({ ...f, yearGroups: years }))}
              supportsCommunity={communities.length > 0}
              hideYearGroups={isCommunity}
              restricted={!isSuperAdmin ? { reason: "This album will be shared with your own batch or community automatically." } : undefined}
            />
          </div>
          <FormError message={formError} className="mt-3" />
          <DialogFooter>
            <Button variant="outline" onClick={() => { setShowCreate(false); setForm(emptyForm); }}>Cancel</Button>
            <Button disabled={!form.title.trim() || createMut.isPending} onClick={() => createMut.mutate()}>
              {createMut.isPending ? <Loader2 size={14} className="animate-spin mr-2" /> : null}
              Create album
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmModal
        open={!!deleteTarget}
        title="Delete album"
        message={`Delete "${deleteTarget?.title}"? This removes the album and all ${deleteTarget?.photoCount ?? 0} of its photos. This cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        isLoading={deleteMut.isPending}
        onConfirm={() => deleteTarget && deleteMut.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
