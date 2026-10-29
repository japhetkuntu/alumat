"use client";

import { useState } from "react";
import { ChipRow, SegmentedControl } from "@alumni/ui";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { Plus, MessageSquare, Pin, Lock, Clock, Search } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { FormSheet } from "@alumni/ui";
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
import { Badge } from "@alumni/ui";
import { SourceBadge } from "@/components/member/source-badge";
import { UserAvatar } from "@alumni/ui";
import { formatDistanceToNowStrict } from "date-fns";
import { useAuth } from "@/hooks/use-auth";
import { SourceFilterChips } from "@/components/member/source-filter-chips";

/** "3 days ago", never throwing on a bad date. */
function ago(value: string | null | undefined): string {
  if (!value) return "";
  const d = new Date(value);
  return isNaN(d.getTime()) ? "" : `${formatDistanceToNowStrict(d)} ago`;
}

/** Ways to begin, for people who have something to say but not the first line. They only fill the title. */
const STARTERS = [
  { label: "Introduce yourself", title: "Hello, I am new here" },
  { label: "Ask for advice", title: "I would love some advice on " },
  { label: "Share good news", title: "Good news to share: " },
  { label: "Looking for a recommendation", title: "Can anyone recommend " },
];

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
  { value: "unanswered", label: "Needs a reply" },
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
  const { user } = useAuth();

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
    mutationFn: () => createThread({ categoryId: form.categoryId || categories[0]?.id || "", title: form.title, content: form.content }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-forum-threads"] });
      setShowNewThread(false);
      setForm({ categoryId: "", title: "", content: "" });
      toast.success("Thread posted.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const categories    = catsData?.results ?? [];
  const openNewThread = () => {
    // Start in the category you are already looking at.
    if (selectedCategory) setForm((f) => ({ ...f, categoryId: selectedCategory }));
    setShowNewThread(true);
  };
  const threads       = threadsData?.results ?? [];
  const totalPages    = threadsData?.totalPages ?? 1;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      {/* ── Header — title and action stay on one row even on a phone ── */}
      <div className="space-y-1.5">
        <div className="flex items-center justify-between gap-3">
          <h1 className="mb-0 tracking-tight" style={{ fontSize: "clamp(1.5rem, 3vw, 2rem)", fontWeight: 700, color: "var(--foreground)" }}>
            Forum
          </h1>
          <Button
            onClick={openNewThread}
            className="shrink-0 gap-2 font-semibold text-[13.5px]"
            style={{ height: 40 }}
          >
            <Plus size={14} /> New thread
          </Button>
        </div>
        <p className="text-[14px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
          Ask questions, share knowledge, and keep the conversation moving.
        </p>
      </div>

      {/* ── Invitation to speak: looks like a box to type in, opens the same sheet ── */}
      <button
        type="button"
        onClick={openNewThread}
        className="card flex w-full items-center gap-3.5 p-4 text-left transition-colors hover:border-primary/40 sm:p-5"
      >
        <UserAvatar name={user ? `${user.firstName ?? ""} ${user.lastName ?? ""}`.trim() || "Me" : "Me"} size="default" />
        <span className="flex-1 text-[14.5px] text-muted-foreground">What is on your mind{user?.firstName ? `, ${user.firstName}` : ""}? Ask, share or start a conversation.</span>
        <span className="hidden shrink-0 items-center gap-1.5 text-[13px] font-semibold text-primary sm:inline-flex"><Plus size={14} /> Start</span>
      </button>

      {/* ── New thread: a sheet over the page, so it is in view wherever you were on the list ── */}
      <FormSheet
        open={showNewThread}
        onClose={() => setShowNewThread(false)}
        title="Start a new thread"
        description="Ask a question or start a conversation. Members who follow the category will see it."
        formId="new-thread-form"
        submitLabel="Post thread"
        savingLabel="Posting"
        saving={createMut.isPending}
        submitDisabled={form.title.trim().length < 3 || form.content.trim().length < 3}
      >
        <form id="new-thread-form" className="space-y-5" onSubmit={e => { e.preventDefault(); createMut.mutate(); }}>
          <div className="space-y-1.5" role="group" aria-label="Ways to start">
            <p className="text-[12.5px] font-semibold text-muted-foreground">Not sure how to begin?</p>
            <div className="flex flex-wrap gap-2">
              {STARTERS.map((s) => (
                <button
                  key={s.label}
                  type="button"
                  onClick={() => setForm((f) => ({ ...f, title: f.title.trim() ? f.title : s.title }))}
                  className="border border-border px-3 py-1.5 text-[12.5px] font-medium text-muted-foreground transition-colors hover:border-primary hover:text-foreground"
                >
                  {s.label}
                </button>
              ))}
            </div>
          </div>
          <div className="space-y-1.5">
            <Label className="text-[13px] font-semibold">Category</Label>
            <FormSelect
              fullWidth
              placeholder="Select a category"
              value={form.categoryId || categories[0]?.id || ""}
              onValueChange={v => setForm({ ...form, categoryId: v })}
              options={categories.map(c => ({ value: c.id, label: c.name }))}
            />
          </div>
          <div className="space-y-1.5">
            <div className="flex items-baseline justify-between gap-3">
              <Label htmlFor="thread-title" className="text-[13px] font-semibold">Title</Label>
              <span className="text-[12px] tabular-nums text-muted-foreground">{form.title.length}/150</span>
            </div>
            <Input
              id="thread-title"
              placeholder="What would you like to discuss?"
              value={form.title}
              maxLength={150}
              onChange={e => setForm({ ...form, title: e.target.value })}
              className="h-11 text-[14px]"
              autoFocus
              required
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="thread-content" className="text-[13px] font-semibold">Details</Label>
            <Textarea
              id="thread-content"
              placeholder="Share the details, and what kind of answer would help."
              rows={9}
              value={form.content}
              onChange={e => setForm({ ...form, content: e.target.value })}
              className="text-[14px]"
              required
            />
          </div>
        </form>
      </FormSheet>

      {/* ── Search and sort share a row, so the first thread is not pushed below the fold ── */}
      <div className="flex flex-wrap items-center justify-between gap-3">
      <div className="relative w-full sm:max-w-sm">
        <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
          style={{ color: "var(--muted-foreground)" }} />
        <Input
          placeholder="Search threads…"
          value={search}
          onChange={e => { setSearch(e.target.value); setThreadPage(1); }}
          className="h-11 pl-9 text-[14px]"
        />
      </div>

        <SegmentedControl label="Sort threads" options={FILTERS} value={threadFilter} onChange={v => { setThreadFilter(v); setThreadPage(1); }} />
      </div>

      {/* ── Category filters ── */}
      <div className="space-y-3">

        <ChipRow label="Filter by category" activeKey={selectedCategory}>
          {categories.length > 0 && (
            <>
              {[{ id: "", name: "All categories" }, ...categories].map(c => (
                <button
                  key={c.id || "all"}
                  onClick={() => { setSelectedCategory(c.id); setThreadPage(1); }}
                  aria-pressed={selectedCategory === c.id}
                  className={cn(
                    "px-3.5 py-2 text-[12.5px] font-semibold border transition-colors sm:py-1.5",
                    selectedCategory === c.id ? "text-white border-transparent" : "border-border hover:border-primary/40",
                  )}
                  style={selectedCategory === c.id
                    ? { background: "var(--primary)", color: "var(--primary-foreground)" }
                    : { background: "var(--background)", color: "var(--muted-foreground)" }}
                >
                  {c.name}
                </button>
              ))}
            </>
          )}
          <SourceFilterChips value={communityId} onChange={(v) => { setCommunityId(v); setThreadPage(1); }} />
        </ChipRow>
      </div>

      {/* ── Thread list ── */}
      {isLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 5 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : threads.length === 0 ? (
        <EmptyState
          icon={<MessageSquare size={40} />}
          title={search ? "No threads match your search" : "The forum is where members talk"}
          description={search ? "Try a different search term." : "Ask for advice, share news or start a conversation with people from your institution. Start the first discussion."}
          action={!search ? (
            <Button onClick={openNewThread} className="gap-2 font-semibold">
              <Plus size={14} /> New thread
            </Button>
          ) : undefined}
        />
      ) : (
        <div className="space-y-2">
          {threads.map(t => {
            const replies = t.replyCount ?? 0;
            return (
              <Link key={t.id} href={`/forum/${t.id}`} className="block group">
                <div className="card p-4 sm:p-5 transition-all duration-150 hover:-translate-y-0.5">
                  <div className="flex items-start gap-3.5">
                    <UserAvatar name={t.authorName ?? "Member"} src={t.authorProfilePictureUrl} size="default" className="mt-0.5 shrink-0" />
                    <div className="min-w-0 flex-1 space-y-1.5">
                      <div className="flex flex-wrap items-center gap-1.5">
                        {t.isPinned && <Badge variant="warning" size="sm" className="gap-1"><Pin size={9} /> Pinned</Badge>}
                        {t.categoryName && <Badge variant="info" size="sm">{t.categoryName}</Badge>}
                        {t.communityId && <SourceBadge communityId={t.communityId} communityName={t.communityName} />}
                        {t.isClosed && <Badge variant="neutral" size="sm" className="gap-1"><Lock size={9} /> Closed</Badge>}
                      </div>
                      <p className="text-[15px] font-semibold leading-snug transition-colors duration-150 group-hover:text-primary" style={{ color: "var(--foreground)" }}>
                        {t.title}
                      </p>
                      {t.excerpt && <p className="line-clamp-2 text-[13.5px] leading-relaxed text-muted-foreground">{t.excerpt}</p>}
                      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 pt-0.5 text-[12.5px] text-muted-foreground">
                        <span className="font-medium text-foreground/80">{t.authorName ?? "A member"}</span>
                        <span>{ago(t.createdAt)}</span>
                        {replies > 0 ? (
                          <span className="inline-flex items-center gap-1 font-medium text-foreground/80">
                            <MessageSquare size={12} /> {replies} {replies === 1 ? "reply" : "replies"}
                            {t.lastReplyByName && <span className="font-normal text-muted-foreground">, latest from {t.lastReplyByName} {ago(t.lastActivityAt)}</span>}
                          </span>
                        ) : !t.isClosed ? (
                          <span className="inline-flex items-center gap-1 font-semibold text-primary"><MessageSquare size={12} /> Be the first to reply</span>
                        ) : null}
                      </div>
                    </div>
                  </div>
                </div>
              </Link>
            );
          })}
        </div>
      )}

      <Pagination page={threadPage} totalPages={totalPages} onPageChange={setThreadPage} />
    </div>
  );
}
