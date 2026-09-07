"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { Plus, MessageSquare, Pin, Lock, Clock, Search } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { getForumCategories, getForumThreads, createThread } from "@/lib/member-api";
import { CardSkeleton } from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { EmptyState } from "@alumni/ui";
import Link from "next/link";
import { cn } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { SourceBadge } from "@/components/member/source-badge";
import { SourceFilterChips } from "@/components/member/source-filter-chips";

/* Safe date formatter — never throws on bad input */
function safeDate(value: string | null | undefined): string {
  if (!value) return "";
  const d = new Date(value);
  if (isNaN(d.getTime())) return "";
  return formatDate(value);
}

const FILTERS = [
  { value: "all",     label: "All"     },
  { value: "recent",  label: "Recent"  },
  { value: "popular", label: "Popular" },
  { value: "pinned",  label: "Pinned"  },
] as const;

type ThreadFilter = typeof FILTERS[number]["value"];

export default function MemberForumPage() {
  const searchParams = useSearchParams();
  const [selectedCategory, setSelectedCategory] = useState("");
  const [showNewThread,    setShowNewThread]     = useState(false);
  const [form,             setForm]              = useState({ categoryId: "", title: "", content: "" });
  const [search,           setSearch]            = useState("");
  const [threadFilter,     setThreadFilter]      = useState<ThreadFilter>("all");
  const [threadPage,       setThreadPage]        = useState(1);
  const [communityId,      setCommunityId]       = useState<string | null>(() => searchParams.get("communityId"));
  const threadPageSize = 20;
  const qc = useQueryClient();

  const { data: catsData } = useQuery({
    queryKey: ["m-forum-cats"],
    queryFn:  getForumCategories,
  });

  const { data: threadsData, isLoading } = useQuery({
    queryKey:        ["m-forum-threads", selectedCategory, search, threadFilter, communityId, threadPage],
    queryFn:         () => getForumThreads(
      threadPage, threadPageSize,
      selectedCategory || undefined,
      search || undefined,
      threadFilter === "all" ? undefined : threadFilter,
      communityId || undefined,
    ),
    placeholderData: (prev) => prev,
  });

  const createMut = useMutation({
    mutationFn: () => createThread({ categoryId: form.categoryId, title: form.title, content: form.content }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-forum-threads"] });
      setShowNewThread(false);
      setForm({ categoryId: "", title: "", content: "" });
      toast.success("Thread posted.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const categories    = catsData?.results ?? [];
  const threads       = threadsData?.results ?? [];
  const totalPages    = threadsData?.totalPages ?? 1;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      {/* ── Header ── */}
      <PageHeader
        title="Forum"
        description="Ask questions, share knowledge, and keep the conversation moving."
      >
        <Button
          onClick={() => setShowNewThread(v => !v)}
          className="shrink-0 gap-2 font-semibold text-[13.5px]"
          style={{ height: 40 }}
        >
          <Plus size={14} /> New thread
        </Button>
      </PageHeader>

      {/* ── New thread form ── */}
      {showNewThread && (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-[15px]">Start a new thread</CardTitle>
          </CardHeader>
          <CardContent>
            <form
              className="space-y-4"
              onSubmit={e => { e.preventDefault(); createMut.mutate(); }}
            >
              <div className="space-y-1.5">
                <Label className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>Category</Label>
                <FormSelect
                  placeholder="Select a category"
                  value={form.categoryId}
                  onValueChange={v => setForm({ ...form, categoryId: v })}
                  options={categories.map(c => ({ value: c.id, label: c.name }))}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="thread-title" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>Title</Label>
                <Input
                  id="thread-title"
                  placeholder="What would you like to discuss?"
                  value={form.title}
                  onChange={e => setForm({ ...form, title: e.target.value })}
                  className="h-11 text-[14px]"
                  required
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="thread-content" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>Content</Label>
                <Textarea
                  id="thread-content"
                  placeholder="Share the details…"
                  rows={4}
                  value={form.content}
                  onChange={e => setForm({ ...form, content: e.target.value })}
                  className="text-[14px]"
                  required
                />
              </div>
              <div className="flex gap-2">
                <Button
                  type="submit"
                  size="sm"
                  className="font-semibold gap-1.5"
                  style={{ height: 38 }}
                  disabled={createMut.isPending}
                >
                  {createMut.isPending ? "Posting…" : "Post thread"}
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  style={{ height: 38 }}
                  onClick={() => setShowNewThread(false)}
                >
                  Cancel
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}

      {/* ── Search ── */}
      <div className="relative max-w-sm">
        <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
          style={{ color: "var(--muted-foreground)" }} />
        <Input
          placeholder="Search threads…"
          value={search}
          onChange={e => { setSearch(e.target.value); setThreadPage(1); }}
          className="h-11 pl-9 text-[14px]"
        />
      </div>

      {/* ── Filter + category pills ── */}
      <div className="space-y-3">
        {/* Sort filter */}
        <div className="flex flex-wrap gap-2">
          {FILTERS.map(f => (
            <button
              key={f.value}
              onClick={() => { setThreadFilter(f.value); setThreadPage(1); }}
              className={cn(
                "px-3.5 py-1.5 text-[12.5px] font-semibold border transition-colors",
                threadFilter === f.value ? "text-white border-transparent" : "border-border hover:border-primary/40",
              )}
              style={threadFilter === f.value
                ? { background: "var(--primary)", color: "white" }
                : { background: "var(--background)", color: "var(--muted-foreground)" }}
            >
              {f.label}
            </button>
          ))}
        </div>

        {/* Category + source filter */}
        <div className="flex flex-wrap gap-2">
          {categories.length > 0 && (
            <>
              <button
                onClick={() => { setSelectedCategory(""); setThreadPage(1); }}
                className={cn(
                  "px-3.5 py-1.5 text-[12.5px] font-semibold border transition-colors",
                  selectedCategory === "" ? "text-white border-transparent" : "border-border hover:border-primary/40",
                )}
                style={selectedCategory === ""
                  ? { background: "var(--primary)", color: "white" }
                  : { background: "var(--background)", color: "var(--muted-foreground)" }}
              >
                All categories
              </button>
              {categories.map(c => (
                <button
                  key={c.id}
                  onClick={() => { setSelectedCategory(c.id); setThreadPage(1); }}
                  className={cn(
                    "px-3.5 py-1.5 text-[12.5px] font-semibold border transition-colors",
                    selectedCategory === c.id ? "text-white border-transparent" : "border-border hover:border-primary/40",
                  )}
                  style={selectedCategory === c.id
                    ? { background: "var(--primary)", color: "white" }
                    : { background: "var(--background)", color: "var(--muted-foreground)" }}
                >
                  {c.name}
                </button>
              ))}
            </>
          )}
          <SourceFilterChips value={communityId} onChange={(v) => { setCommunityId(v); setThreadPage(1); }} />
        </div>
      </div>

      {/* ── Thread list ── */}
      {isLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 5 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : threads.length === 0 ? (
        <EmptyState
          icon={<MessageSquare size={40} />}
          title={search ? "No threads match your search" : "No threads yet"}
          description={search ? "Try a different search term." : "Be the first to start a discussion."}
          action={!search ? (
            <Button onClick={() => setShowNewThread(true)} className="gap-2 font-semibold">
              <Plus size={14} /> New thread
            </Button>
          ) : undefined}
        />
      ) : (
        <div className="space-y-2">
          {threads.map(t => (
            <Link key={t.id} href={`/forum/${t.id}`} className="block group">
              <div className="card p-4 sm:p-5 transition-all duration-150 hover:-translate-y-0.5">
                <div className="flex items-start gap-4">
                  <div className="flex-1 min-w-0 space-y-2">

                    {/* Labels */}
                    <div className="flex flex-wrap items-center gap-1.5">
                      <SourceBadge communityId={t.communityId} communityName={t.communityName} />
                      {t.categoryName && (
                        <Badge variant="info" size="sm">{t.categoryName}</Badge>
                      )}
                      {t.isPinned && (
                        <Badge variant="warning" size="sm" className="gap-1">
                          <Pin size={9} /> Pinned
                        </Badge>
                      )}
                      {t.isClosed && (
                        <Badge variant="neutral" size="sm" className="gap-1">
                          <Lock size={9} /> Closed
                        </Badge>
                      )}
                    </div>

                    {/* Title */}
                    <p
                      className="text-[14.5px] font-semibold leading-snug transition-colors duration-150 group-hover:text-primary"
                      style={{ color: "var(--foreground)" }}
                    >
                      {t.title}
                    </p>

                    {/* Meta */}
                    <div className="flex flex-wrap items-center gap-4">
                      {safeDate(t.createdAt) && (
                        <span className="flex items-center gap-1 text-[12px]" style={{ color: "var(--muted-foreground)" }}>
                          <Clock size={11} /> {safeDate(t.createdAt)}
                        </span>
                      )}
                      <span className="flex items-center gap-1 text-[12px]" style={{ color: "var(--muted-foreground)" }}>
                        <MessageSquare size={11} />
                        {t.replyCount ?? 0} {(t.replyCount ?? 0) === 1 ? "reply" : "replies"}
                      </span>
                    </div>
                  </div>

                  <MessageSquare
                    size={17}
                    className="shrink-0 mt-1 transition-colors duration-150 group-hover:text-primary"
                    style={{ color: "var(--muted-foreground)", opacity: 0.4 }}
                  />
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}

      <Pagination page={threadPage} totalPages={totalPages} onPageChange={setThreadPage} />
    </div>
  );
}
