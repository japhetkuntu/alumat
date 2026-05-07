"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { MessageSquare, Pin, Lock } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { FormSelect } from "@/components/ui/select";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { SearchModal } from "@/components/ui/search-modal";
import { formatDate } from "@/lib/utils";
import { getForumCategories, createForumCategory, getForumThreads, pinThread, closeThread, deleteThread } from "@/lib/admin-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { useAuth } from "@/hooks/use-auth";

export default function AdminForumPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const [view, setView] = useState<"categories" | "threads">("threads");
  const [catName, setCatName] = useState("");
  const [catDesc, setCatDesc] = useState("");
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
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
      <div className="p-8 lg:p-12 space-y-6 max-w-7xl mx-auto">
        <EmptyState
          icon={<Lock size={40} />}
          title="Access denied"
          description="Only Super Admins can access forum management."
        />
      </div>
    );
  }

  return (
    <div className="p-8 lg:p-12 space-y-10 max-w-7xl mx-auto">
      <header className="flex items-end justify-between gap-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div className="space-y-1">
          <p className="text-[11px] font-black uppercase tracking-[0.2em] text-primary">Community</p>
          <h1 className="text-4xl font-black tracking-tight text-foreground">Forum Moderation</h1>
          <p className="text-muted-foreground font-medium">Monitor and moderate community discussions</p>
        </div>
        <div className="flex gap-2">
          <Button variant={view === "threads" ? "default" : "outline"} className="font-bold" onClick={() => setView("threads")}>Threads</Button>
          <Button variant={view === "categories" ? "default" : "outline"} className="font-bold" onClick={() => setView("categories")}>Categories</Button>
        </div>
      </header>

      {view === "categories" && (
        <div className="space-y-4">
          <Card>
            <CardHeader><CardTitle className="text-base">Add Category</CardTitle></CardHeader>
            <CardContent>
              <form className="flex gap-3" onSubmit={(e) => { e.preventDefault(); createCatMut.mutate(); }}>
                <div className="flex-1 space-y-2">
                  <Label>Category Name</Label>
                  <Input placeholder="e.g. Career & Jobs" value={catName} onChange={(e) => setCatName(e.target.value)} required />
                </div>
                <div className="flex-1 space-y-2">
                  <Label>Description</Label>
                  <Input placeholder="Short description..." value={catDesc} onChange={(e) => setCatDesc(e.target.value)} />
                </div>
                <div className="pt-8">
                  <Button type="submit" size="sm" disabled={createCatMut.isPending}>{createCatMut.isPending ? "Adding..." : "Add"}</Button>
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
                  <CardContent className="p-4 flex items-center justify-between">
                    <div>
                      <p className="font-semibold">{c.name}</p>
                      {c.description && <p className="text-xs text-muted-foreground">{c.description}</p>}
                    </div>
                    <Badge variant="outline">Sort: {c.sortOrder ?? 0}</Badge>
                  </CardContent>
                </Card>
              ))}
              {categories.length === 0 && <p className="text-muted-foreground text-center py-4">No categories yet</p>}
            </div>
          )}
        </div>
      )}

      {view === "threads" && (
        <div className="space-y-5">
          {/* Search + Filter */}
          <div className="flex flex-col sm:flex-row gap-3 items-start sm:items-center">
            <div className="flex-1 min-w-0 max-w-sm">
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
            <div className="flex items-center gap-2">
              {(["all", "recent", "popular", "pinned"] as const).map((f) => (
                <button
                  key={f}
                  onClick={() => { setThreadFilter(f); setThreadPage(1); }}
                  className={`px-4 py-1.5 rounded-full text-[11px] font-black uppercase tracking-widest transition-all ${
                    threadFilter === f ? "bg-primary text-primary-foreground shadow-md shadow-primary/20" : "bg-muted/50 text-muted-foreground hover:bg-muted"
                  }`}
                >
                  {f === "all" ? "All" : f === "recent" ? "Recent" : f === "popular" ? "Popular" : "Pinned"}
                </button>
              ))}
            </div>
          </div>

          {threadsLoading ? (
            <div className="space-y-3">{Array.from({ length: 4 }).map((_, i) => <CardSkeleton key={i} />)}</div>
          ) : threads.length === 0 ? (
            <EmptyState icon={<MessageSquare size={40} />} title="No threads found" description="No threads match your current filters." className="py-8" />
          ) : threads.map((t, i) => (
            <Card
              key={t.id}
              className="group hover:shadow-md hover:border-primary/20 transition-all duration-200 animate-in fade-in slide-in-from-bottom-4 duration-500"
              style={{ animationDelay: `${i * 40}ms` }}
            >
              <CardContent className="p-5">
                <div className="flex items-start gap-4">
                  <div className="h-9 w-9 rounded-xl bg-primary/5 flex items-center justify-center shrink-0 mt-0.5">
                    <MessageSquare size={16} className="text-primary/60" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 flex-wrap mb-1">
                          {t.categoryName && (
                            <span className="px-2 py-0.5 rounded-full bg-primary/10 text-primary text-[10px] font-black uppercase tracking-wider">
                              {t.categoryName}
                            </span>
                          )}
                          {t.isPinned && (
                            <span className="flex items-center gap-1 px-2 py-0.5 rounded-full bg-orange-500/10 text-orange-600 text-[10px] font-black">
                              <Pin size={9} />Pinned
                            </span>
                          )}
                          {t.isClosed && (
                            <span className="flex items-center gap-1 px-2 py-0.5 rounded-full bg-muted text-muted-foreground text-[10px] font-black">
                              <Lock size={9} />Closed
                            </span>
                          )}
                        </div>
                        <p className="font-bold text-[14px] leading-snug group-hover:text-primary transition-colors">{t.title}</p>
                        <div className="flex items-center gap-4 mt-1.5 text-[11px] text-muted-foreground">
                          <span>{formatDate(t.createdAt)}</span>
                          <span className="flex items-center gap-1"><MessageSquare size={10} /> {t.replyCount} replies</span>
                        </div>
                      </div>
                      <div className="flex items-center gap-1.5 shrink-0">
                        <Button
                          size="sm"
                          variant="outline"
                          className={`h-8 text-[11px] font-bold px-3 ${t.isPinned ? "text-orange-600 border-orange-200 hover:bg-orange-50 dark:border-orange-800" : ""}`}
                          disabled={pinMut.isPending}
                          onClick={() => pinMut.mutate(t.id)}
                        >
                          <Pin size={12} />{t.isPinned ? "Unpin" : "Pin"}
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          className={`h-8 text-[11px] font-bold px-3 ${t.isClosed ? "text-green-600 border-green-200 hover:bg-green-50 dark:border-green-800" : ""}`}
                          disabled={closeMut.isPending}
                          onClick={() => setCloseTarget({ id: t.id, title: t.title, isClosed: t.isClosed })}
                        >
                          <Lock size={12} />{t.isClosed ? "Reopen" : "Close"}
                        </Button>
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-8 w-8 text-destructive hover:bg-destructive/10"
                          disabled={deleteMut.isPending}
                          onClick={() => setDeleteTarget(t.id)}
                        >
                          ✕
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
    </div>
  );
}
