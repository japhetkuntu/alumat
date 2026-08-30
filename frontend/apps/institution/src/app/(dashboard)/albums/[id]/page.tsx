"use client";

import { useRef, useState, useMemo, useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Images, Pencil, Trash2, Star, Plus, Loader2, ImageOff } from "lucide-react";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { cn } from "@alumni/ui";
import {
  getAlbum, getAlbumPhotos, updateAlbum, deleteAlbum, addAlbumPhotos, deleteAlbumPhoto, type AlbumPhoto,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { EmptyState } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";

interface UploadingItem {
  key: string;
  previewUrl: string;
}

export default function AdminAlbumDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const qc = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [editOpen, setEditOpen] = useState(false);
  const [editForm, setEditForm] = useState({ title: "", description: "" });
  const [deletePhotoTarget, setDeletePhotoTarget] = useState<AlbumPhoto | null>(null);
  const [deleteAlbumOpen, setDeleteAlbumOpen] = useState(false);
  const [uploading, setUploading] = useState<UploadingItem[]>([]);

  const { data: album, isLoading } = useQuery({
    queryKey: ["admin-album", id],
    queryFn: () => getAlbum(id),
    enabled: !!id,
  });

  const { data: photosPage } = useQuery({
    queryKey: ["admin-album-photos", id],
    queryFn: () => getAlbumPhotos(id, 1, 200),
    enabled: !!id,
  });
  const photos = photosPage?.results ?? null;

  useEffect(() => {
    if (editOpen && album) {
      setEditForm({ title: album.title, description: album.description ?? "" });
    }
  }, [editOpen, album]);

  const saveEditMut = useMutation({
    mutationFn: () => updateAlbum(id, {
      title: editForm.title,
      description: editForm.description || undefined,
      coverImageUrl: album?.coverImageUrl ?? undefined,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-album", id] });
      qc.invalidateQueries({ queryKey: ["admin-albums"] });
      setEditOpen(false);
      toast.success("Album updated");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const deleteAlbumMut = useMutation({
    mutationFn: () => deleteAlbum(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-albums"] });
      toast.success("Album deleted");
      router.push("/albums");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const setCoverMut = useMutation({
    mutationFn: (url: string) => updateAlbum(id, {
      title: album?.title ?? "",
      description: album?.description ?? undefined,
      coverImageUrl: url,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-album", id] });
      qc.invalidateQueries({ queryKey: ["admin-albums"] });
      toast.success("Cover photo updated");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const addPhotosMut = useMutation({
    mutationFn: (files: File[]) => addAlbumPhotos(id, files),
    onSuccess: (result) => {
      qc.invalidateQueries({ queryKey: ["admin-album", id] });
      qc.invalidateQueries({ queryKey: ["admin-album-photos", id] });
      qc.invalidateQueries({ queryKey: ["admin-albums"] });
      toast.success(`Added ${result.addedPhotos.length} photo${result.addedPhotos.length === 1 ? "" : "s"}`);
    },
    onError: (e) => toast.error(handleApiError(e)),
    onSettled: (_data, _err, files) => {
      setUploading((prev) => prev.filter((u) => !files.some((f) => u.key === `${f.name}-${f.size}-${f.lastModified}`)));
    },
  });

  const deletePhotoMut = useMutation({
    mutationFn: (photoId: string) => deleteAlbumPhoto(id, photoId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-album", id] });
      qc.invalidateQueries({ queryKey: ["admin-album-photos", id] });
      qc.invalidateQueries({ queryKey: ["admin-albums"] });
      setDeletePhotoTarget(null);
      toast.success("Photo deleted");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const handleFilesSelected = (fileList: FileList | null) => {
    if (!fileList || fileList.length === 0) return;
    const files = Array.from(fileList);
    const items: UploadingItem[] = files.map((f) => ({
      key: `${f.name}-${f.size}-${f.lastModified}`,
      previewUrl: URL.createObjectURL(f),
    }));
    setUploading((prev) => [...prev, ...items]);
    addPhotosMut.mutate(files);
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  useEffect(() => {
    return () => { uploading.forEach((u) => URL.revokeObjectURL(u.previewUrl)); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const knownPhotos = useMemo(() => photos ?? [], [photos]);

  if (isLoading) {
    return (
      <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
        <Skeleton className="h-8 w-64" />
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-3">
          {Array.from({ length: 10 }).map((_, i) => <Skeleton key={i} className="aspect-square" />)}
        </div>
      </div>
    );
  }

  if (!album) {
    return (
      <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto">
        <EmptyState icon={<Images size={40} />} title="Album not found" description="This album may have been deleted." />
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <button onClick={() => router.push("/albums")} className="inline-flex items-center gap-1.5 text-[12.5px] text-muted-foreground hover:text-foreground transition-colors">
        <ArrowLeft size={14} />Back to albums
      </button>

      <header className="flex items-start justify-between gap-4 flex-wrap">
        <div className="flex items-start gap-4 min-w-0">
          <div className="h-16 w-16 shrink-0 overflow-hidden bg-muted/40 border border-border/40 flex items-center justify-center">
            {album.coverImageUrl ? (
              <img src={album.coverImageUrl} alt="" className="w-full h-full object-cover" />
            ) : (
              <Images size={22} className="text-muted-foreground" />
            )}
          </div>
          <div className="min-w-0">
            <h1 className="text-[20px] sm:text-[25px] font-bold m-0 truncate">{album.title}</h1>
            {album.description && <p className="text-muted-foreground text-[13px] mt-1 max-w-xl">{album.description}</p>}
            <p className="text-[11.5px] text-muted-foreground mt-1">
              {album.photoCount} photo{album.photoCount === 1 ? "" : "s"} &middot; Created {formatDate(album.createdAt)}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => setEditOpen(true)}>
            <Pencil size={13} />Edit
          </Button>
          <Button variant="outline" size="sm" className="text-destructive hover:text-destructive" onClick={() => setDeleteAlbumOpen(true)}>
            <Trash2 size={13} />Delete
          </Button>
        </div>
      </header>

      <div className="flex items-center justify-between gap-3 flex-wrap">
        <p className="text-[13px] font-semibold">Photos</p>
        <div>
          <input ref={fileInputRef} type="file" accept="image/*" multiple className="hidden" onChange={(e) => handleFilesSelected(e.target.files)} />
          <Button size="sm" onClick={() => fileInputRef.current?.click()} isLoading={false}>
            <Plus size={14} />Add photos
          </Button>
        </div>
      </div>

      {knownPhotos.length === 0 && uploading.length === 0 ? (
        <EmptyState
          icon={<ImageOff size={40} />}
          title="No photos added yet"
          description="Add photos to this album — they'll appear here as soon as they upload."
          action={<Button size="sm" onClick={() => fileInputRef.current?.click()}><Plus size={14} />Add photos</Button>}
        />
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-3">
          {knownPhotos.map((p) => {
            const isCover = album.coverImageUrl === p.url;
            return (
              <div key={p.id} className="relative group aspect-square overflow-hidden border border-border/40 bg-muted/30">
                <img src={p.url} alt={p.caption ?? ""} className="w-full h-full object-cover" loading="lazy" />
                {isCover && (
                  <div className="absolute top-1.5 left-1.5 flex items-center gap-1 bg-primary text-primary-foreground text-[9px] font-black uppercase tracking-wide px-1.5 py-0.5 rounded-sm shadow">
                    <Star size={10} className="fill-current" />Cover
                  </div>
                )}
                <div className="absolute inset-0 bg-black/0 group-hover:bg-black/45 transition-colors duration-150 flex items-center justify-center gap-1.5 opacity-0 group-hover:opacity-100">
                  {!isCover && (
                    <Button
                      type="button"
                      size="sm"
                      variant="secondary"
                      className="h-7 px-2 text-[10.5px]"
                      isLoading={setCoverMut.isPending && setCoverMut.variables === p.url}
                      onClick={() => setCoverMut.mutate(p.url)}
                      title="Set as cover"
                    >
                      <Star size={11} />
                    </Button>
                  )}
                  <Button
                    type="button"
                    size="sm"
                    variant="destructive"
                    className="h-7 px-2 text-[10.5px]"
                    onClick={() => setDeletePhotoTarget(p)}
                    title="Delete photo"
                  >
                    <Trash2 size={11} />
                  </Button>
                </div>
              </div>
            );
          })}

          {uploading.map((u) => (
            <div key={u.key} className="relative aspect-square overflow-hidden border border-primary/40 bg-muted/30">
              <img src={u.previewUrl} alt="" className="w-full h-full object-cover opacity-50" />
              <div className="absolute inset-0 flex flex-col items-center justify-center gap-1.5 bg-black/25">
                <Loader2 size={20} className="animate-spin text-white" />
                <span className="text-[9.5px] font-bold text-white uppercase tracking-wide">Uploading</span>
              </div>
            </div>
          ))}

          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            className={cn(
              "aspect-square border-2 border-dashed flex flex-col items-center justify-center gap-1 transition-all duration-150 cursor-pointer",
              "border-muted-foreground/25 text-muted-foreground hover:border-primary hover:text-primary hover:bg-primary/5"
            )}
          >
            <Plus size={18} />
            <span className="text-[10px] font-medium">Add photos</span>
          </button>
        </div>
      )}

      {knownPhotos.length === 0 && !photos && album.photoCount > 0 && (
        <p className="text-[12px] text-muted-foreground -mt-3">
          This album already has {album.photoCount} photo{album.photoCount === 1 ? "" : "s"} from a previous session — add a new photo above to bring the full gallery back into view here, or use the cover thumbnail shown in the header.
        </p>
      )}

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit album</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Title</Label>
              <Input value={editForm.title} onChange={(e) => setEditForm((f) => ({ ...f, title: e.target.value }))} required />
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Textarea rows={3} value={editForm.description} onChange={(e) => setEditForm((f) => ({ ...f, description: e.target.value }))} />
            </div>
            <p className="text-[11.5px] text-muted-foreground">
              Tip: hover any photo below and click the star to set it as this album&apos;s cover photo.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditOpen(false)}>Cancel</Button>
            <Button disabled={!editForm.title.trim() || saveEditMut.isPending} isLoading={saveEditMut.isPending} loadingText="Saving" onClick={() => saveEditMut.mutate()}>
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmModal
        open={!!deletePhotoTarget}
        title="Delete photo"
        message="Delete this photo from the album? This cannot be undone."
        confirmLabel="Delete"
        variant="destructive"
        isLoading={deletePhotoMut.isPending}
        onConfirm={() => deletePhotoTarget && deletePhotoMut.mutate(deletePhotoTarget.id)}
        onCancel={() => setDeletePhotoTarget(null)}
      />

      <ConfirmModal
        open={deleteAlbumOpen}
        title="Delete album"
        message={`Delete "${album.title}"? This removes the album and all ${album.photoCount} of its photos. This cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        isLoading={deleteAlbumMut.isPending}
        onConfirm={() => deleteAlbumMut.mutate()}
        onCancel={() => setDeleteAlbumOpen(false)}
      />
    </div>
  );
}
