"use client";

import { LoadError } from "@alumni/ui";
import { ZoomableImage } from "@alumni/ui";
import { ChipRow } from "@alumni/ui";
import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Pin, Pencil, Archive, Eye } from "@alumni/ui";
import Link from "next/link";
import { Pagination } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { FormSheet, FormSection, CheckOption } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { SearchModal } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { RichTextEditor } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { AudienceScopePicker, inferAudienceMode, type AudienceMode } from "@alumni/ui";
import { useInstitutionNavTheme } from "@/components/institution/institution-layout";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { getNewsPost, getNewsPosts, createNewsPost, publishNewsPost, updateNewsPost, getCommunities } from "@/lib/institution-api";
import { MultiImageUpload } from "@alumni/ui";
import { YouTubePreview } from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import type { NewsPost } from "@/types";
import { PublicOption } from "@/components/institution/public-option";

const categories = ["Announcement", "Achievement", "News", "Event", "Opportunity", "Other"];

interface FormState {
  title: string;
  content: string;
  category: string;
  status: string;
  isPinned: string;
  isPublic: boolean;
  audienceMode: AudienceMode;
  yearGroups: number[];
  communityId: string;
  images: File[];
  existingImageUrls: string[];
  youtubeVideoUrls: string;
}

const emptyForm: FormState = { title: "", content: "", category: "News", status: "Draft", isPinned: "false", isPublic: false, audienceMode: "everyone", yearGroups: [], communityId: "", images: [], existingImageUrls: [], youtubeVideoUrls: "" };

function commaToArray(s: string): string[] | undefined {
  const arr = s.split(",").map(x => x.trim()).filter(Boolean);
  return arr.length > 0 ? arr : undefined;
}

function arrayToComma(arr?: string[]): string {
  return arr?.join(", ") ?? "";
}

function PostForm({ id, init, onSave, isSuperAdmin }: {
  id: string; init: FormState; onSave: (f: FormState) => void; isSuperAdmin: boolean;
}) {
  const [form, setForm] = useState(init);
  const f = (k: string, v: string) => setForm(prev => ({ ...prev, [k]: v }));
  const { data: communities = [] } = useQuery({ queryKey: ["communities"], queryFn: getCommunities });
  const { data: navTheme } = useInstitutionNavTheme();
  const isCommunity = navTheme?.organizationType === "Community";
  return (
        <form id={id} className="space-y-5" onSubmit={(e) => { e.preventDefault(); onSave(form); }}>
          <FormSection title="The post">
            <div className="space-y-2"><Label>Title</Label>
              <Input placeholder="e.g. Scholarship fund reaches GH₵ 50,000" value={form.title} onChange={(e) => f("title", e.target.value)} required /></div>
            <div className="space-y-2"><Label>Content</Label>
              <RichTextEditor value={form.content} onChange={(html) => f("content", html)} placeholder="Write the post here…" /></div>
          </FormSection>

          <FormSection title="Images and video" hint="All optional.">
            <div className="space-y-2"><Label>Images</Label>
              <MultiImageUpload files={form.images} existingUrls={form.existingImageUrls}
                onAddFile={(file) => setForm(prev => ({ ...prev, images: [...prev.images, file] }))}
                onRemoveFile={(i) => setForm(prev => ({ ...prev, images: prev.images.filter((_, idx) => idx !== i) }))}
                onRemoveExisting={(i) => setForm(prev => ({ ...prev, existingImageUrls: prev.existingImageUrls.filter((_, idx) => idx !== i) }))}
                label="Add image" /></div>
            <div className="space-y-2"><Label>YouTube links</Label>
              <Input placeholder="https://youtube.com/… (separate several with commas)" value={form.youtubeVideoUrls} onChange={(e) => f("youtubeVideoUrls", e.target.value)} />
              {form.youtubeVideoUrls && (
                <div className="grid grid-cols-2 gap-2">
                  {form.youtubeVideoUrls.split(",").map((u, i) => u.trim() && <YouTubePreview key={i} url={u.trim()} />)}
                </div>
              )}
            </div>
          </FormSection>

          <FormSection title="Publishing">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="space-y-2"><Label>Category</Label>
                <FormSelect className="w-full" value={form.category} onValueChange={(v) => f("category", v)}
                  options={categories.map((c) => ({ value: c, label: c }))} /></div>
              <div className="space-y-2"><Label>Status</Label>
                <FormSelect className="w-full" value={form.status} onValueChange={(v) => f("status", v)}
                  options={[
                    { value: "Draft", label: "Draft (only you see it)" },
                    { value: "Published", label: "Published (members see it)" },
                    ...(form.status === "Archived" ? [{ value: "Archived", label: "Archived" }] : []),
                  ]} /></div>
            </div>
            <CheckOption
              id="news-pinned"
              checked={form.isPinned === "true"}
              onChange={(v) => f("isPinned", v ? "true" : "false")}
              label="Pin to the top"
              hint="Pinned posts stay first in members' news."
            />
            <AudienceScopePicker
              mode={form.audienceMode}
              onModeChange={(mode) => setForm((prev) => ({ ...prev, audienceMode: mode }))}
              communityId={form.communityId}
              onCommunityChange={(v) => f("communityId", v)}
              communities={communities}
              yearGroups={form.yearGroups}
              onYearGroupsChange={(years) => setForm((prev) => ({ ...prev, yearGroups: years }))}
              supportsCommunity
              hideYearGroups={isCommunity}
              restricted={!isSuperAdmin ? { reason: "Regular admins cannot choose an audience. Posts are restricted to your assigned group, chapter, or community." } : undefined}
            />
            <PublicOption id="news-public" checked={form.isPublic} onChange={(v) => setForm((prev) => ({ ...prev, isPublic: v }))} forEveryone={isSuperAdmin && form.audienceMode === "everyone"} noun="story" />
          </FormSection>
        </form>
  );
}

export default function AdminNewsPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const [showCreate, setShowCreate] = useState(false);
  const [pickedPost, setEditPost] = useState<NewsPost | null>(null);
  // "/news?edit=ID" (the Edit button on a post's own page) opens that post's editor here.
  const [linkedEditId] = useState(() => searchParams.get("edit"));   // read once: this page rewrites its own address as filters change
  const [closedEditId, setClosedEditId] = useState<string | null>(null);
  const linkedActive = !!linkedEditId && closedEditId !== linkedEditId;
  const { data: linkedPost } = useQuery({ queryKey: ["admin-news-post", linkedEditId], queryFn: () => getNewsPost(linkedEditId!), enabled: linkedActive });
  const editPost = pickedPost ?? (linkedActive ? linkedPost ?? null : null);
  const [archiveTarget, setArchiveTarget] = useState<NewsPost | null>(null);
  const [publishTarget, setPublishTarget] = useState<NewsPost | null>(null);
  const [search, setSearch] = useState(searchParams.get("search") ?? "");
  const [statusFilter, setStatusFilter] = useState(searchParams.get("status") ?? "");
  const [page, setPage] = useState(Number(searchParams.get("page") ?? "1"));
  const [editError, setEditError] = useState("");
  const pageSize = 20;
  const qc = useQueryClient();

  useEffect(() => {
    const params = new URLSearchParams();
    if (search) params.set("search", search);
    if (statusFilter) params.set("status", statusFilter);
    params.set("page", String(page));

    const searchString = params.toString();
    const currentSearch = typeof window !== "undefined" ? window.location.search.replace(/^\?/, "") : "";

    if (searchString && searchString === currentSearch) return;

    router.replace(`/news?${searchString}`);
  }, [search, statusFilter, page, router]);

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["admin-news", search, statusFilter, page],
    queryFn: () => getNewsPosts(page, pageSize, search || undefined, statusFilter || undefined),
    placeholderData: (prev) => prev,
  });

  const createMut = useMutation({
    mutationFn: (f: FormState) => createNewsPost({
      communityId: isSuperAdmin && f.audienceMode === "community" ? f.communityId || undefined : undefined,
      title: f.title, content: f.content, category: f.category, status: f.status,
      isPinned: f.isPinned === "true",
      isPublic: isSuperAdmin && f.audienceMode === "everyone" ? f.isPublic : false,
      yearGroups: isSuperAdmin && f.audienceMode === "yearGroups" ? f.yearGroups : undefined,
      images: f.images.length > 0 ? f.images : undefined, youtubeVideoUrls: commaToArray(f.youtubeVideoUrls),
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-news"] }); setShowCreate(false); toast.success("Post created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, f }: { id: string; f: FormState }) => updateNewsPost(id, {
      communityId: isSuperAdmin && f.audienceMode === "community" ? f.communityId || undefined : undefined,
      title: f.title, content: f.content, category: f.category, status: f.status,
      isPinned: f.isPinned === "true",
      isPublic: isSuperAdmin && f.audienceMode === "everyone" ? f.isPublic : false,
      yearGroups: isSuperAdmin && f.audienceMode === "yearGroups" ? f.yearGroups : undefined,
      images: f.images.length > 0 ? f.images : undefined,
      existingImageUrls: f.existingImageUrls.length > 0 ? f.existingImageUrls : undefined,
      youtubeVideoUrls: commaToArray(f.youtubeVideoUrls),
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-news"] }); qc.invalidateQueries({ queryKey: ["admin-news-post"] }); setEditPost(null); setClosedEditId(linkedEditId); setEditError(""); toast.success("Post updated"); },
    onError: (e) => { const msg = handleApiError(e); setEditError(msg); toast.error(msg); },
  });

  const publishMut = useMutation({
    mutationFn: (id: string) => publishNewsPost(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-news"] }); setPublishTarget(null); toast.success("Post published"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const archiveMut = useMutation({
    mutationFn: (p: NewsPost) => updateNewsPost(p.id, {
      title: p.title, content: p.content, category: p.category,
      isPinned: p.isPinned, status: "Archived",
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-news"] }); setArchiveTarget(null); toast.success("Post archived"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const posts = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <header className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">News</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">Publish updates for your community.</p>
        </div>
        <Button onClick={() => setShowCreate(true)}>
          <Plus size={16} />Create article
        </Button>
      </header>

      {/* Search + Filter */}
      <div className="flex flex-col sm:flex-row gap-3">
        <div className="w-full sm:flex-1 min-w-0 sm:max-w-sm">
          <SearchModal
            title="Search posts"
            value={search}
            onChange={(value) => { setSearch(value); setPage(1); }}
            placeholder="Search posts..."
          >
            {isLoading ? (
              <p className="text-sm text-muted-foreground">Loading results…</p>
            ) : posts.length === 0 ? (
              <p className="text-sm text-muted-foreground">No posts match your search.</p>
            ) : (
              <div className="space-y-2 max-h-72 overflow-y-auto">
                {posts.slice(0, 5).map((p) => (
                  <div key={p.id} className="flex items-center justify-between gap-3 rounded-lg border border-border/40 bg-muted/30 px-3 py-2">
                    <div className="min-w-0">
                      <p className="text-sm font-semibold truncate">{p.title}</p>
                      <p className="text-xs text-muted-foreground truncate">{p.category} · {p.status}</p>
                    </div>
                    {p.isPinned && <Badge variant="secondary" className="text-[12px] uppercase font-bold">Pinned</Badge>}
                  </div>
                ))}
                {posts.length > 5 && (
                  <p className="text-xs text-muted-foreground">Showing {Math.min(5, posts.length)} of {posts.length} results. Close to view the full list.</p>
                )}
              </div>
            )}
          </SearchModal>
        </div>
        <ChipRow label="Filter" activeKey={String(statusFilter)}>
          {["", "Draft", "Published", "Archived"].map((s) => (
            <button
              key={s}
              aria-pressed={statusFilter === s}
              onClick={() => { setStatusFilter(s); setPage(1); }}
              className={`px-3 py-1.5 border text-[12.5px] font-semibold transition-colors ${
                statusFilter === s ? "bg-primary/10 text-primary border-blue-300" : "bg-white text-foreground border-border hover:bg-muted"
              }`}
            >
              {s === "" ? "All" : s}
            </button>
          ))}
        </ChipRow>
      </div>

      <FormSheet
        open={showCreate || editPost !== null}
        onClose={() => { setShowCreate(false); setEditPost(null); setClosedEditId(linkedEditId); setEditError(""); }}
        title={editPost ? "Edit post" : "Create post"}
        description={editPost ? editPost.title : "Publish an update for your community. Save it as a draft if you are not ready."}
        formId="news-post-form"
        submitLabel={editPost ? "Save changes" : "Save post"}
        saving={createMut.isPending || updateMut.isPending}
        error={editError || null}
        size="lg"
      >
        {editPost ? (
          <PostForm
            key={editPost.id}
            id="news-post-form"
            init={{
              title: editPost.title ?? "",
              content: editPost.content ?? "",
              category: editPost.category || "News",
              status: editPost.status || "Draft",
              isPinned: editPost.isPinned ? "true" : "false",
              isPublic: !!editPost.isPublic,
              audienceMode: inferAudienceMode(editPost.communityId, editPost.yearGroups),
              yearGroups: editPost.yearGroups ?? [],
              communityId: editPost.communityId ?? "",
              images: [],
              existingImageUrls: editPost.imageUrls ?? [],
              youtubeVideoUrls: arrayToComma(editPost.youtubeVideoUrls),
            }}
            isSuperAdmin={isSuperAdmin}
            onSave={(f) => { setEditError(""); updateMut.mutate({ id: editPost.id, f }); }}
          />
        ) : showCreate ? (
          <PostForm id="news-post-form" init={emptyForm} isSuperAdmin={isSuperAdmin} onSave={(f) => createMut.mutate(f)} />
        ) : null}
      </FormSheet>

      {isError || (!isLoading && !data) ? (
        <LoadError onRetry={() => refetch()} />
      ) : isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : posts.length === 0 ? (
        <EmptyState icon={<Pin size={40} />} title="News keeps members informed" description="Publish announcements, achievements and updates. Members see them in their portal. Save a draft first if you are not ready to publish." action={<Button onClick={() => setShowCreate(true)}><Plus size={14} />Create article</Button>} />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
          {posts.map((p, i) => (
            <Card
              key={p.id}
              className="group overflow-hidden flex flex-col border-border/40 hover:border-primary/30 hover:shadow-2xl hover:shadow-primary/5 transition-all duration-500 animate-in fade-in slide-in-from-bottom-6 duration-700"
              style={{ animationDelay: `${i * 80}ms` }}
            >
              {/* Cover Image */}
              <div className="relative h-44 overflow-hidden shrink-0">
                {p.imageUrls && p.imageUrls.length > 0 ? (
                  <ZoomableImage
                    src={p.imageUrls[0]}
                    alt={p.title}
                    wrapperClassName="h-full w-full"
                    className="w-full h-full object-cover"
                  />
                ) : (
                  <div className="w-full h-full bg-gradient-to-br from-primary/10 via-primary/5 to-muted/30 flex items-center justify-center">
                    <span className="text-[72px] font-black text-primary/10 select-none leading-none">
                      {p.category.charAt(0)}
                    </span>
                  </div>
                )}
                <div className="pointer-events-none absolute inset-0 bg-gradient-to-t from-black/40 via-transparent to-transparent" />
                <div className="pointer-events-none absolute top-3 left-3 flex items-center gap-1.5">
                  {p.isPinned && (
                    <div className="bg-orange-500/90 backdrop-blur-sm rounded-full p-1.5">
                      <Pin size={9} className="text-white" />
                    </div>
                  )}
                  <span className="bg-white/90 dark:bg-black/60 backdrop-blur-sm text-[12px] font-black uppercase tracking-widest px-2.5 py-1 text-foreground">
                    {p.category}
                  </span>
                </div>
                <div className="absolute top-3 right-3">
                  <Badge variant={p.status === "Published" ? "success" : p.status === "Archived" ? "warning" : "secondary"} className="text-[12px] font-black uppercase tracking-widest backdrop-blur-sm">
                    {p.status}
                  </Badge>
                </div>
              </div>

              <CardContent className="p-4 flex flex-col flex-1 gap-3">
                <div className="flex-1">
                  <h3 className="font-bold text-[14px] leading-snug line-clamp-2 mb-1.5 group-hover:text-primary transition-colors">
                    {p.title}
                  </h3>
                  {p.content && (
                    <p className="text-[12px] text-muted-foreground line-clamp-3 leading-relaxed">
                      {p.content.replace(/<[^>]+>/g, " ").slice(0, 200)}
                    </p>
                  )}
                </div>

                <div className="flex items-center justify-between text-[12px] text-muted-foreground font-medium">
                  <span>{p.publishedAt ? formatDate(p.publishedAt) : "Not published"}</span>
                  {p.imageUrls && p.imageUrls.length > 1 && (
                    <span>{p.imageUrls.length} images</span>
                  )}
                </div>

                <div className="flex items-center gap-1.5 pt-1 border-t border-border/40">
                  <Link href={`/news/${p.id}`} className="flex-1">
                    <Button size="sm" variant="outline" className="w-full h-8 text-[12px] font-bold"><Eye size={12} />View</Button>
                  </Link>
                  <Button size="icon" variant="ghost" className="h-8 w-8 shrink-0" title="Edit" onClick={() => setEditPost(p)}>
                    <Pencil size={13} />
                  </Button>
                  {p.status === "Draft" && (
                    <Button size="sm" className="h-8 text-[12px] font-bold shrink-0 px-3" onClick={() => setPublishTarget(p)}>Publish</Button>
                  )}
                  {p.status !== "Archived" && (
                    <Button size="icon" variant="ghost" className="h-8 w-8 shrink-0 text-amber-600 hover:bg-amber-50 dark:hover:bg-amber-900/20" title="Archive" onClick={() => setArchiveTarget(p)}>
                      <Archive size={13} />
                    </Button>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      <ConfirmModal
        open={!!publishTarget}
        title="Publish Post"
        message={`Publish "${publishTarget?.title}"? It will be visible to all members.`}
        confirmLabel="Publish"
        variant="default"
        isLoading={publishMut.isPending}
        onConfirm={() => publishTarget && publishMut.mutate(publishTarget.id)}
        onCancel={() => setPublishTarget(null)}
      />
      <ConfirmModal
        open={!!archiveTarget}
        title="Archive Post"
        message={`Archive "${archiveTarget?.title}"? It will be hidden from members but preserved.`}
        confirmLabel="Archive"
        variant="default"
        isLoading={archiveMut.isPending}
        onConfirm={() => archiveTarget && archiveMut.mutate(archiveTarget)}
        onCancel={() => setArchiveTarget(null)}
      />
    </div>
  );
}
