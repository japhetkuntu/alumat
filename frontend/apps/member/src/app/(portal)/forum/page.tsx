"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, MessageSquare, Pin, Lock, Send, ArrowLeft, Clock } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { FormSelect } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { SearchModal } from "@/components/ui/search-modal";
import { UserAvatar } from "@/components/ui/user-avatar";
import { formatDate } from "@/lib/utils";
import { getForumCategories, getForumThreads, createThread, getThreadPosts, replyToThread } from "@/lib/member-api";
import { CardSkeleton } from "@/components/ui/skeleton";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { EmptyState } from "@/components/ui/empty-state";
import type { ForumThread } from "@/types";

export default function MemberForumPage() {
  const [selectedCategory, setSelectedCategory] = useState<string>("");
  const [selectedThread, setSelectedThread] = useState<ForumThread | null>(null);
  const [showNewThread, setShowNewThread] = useState(false);
  const [form, setForm] = useState({ categoryId: "", title: "", content: "" });
  const [replyText, setReplyText] = useState("");
  const [search, setSearch] = useState("");
  const [threadFilter, setThreadFilter] = useState<"all" | "recent" | "popular" | "pinned">("all");
  const [threadPage, setThreadPage] = useState(1);
  const threadPageSize = 20;
  const qc = useQueryClient();

  const { data: catsData } = useQuery({
    queryKey: ["m-forum-cats"],
    queryFn: getForumCategories,
  });

  const { data: threadsData, isLoading } = useQuery({
    queryKey: ["m-forum-threads", selectedCategory, search, threadFilter, threadPage],
    queryFn: () => getForumThreads(
      threadPage,
      threadPageSize,
      selectedCategory || undefined,
      search || undefined,
      threadFilter === "all" ? undefined : threadFilter,
    ),
    placeholderData: (prev) => prev,
  });

  const { data: postsData } = useQuery({
    queryKey: ["m-thread-posts", selectedThread?.id],
    queryFn: () => getThreadPosts(selectedThread!.id),
    enabled: !!selectedThread,
  });

  const createMut = useMutation({
    mutationFn: () => createThread({ categoryId: form.categoryId, title: form.title, content: form.content }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["m-forum-threads"] }); setShowNewThread(false); setForm({ categoryId: "", title: "", content: "" }); toast.success("Thread posted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const replyMut = useMutation({
    mutationFn: () => replyToThread(selectedThread!.id, replyText),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["m-thread-posts", selectedThread?.id] }); setReplyText(""); toast.success("Reply posted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const categories = catsData?.results ?? [];
  const threads = threadsData?.results ?? [];
  const threadTotalPages = threadsData?.totalPages ?? 1;
  const posts = postsData?.results ?? [];

  if (selectedThread) {
    return (
      <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-8 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div className="flex items-start gap-4">
          <Button variant="ghost" size="sm" onClick={() => setSelectedThread(null)} className="shrink-0 mt-0.5">
            <ArrowLeft size={16} />Back
          </Button>
          <div className="flex-1 space-y-2">
            <div className="flex items-center gap-2 flex-wrap">
              {selectedThread.categoryName && (
                <Badge variant="secondary" className="text-[10px] font-bold">{selectedThread.categoryName}</Badge>
              )}
              {selectedThread.isPinned && <Badge className="text-[10px] font-bold bg-orange-500/10 text-orange-600 border-orange-200 dark:border-orange-800"><Pin size={9} className="mr-1" />Pinned</Badge>}
              {selectedThread.isClosed && <Badge variant="outline" className="text-[10px] font-bold"><Lock size={9} className="mr-1" />Closed</Badge>}
            </div>
            <h1 className="text-lg sm:text-xl lg:text-2xl font-extrabold tracking-tight leading-tight">{selectedThread.title}</h1>
            <p className="text-[12px] text-muted-foreground">{formatDate(selectedThread.createdAt)}</p>
          </div>
        </div>

        <div className="space-y-4">
          {posts.map((p, i) => {
            const authorName = p.authorName ?? "Unknown";
            const isFirst = i === 0;
            return (
              <Card key={p.id} className={isFirst ? "border-primary/20 bg-primary/5" : ""}>
                <CardContent className="p-5 flex gap-4">
                  <div className="shrink-0">
                    <UserAvatar
                      src={p.authorProfilePictureUrl}
                      name={authorName}
                      size="sm"
                    />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-2">
                      <span className="font-semibold text-sm">{authorName}</span>
                      {isFirst && <Badge variant="secondary" className="text-[9px] font-bold">OP</Badge>}
                      <span className="text-[11px] text-muted-foreground ml-auto">{formatDate(p.createdAt)}</span>
                    </div>
                    <p className="text-sm whitespace-pre-wrap leading-relaxed text-foreground/90">{p.content}</p>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>

        {!selectedThread.isClosed && (
          <Card>
            <CardContent className="p-4">
              <Textarea
                placeholder="Write your reply..."
                rows={3}
                value={replyText}
                onChange={(e) => setReplyText(e.target.value)}
                className="mb-3"
              />
              <Button size="sm" className="gap-2" disabled={replyMut.isPending || !replyText.trim()} onClick={() => replyMut.mutate()}>
                <Send size={13} />{replyMut.isPending ? "Sending..." : "Post Reply"}
              </Button>
            </CardContent>
          </Card>
        )}
        {selectedThread.isClosed && (
          <p className="text-center text-sm text-muted-foreground py-4">This thread is closed</p>
        )}
      </div>
    );
  }

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-8">
      <header className="flex items-start justify-between gap-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div className="space-y-1">
          <h1 className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tight">Community Forum</h1>
          <p className="text-muted-foreground text-sm sm:text-base lg:text-lg font-medium max-w-xl">Discuss, share ideas, and connect with your fellow UMaT alumni.</p>
        </div>
        <Button onClick={() => setShowNewThread(!showNewThread)} className="shrink-0">
          <Plus size={14} />New Thread
        </Button>
      </header>

      {showNewThread && (
        <Card>
          <CardHeader><CardTitle className="text-base">Start New Thread</CardTitle></CardHeader>
          <CardContent>
            <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); createMut.mutate(); }}>
              <div className="space-y-2">
                <Label>Category</Label>
                <FormSelect placeholder="Select a category" value={form.categoryId} onValueChange={(v) => setForm({ ...form, categoryId: v })}
                  options={categories.map((c) => ({ value: c.id, label: c.name }))} />
              </div>
              <div className="space-y-2">
                <Label>Title</Label>
                <Input placeholder="Thread title..." value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} required />
              </div>
              <div className="space-y-2">
                <Label>Content</Label>
                <Textarea placeholder="What would you like to discuss?" rows={4} value={form.content} onChange={(e) => setForm({ ...form, content: e.target.value })} required />
              </div>
              <div className="flex gap-3">
                <Button type="submit" size="sm" disabled={createMut.isPending}>{createMut.isPending ? "Posting..." : "Post Thread"}</Button>
                <Button type="button" size="sm" variant="outline" onClick={() => setShowNewThread(false)}>Cancel</Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}

      <div className="relative max-w-md animate-in fade-in slide-in-from-bottom-4 duration-700 delay-75">
        <SearchModal
          title="Search threads"
          value={search}
          onChange={(value) => { setSearch(value); setThreadPage(1); }}
          placeholder="Search threads..."
        >
          {isLoading ? (
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

      <div className="flex items-center gap-2 flex-wrap">
        {(["all", "recent", "popular", "pinned"] as const).map((f) => (
          <button
            key={f}
            onClick={() => { setThreadFilter(f); setThreadPage(1); }}
            className={`px-3 py-1.5 rounded-full text-[11px] font-black uppercase tracking-widest transition-all ${
              threadFilter === f ? "bg-primary text-primary-foreground shadow-md shadow-primary/20" : "bg-muted/50 text-muted-foreground hover:bg-muted"
            }`}
          >
            {f === "all" ? "All" : f === "recent" ? "Recent" : f === "popular" ? "Popular" : "Pinned"}
          </button>
        ))}
      </div>

      <div className="flex gap-2 flex-wrap animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
        <button
          onClick={() => setSelectedCategory("")}
          className={`px-3 py-1.5 rounded-full text-[12px] font-semibold transition-all ${
            selectedCategory === ""
              ? "bg-primary text-primary-foreground shadow-sm"
              : "bg-muted text-muted-foreground hover:bg-muted/80"
          }`}
        >All</button>
        {categories.map((c) => (
          <button
            key={c.id}
            onClick={() => setSelectedCategory(c.id)}
            className={`px-3 py-1.5 rounded-full text-[12px] font-semibold transition-all ${
              selectedCategory === c.id
                ? "bg-primary text-primary-foreground shadow-sm"
                : "bg-muted text-muted-foreground hover:bg-muted/80"
            }`}
          >{c.name}</button>
        ))}
      </div>

      {isLoading ? (
        <div className="space-y-3">{Array.from({ length: 4 }).map((_, i) => <CardSkeleton key={i} />)}</div>
      ) : threads.length === 0 ? (
        <EmptyState icon={<MessageSquare size={48} />} title={search ? "No threads match your search" : "No threads yet"} description={search ? "Try a different search term or clear the filter." : "Be the first to start a discussion!"} action={!search ? <Button onClick={() => setShowNewThread(true)}><Plus size={14} />New Thread</Button> : undefined} />
      ) : (
        <div className="space-y-3">
          {threads.map((t, i) => (
            <Card
              key={t.id}
              className="group cursor-pointer border-border/40 hover:border-primary/30 hover:shadow-lg hover:shadow-primary/5 transition-all duration-300 animate-in fade-in slide-in-from-bottom-4 duration-700"
              style={{ animationDelay: `${i * 40}ms` }}
              onClick={() => setSelectedThread(t)}
            >
              <CardContent className="p-5">
                <div className="flex items-start gap-4">
                  <div className="flex-1 min-w-0 space-y-2">
                    <div className="flex items-center gap-2 flex-wrap">
                      {t.categoryName && (
                        <span className="px-2 py-0.5 rounded-full bg-primary/10 text-primary text-[10px] font-bold">{t.categoryName}</span>
                      )}
                      {t.isPinned && <span className="flex items-center gap-1 px-2 py-0.5 rounded-full bg-orange-500/10 text-orange-600 text-[10px] font-bold"><Pin size={9} />Pinned</span>}
                      {t.isClosed && <span className="flex items-center gap-1 px-2 py-0.5 rounded-full bg-muted text-muted-foreground text-[10px] font-bold"><Lock size={9} />Closed</span>}
                    </div>
                    <p className="font-bold text-[15px] leading-snug group-hover:text-primary transition-colors">{t.title}</p>
                    <div className="flex items-center gap-4 text-[11px] text-muted-foreground">
                      <span className="flex items-center gap-1"><Clock size={10} />{formatDate(t.createdAt)}</span>
                      <span className="flex items-center gap-1"><MessageSquare size={10} />{t.replyCount} {t.replyCount === 1 ? "reply" : "replies"}</span>
                    </div>
                  </div>
                  <MessageSquare size={18} className="shrink-0 text-muted-foreground/40 group-hover:text-primary/50 transition-colors mt-1" />
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Pagination page={threadPage} totalPages={threadTotalPages} onPageChange={setThreadPage} />
    </div>
  );
}
