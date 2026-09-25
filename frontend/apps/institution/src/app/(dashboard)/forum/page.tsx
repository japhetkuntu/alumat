"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { MessageSquare, Pin, Lock, Pencil, Trash2 } from "@alumni/ui";
import { Pagination, ChipRow, SegmentedControl } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { SearchModal } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { getForumCategories, createForumCategory, updateForumCategory, deleteForumCategory, getForumThreads, pinThread, closeThread, deleteThread } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";

export default function AdminForumPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const [view, setView] = useState<"categories" | "threads">("threads");
  const [catName, setCatName] = useState("");
  const [catDesc, setCatDesc] = useState("");
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
  const [editingCatId, setEditingCatId] = useState<string | null>(null);
  const [editCatName, setEditCatName] = useState("");
  const [editCatDesc, setEditCatDesc] = useState("");
  const [deleteCatTarget, setDeleteCatTarget] = useState<{ id: string; name: string } | null>(null);
  const [closeTarget, setCloseTarget] = useState<{ id: string; title: string; isClosed: boolean } | null>(null);
  const [threadPage, setThreadPage] = useState(1);
  const [search, setSearch] = useState("");
  const [threadFilter, setThreadFilter] = useState<"all" | "recent" | "popular" | "pinned">("all");
  const [categoryFilter, setCategoryFilter] = useState("");
  const threadPageSize = 20;
  const qc = useQueryClient();

  const { data: categoriesData, isLoading: catsLoading } = useQuery({
    queryKey: ["admin-forum-categories"],
    queryFn: () => getForumCategories(),
    enabled: isSuperAdmin,
  });

  const { data: threadsData, isLoading: threadsLoading } = useQuery({
    queryKey: ["admin-forum-threads", threadPage, search, threadFilter, categoryFilter],
    queryFn: () => getForumThreads(
      threadPage,
      threadPageSize,
      categoryFilter || undefined,
      search || undefined,
      threadFilter === "all" ? undefined : threadFilter,
    ),
    placeholderData: (prev) => prev,
    enabled: isSuperAdmin,
  });

  const createCatMut = useMutation({
    mutationFn: () => createForumCategory(catName, catDesc || undefined),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-forum-categories"] }); setCatName(""); setCatDesc(""); toast.success("Category created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateCatMut = useMutation({
    mutationFn: () => updateForumCategory(editingCatId!, editCatName, editCatDesc || undefined),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-forum-categories"] });
      qc.invalidateQueries({ queryKey: ["admin-forum-threads"] });
      setEditingCatId(null);
      toast.success("Category updated");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const deleteCatMut = useMutation({
    mutationFn: (id: string) => deleteForumCategory(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-forum-categories"] }); setDeleteCatTarget(null); toast.success("Category deleted"); },
    onError: (e) => { setDeleteCatTarget(null); toast.error(handleApiError(e)); },
  });

  const pinMut = useMutation({
    mutationFn: (id: string) => pinThread(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-forum-threads"] }); toast.success("Thread pin toggled"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const closeMut = useMutation({
    mutationFn: (id: string) => closeThread(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-forum-threads"] }); setCloseTarget(null); toast.success("Thread status toggled"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => deleteThread(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-forum-threads"] }); setDeleteTarget(null); toast.success("Thread deleted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const categories = categoriesData?.results ?? [];
  const threads = threadsData?.results ?? [];
  const threadTotalPages = threadsData?.totalPages ?? 1;

  if (!isSuperAdmin) {
    return (
      <div className="p-4 sm:p-8 lg:p-12 space-y-6 max-w-7xl mx-auto">
        <EmptyState
          icon={<Lock size={40} />}
          title="Access denied"
          description="Only Super Admins can access forum management."
          action={<Link href="/dashboard"><Button size="sm" className="font-semibold">Go to dashboard</Button></Link>}
        />
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between sm:gap-4">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Forum moderation</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">SuperAdmin-only controls. Decisions are recorded on each discussion.</p>
        </div>
        <SegmentedControl
          label="Forum view"
          value={view}
          onChange={setView}
          options={[{ value: "threads", label: "Threads" }, { value: "categories", label: "Categories" }] as const}
          className="w-full sm:w-auto"
        />
      </header>

      {view === "categories" && (
        <div className="space-y-4">
          <p className="text-[13px] text-muted-foreground max-w-2xl">
            Categories keep discussions organized. Members pick one when they start a thread. We added a starter set, headed by General, so the forum works from day one. Rename, edit or delete any of them, or add your own.
          </p>
          <Card>
            <CardHeader><CardTitle className="text-base">Add Category</CardTitle></CardHeader>
            <CardContent>
              <form className="flex flex-col gap-3 sm:flex-row" onSubmit={(e) => { e.preventDefault(); createCatMut.mutate(); }}>
                <div className="flex-1 space-y-2">
                  <Label>Category Name</Label>
                  <Input placeholder="e.g. Career & Jobs" value={catName} onChange={(e) => setCatName(e.target.value)} required />
                </div>
                <div className="flex-1 space-y-2">
                  <Label>Description</Label>
                  <Input placeholder="Short description..." value={catDesc} onChange={(e) => setCatDesc(e.target.value)} />
                </div>
                <div className="sm:pt-8">
                  <Button type="submit" size="sm" className="w-full sm:w-auto" disabled={createCatMut.isPending}>{createCatMut.isPending ? "Adding..." : "Add"}</Button>
                </div>
              </form>
            </CardContent>
          </Card>
          {catsLoading ? (
            <div className="space-y-2">{Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}</div>
          ) : (
            <div className="space-y-2">
              {categories.map((c) => (
                <Card key={c.id}>
                  <CardContent className="p-4">
                    {editingCatId === c.id ? (
                      <form className="flex flex-col gap-3 sm:flex-row sm:items-end" onSubmit={(e) => { e.preventDefault(); updateCatMut.mutate(); }}>
                        <div className="flex-1 space-y-2">
                          <Label>Category name</Label>
                          <Input value={editCatName} onChange={(e) => setEditCatName(e.target.value)} required />
                        </div>
                        <div className="flex-1 space-y-2">
                          <Label>Description</Label>
                          <Input value={editCatDesc} onChange={(e) => setEditCatDesc(e.target.value)} placeholder="Short description..." />
                        </div>
                        <div className="flex gap-2">
                          <Button type="submit" size="sm" disabled={updateCatMut.isPending}>{updateCatMut.isPending ? "Saving..." : "Save category"}</Button>
                          <Button type="button" size="sm" variant="outline" onClick={() => setEditingCatId(null)}>Cancel</Button>
                        </div>
                      </form>
                    ) : (
                      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                        <div className="min-w-0">
                          <p className="font-semibold">{c.name}</p>
                          {c.description && <p className="text-xs text-muted-foreground">{c.description}</p>}
                        </div>
                        <div className="flex shrink-0 gap-2">
                          <Button size="sm" variant="outline" onClick={() => { setEditingCatId(c.id); setEditCatName(c.name); setEditCatDesc(c.description ?? ""); }}>
                            <Pencil size={13} className="mr-1.5" />Edit
                          </Button>
                          <Button size="sm" variant="outline" onClick={() => setDeleteCatTarget({ id: c.id, name: c.name })}>
                            <Trash2 size={13} className="mr-1.5" />Delete
                          </Button>
                        </div>
                      </div>
                    )}
                  </CardContent>
                </Card>
              ))}
              {categories.length === 0 && <EmptyState className="py-8" title="Categories organise the forum" description="Add topics such as Careers or Events so members know where to post." />}
            </div>
          )}
        </div>
      )}

      {view === "threads" && (
        <div className="space-y-5">
          {/* Search + Filter */}
          <div className="flex flex-col sm:flex-row gap-3 sm:items-center">
            <div className="w-full sm:flex-1 min-w-0 sm:max-w-sm">
            <SearchModal
              title="Search threads"
              value={search}
              onChange={(value) => { setSearch(value); setThreadPage(1); }}
              placeholder="Search threads..."
            >
              {threadsLoading ? (
                <p className="text-sm text-muted-foreground">Loading results…</p>
              ) : threads.length === 0 ? (
                <p className="text-sm text-muted-foreground">No threads match your search.</p>
              ) : (
                <div className="space-y-2 max-h-72 overflow-y-auto">
                  {threads.slice(0, 5).map((t) => (
                    <div key={t.id} className="flex items-center justify-between gap-3 rounded-lg border border-border/40 bg-muted/30 px-3 py-2">
                      <div className="min-w-0">
                        <p className="text-sm font-semibold truncate">{t.title}</p>
                        <p className="text-xs text-muted-foreground truncate">
                          {t.categoryName || "Uncategorized"} • {formatDate(t.createdAt)}
                        </p>
                      </div>
                      {t.isPinned && <Badge variant="secondary" className="text-[10px] uppercase font-bold">Pinned</Badge>}
                    </div>
                  ))}
                  {threads.length > 5 && (
                    <p className="text-xs text-muted-foreground">Showing {Math.min(5, threads.length)} of {threads.length} results. Close to view the full list.</p>
                  )}
                </div>
              )}
            </SearchModal>
          </div>
            <FormSelect
              value={categoryFilter || "__all__"}
              onValueChange={(v) => { setCategoryFilter(v === "__all__" ? "" : v); setThreadPage(1); }}
              placeholder="All categories"
              className="w-full sm:w-52"
              options={[
                { value: "__all__", label: "All categories" },
                ...categories.map((c) => ({ value: c.id, label: c.name })),
              ]}
            />
            <ChipRow label="Sort threads" activeKey={threadFilter} className="sm:ml-auto">
              {(["all", "recent", "popular", "pinned"] as const).map((f) => (
                <button
                  key={f}
                  aria-pressed={threadFilter === f}
                  onClick={() => { setThreadFilter(f); setThreadPage(1); }}
                  className={`px-4 py-1.5 text-[11px] font-black uppercase tracking-widest transition-all ${
                    threadFilter === f ? "bg-primary text-primary-foreground shadow-md shadow-primary/20" : "bg-muted/50 text-muted-foreground hover:bg-muted"
                  }`}
                >
                  {f === "all" ? "All" : f === "recent" ? "Recent" : f === "popular" ? "Popular" : "Pinned"}
                </button>
              ))}
            </ChipRow>
          </div>

          {threadsLoading ? (
            <div className="space-y-3">{Array.from({ length: 4 }).map((_, i) => <CardSkeleton key={i} />)}</div>
          ) : threads.length === 0 ? (
            (search || categoryFilter || threadFilter !== "all") ? (
              <EmptyState icon={<MessageSquare size={40} />} title="No threads found" description="No threads match your current filters. Try adjusting your search, category, or filter." className="py-8" action={<Button variant="outline" size="sm" className="font-semibold" onClick={() => { setSearch(""); setCategoryFilter(""); setThreadFilter("all"); setThreadPage(1); }}>Clear filters</Button>} />
            ) : (
              <EmptyState icon={<MessageSquare size={40} />} title="Discussions started by members appear here" description="The forum is where members talk to each other. Threads they start show up here so you can moderate them." className="py-8" />
            )
          ) : threads.map((t, i) => (
            <Card
              key={t.id}
              className="group hover:shadow-md hover:border-primary/20 transition-all duration-200 animate-in fade-in slide-in-from-bottom-4 duration-500"
              style={{ animationDelay: `${i * 40}ms` }}
            >
              <CardContent className="p-4 sm:p-5">
                <div className="flex items-start gap-3 sm:gap-4">
                  <div className="hidden sm:flex h-9 w-9 rounded-xl bg-primary/5 flex items-center justify-center shrink-0 mt-0.5">
                    <MessageSquare size={16} className="text-primary/60" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 flex-wrap mb-1">
                          {t.categoryName && (
                            <span className="px-2 py-0.5 bg-accent/10 text-accent text-[10px] font-black uppercase tracking-wider">
                              {t.categoryName}
                            </span>
                          )}
                          {t.isPinned && (
                            <span className="flex items-center gap-1 px-2 py-0.5 bg-orange-500/10 text-orange-600 text-[10px] font-black">
                              <Pin size={9} />Pinned
                            </span>
                          )}
                          {t.isClosed && (
                            <span className="flex items-center gap-1 px-2 py-0.5 bg-muted text-muted-foreground text-[10px] font-black">
                              <Lock size={9} />Closed
                            </span>
                          )}
                        </div>
                        <p className="font-bold text-[14px] leading-snug group-hover:text-primary transition-colors">{t.title}</p>
                        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 mt-1.5 text-[11px] text-muted-foreground">
                          <span className="whitespace-nowrap">{formatDate(t.createdAt)}</span>
                          <span className="flex items-center gap-1 whitespace-nowrap"><MessageSquare size={10} /> {t.replyCount} {t.replyCount === 1 ? "reply" : "replies"}</span>
                        </div>
                      </div>
                      <div className="flex items-center gap-1.5 shrink-0">
                        <Button
                          size="sm"
                          variant="outline"
                          className={`h-9 sm:h-8 text-[11px] font-bold px-3 ${t.isPinned ? "text-orange-600 border-orange-200 hover:bg-orange-50 dark:border-orange-800" : ""}`}
                          disabled={pinMut.isPending}
                          onClick={() => pinMut.mutate(t.id)}
                        >
                          <Pin size={12} />{t.isPinned ? "Unpin" : "Pin"}
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          className={`h-9 sm:h-8 text-[11px] font-bold px-3 ${t.isClosed ? "text-success border-success/30 hover:bg-success/10 dark:border-success/40" : ""}`}
                          disabled={closeMut.isPending}
                          onClick={() => setCloseTarget({ id: t.id, title: t.title, isClosed: t.isClosed })}
                        >
                          <Lock size={12} />{t.isClosed ? "Reopen" : "Close"}
                        </Button>
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-9 w-9 sm:h-8 sm:w-8 ml-auto sm:ml-0 text-destructive hover:bg-destructive/10"
                          disabled={deleteMut.isPending}
                          onClick={() => setDeleteTarget(t.id)}
                          aria-label="Delete thread"
                        >
                          <span aria-hidden="true">✕</span>
                        </Button>
                      </div>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}

          <Pagination page={threadPage} totalPages={threadTotalPages} onPageChange={setThreadPage} />
        </div>
      )}

      <ConfirmModal
        open={!!closeTarget}
        title={closeTarget?.isClosed ? "Reopen Thread" : "Close Thread"}
        message={closeTarget?.isClosed ? `Reopen "${closeTarget.title}" so members can reply again?` : `Close "${closeTarget?.title}" to stop new replies?`}
        confirmLabel={closeTarget?.isClosed ? "Reopen" : "Close"}
        variant="default"
        isLoading={closeMut.isPending}
        onConfirm={() => closeTarget && closeMut.mutate(closeTarget.id)}
        onCancel={() => setCloseTarget(null)}
      />

      <ConfirmModal
        open={!!deleteTarget}
        title="Delete Thread"
        message="Delete this thread? This cannot be undone."
        confirmLabel="Delete"
        variant="destructive"
        isLoading={deleteMut.isPending}
        onConfirm={() => deleteTarget && deleteMut.mutate(deleteTarget)}
        onCancel={() => setDeleteTarget(null)}
      />
      <ConfirmModal
        open={!!deleteCatTarget}
        title="Delete Category"
        message={`Delete the "${deleteCatTarget?.name ?? ""}" category? This only works if it has no threads.`}
        confirmLabel="Delete category"
        variant="destructive"
        isLoading={deleteCatMut.isPending}
        onConfirm={() => deleteCatTarget && deleteCatMut.mutate(deleteCatTarget.id)}
        onCancel={() => setDeleteCatTarget(null)}
      />
    </div>
  );
}
