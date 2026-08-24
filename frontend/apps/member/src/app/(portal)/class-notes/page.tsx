"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { MessageSquarePlus, Heart, Trash2, Loader2, Send, BookOpen, GraduationCap } from "lucide-react";
import { Pagination } from "@alumni/ui";
import { toast } from "sonner";
import { Button } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { formatDistanceToNow } from "date-fns";
import { getInitials, cn } from "@alumni/ui";
import { getClassNotes, createClassNote, toggleClassNoteLike, deleteClassNote } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import type { ClassNote } from "@/types";

const MAX_CHARS = 1000;

/* Deterministic avatar color — inline styles, dark-mode safe */
const AVATAR_COLORS = [
  { bg: "rgba(139,92,246,0.12)",  text: "#7c3aed", border: "rgba(139,92,246,0.25)"  },
  { bg: "rgba(59,130,246,0.12)",  text: "#2563eb", border: "rgba(59,130,246,0.25)"  },
  { bg: "rgba(16,185,129,0.12)",  text: "#059669", border: "rgba(16,185,129,0.25)"  },
  { bg: "rgba(245,158,11,0.12)",  text: "#d97706", border: "rgba(245,158,11,0.25)"  },
  { bg: "rgba(236,72,153,0.12)",  text: "#be185d", border: "rgba(236,72,153,0.25)"  },
  { bg: "rgba(239,68,68,0.12)",   text: "#dc2626", border: "rgba(239,68,68,0.25)"   },
];
function avatarColor(name: string) {
  let s = 0;
  for (const c of name) s += c.charCodeAt(0);
  return AVATAR_COLORS[s % AVATAR_COLORS.length];
}

/* Safe relative time */
function relativeTime(dateStr: string | null | undefined): string {
  if (!dateStr) return "";
  const d = new Date(dateStr);
  if (isNaN(d.getTime())) return "";
  return formatDistanceToNow(d, { addSuffix: true });
}

/* ─────────────────────────────────────────────────────────────────────────
   NOTE CARD
   ───────────────────────────────────────────────────────────────────────── */
function NoteCard({
  note, userId, onLike, onDelete, isDeleting, isLiking,
}: {
  note: ClassNote;
  userId?: string;
  onLike: () => void;
  onDelete: () => void;
  isDeleting: boolean;
  isLiking: boolean;
}) {
  const [expanded,      setExpanded]      = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const isOwn  = note.authorId === userId;
  const color  = avatarColor(note.authorName ?? "A");
  const long   = note.content.length > 280;
  const time   = relativeTime(note.createdAt);

  return (
    <div
      className="flex gap-3 sm:gap-4 px-4 sm:px-5 py-4 border-b transition-colors"
      style={{ borderColor: "var(--border)" }}
    >
      {/* Avatar */}
      <div
        className="w-9 h-9 rounded-full flex items-center justify-center shrink-0 mt-0.5 text-[12px] font-semibold overflow-hidden"
        style={{ background: color.bg, border: `1.5px solid ${color.border}`, color: color.text }}
      >
        {note.authorProfilePictureUrl
          ? <img src={note.authorProfilePictureUrl} alt={note.authorName ?? ""} className="w-full h-full object-cover" />
          : getInitials(note.authorName ?? "A")}
      </div>

      <div className="flex-1 min-w-0">
        {/* Author row */}
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>
            {note.authorName ?? "Member"}
          </span>
          {note.yearGroup && (
            <span className="flex items-center gap-0.5 text-[11px]" style={{ color: "var(--muted-foreground)" }}>
              <GraduationCap size={10} /> {note.yearGroup}
            </span>
          )}
          {time && (
            <span className="text-[11.5px]" style={{ color: "var(--muted-foreground)", opacity: 0.55 }}>
              {time}
            </span>
          )}
        </div>

        {/* Content */}
        <p
          className={cn("mt-1.5 text-[14px] leading-relaxed whitespace-pre-wrap", !expanded && long && "line-clamp-4")}
          style={{ color: "var(--foreground)", opacity: 0.9 }}
        >
          {note.content}
        </p>
        {long && (
          <button
            onClick={() => setExpanded(v => !v)}
            className="text-[12.5px] font-semibold mt-1 hover:underline"
            style={{ color: "var(--primary)" }}
          >
            {expanded ? "Show less" : "Read more"}
          </button>
        )}

        {/* Image */}
        {note.imageUrl && (
          <img
            src={note.imageUrl}
            alt=""
            className="mt-3 rounded-xl w-full object-cover border"
            style={{ maxHeight: 280, borderColor: "var(--border)" }}
          />
        )}

        {/* Actions */}
        <div className="mt-3 flex items-center gap-2 flex-wrap">
          {/* Like */}
          <button
            onClick={onLike}
            disabled={isLiking}
            className={cn(
              "flex items-center gap-1.5 text-[12.5px] font-semibold rounded-full px-2.5 py-1 transition-colors",
              note.isLikedByMe
                ? "text-rose-500 bg-rose-50 dark:bg-rose-950/30"
                : "hover:text-rose-500 hover:bg-rose-50 dark:hover:bg-rose-950/30",
            )}
            style={!note.isLikedByMe ? { color: "var(--muted-foreground)" } : {}}
          >
            <Heart size={13} className={cn("transition-transform", note.isLikedByMe && "fill-current scale-110")} />
            {note.likeCount > 0 && <span>{note.likeCount}</span>}
            <span>{note.isLikedByMe ? "Liked" : "Like"}</span>
          </button>

          {/* Delete — always visible on mobile, destructive confirm */}
          {isOwn && (
            confirmDelete ? (
              <div className="flex items-center gap-1.5">
                <span className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>Delete this?</span>
                <button
                  onClick={() => { onDelete(); setConfirmDelete(false); }}
                  disabled={isDeleting}
                  className="text-[12px] font-semibold text-destructive hover:underline"
                >
                  {isDeleting ? <Loader2 size={12} className="animate-spin" /> : "Yes, delete"}
                </button>
                <button
                  onClick={() => setConfirmDelete(false)}
                  className="text-[12px] font-semibold hover:underline"
                  style={{ color: "var(--muted-foreground)" }}
                >
                  Cancel
                </button>
              </div>
            ) : (
              <button
                onClick={() => setConfirmDelete(true)}
                className="flex items-center gap-1 text-[12.5px] font-semibold rounded-full px-2 py-1 transition-colors"
                style={{ color: "var(--muted-foreground)" }}
              >
                <Trash2 size={12} /> Delete
              </button>
            )
          )}
        </div>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PAGE
   ───────────────────────────────────────────────────────────────────────── */
export default function ClassNotesPage() {
  const { user }   = useAuth();
  const [page,     setPage]    = useState(1);
  const [newPost,  setNewPost] = useState("");
  const pageSize = 20;
  const qc = useQueryClient();
  const color    = avatarColor(user?.name ?? "M");
  const charsLeft = MAX_CHARS - newPost.length;

  const { data: notesData, isLoading } = useQuery({
    queryKey:        ["m-class-notes", page],
    queryFn:         () => getClassNotes(page, pageSize),
    placeholderData: (prev) => prev,
  });

  const postMut = useMutation({
    mutationFn: () => createClassNote({ content: newPost }),
    onSuccess: () => {
      toast.success("Posted.");
      setNewPost("");
      qc.invalidateQueries({ queryKey: ["m-class-notes"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const likeMut = useMutation({
    mutationFn: (noteId: string) => toggleClassNoteLike(noteId),
    onSuccess:  () => qc.invalidateQueries({ queryKey: ["m-class-notes"] }),
    onError:    (e) => toast.error(handleApiError(e)),
  });

  const deleteMut = useMutation({
    mutationFn: (noteId: string) => deleteClassNote(noteId),
    onSuccess: () => {
      toast.success("Note deleted.");
      qc.invalidateQueries({ queryKey: ["m-class-notes"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const notes      = notesData?.results ?? [];
  const totalPages = notesData?.totalPages ?? 1;

  return (
    <div className="w-full max-w-[680px] mx-auto p-4 sm:p-6 space-y-5">

      {/* ── Header ── */}
      <div className="flex items-start gap-3">
        <div
          className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0"
          style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
        >
          <BookOpen size={17} style={{ color: "var(--primary)" }} />
        </div>
        <div>
          <h1
            className="font-[family-name:var(--font-display)] tracking-tight"
            style={{ fontSize: "1.4rem", fontWeight: 700, color: "var(--foreground)" }}
          >
            Class notes
          </h1>
          <p className="text-[13.5px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
            {user?.graduationYear
              ? `The wall for Class of ${user.graduationYear} — share updates, memories & milestones.`
              : "Connect with your year group through posts and updates."}
          </p>
        </div>
      </div>

      {/* ── Composer ── */}
      <div className="card overflow-hidden transition-shadow duration-200 focus-within:shadow-sm">
        <div className="flex gap-3 p-4">
          {/* Current user avatar */}
          <div
            className="w-9 h-9 rounded-full flex items-center justify-center shrink-0 mt-0.5 text-[12px] font-semibold"
            style={{ background: color.bg, border: `1.5px solid ${color.border}`, color: color.text }}
          >
            {getInitials(user?.name ?? "M")}
          </div>
          <Textarea
            placeholder={`What's on your mind${user?.graduationYear ? `, Class of ${user.graduationYear}` : ""}?`}
            value={newPost}
            onChange={e => setNewPost(e.target.value.slice(0, MAX_CHARS))}
            rows={3}
            className="resize-none border-0 shadow-none focus-visible:ring-0 p-0 text-[14px] leading-relaxed"
            style={{ color: "var(--foreground)" }}
          />
        </div>
        <div
          className="flex items-center justify-between px-4 py-2.5 border-t"
          style={{ borderColor: "var(--border)", background: "var(--secondary)" }}
        >
          <span
            className="text-[12px] font-medium"
            style={{ color: charsLeft < 50 ? "#d97706" : "var(--muted-foreground)", opacity: charsLeft < 50 ? 1 : 0.6 }}
          >
            {charsLeft} left
          </span>
          <Button
            size="sm"
            onClick={() => postMut.mutate()}
            disabled={!newPost.trim() || postMut.isPending}
            className="gap-1.5 font-semibold text-[13px]"
            style={{ height: 34 }}
          >
            {postMut.isPending
              ? <Loader2 size={13} className="animate-spin" />
              : <><Send size={13} /> Post</>}
          </Button>
        </div>
      </div>

      {/* ── Feed ── */}
      <div className="card overflow-hidden">
        {isLoading ? (
          <div>
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="flex gap-3 p-5 border-b animate-pulse" style={{ borderColor: "var(--border)" }}>
                <div className="w-9 h-9 rounded-full shrink-0" style={{ background: "var(--secondary)" }} />
                <div className="flex-1 space-y-2">
                  <div className="h-3 rounded w-1/3" style={{ background: "var(--secondary)" }} />
                  <div className="h-4 rounded w-full"  style={{ background: "var(--secondary)" }} />
                  <div className="h-4 rounded w-4/5"  style={{ background: "var(--secondary)" }} />
                </div>
              </div>
            ))}
          </div>
        ) : notes.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-14 px-4 text-center gap-3">
            <div
              className="w-14 h-14 rounded-full flex items-center justify-center"
              style={{ background: "var(--secondary)" }}
            >
              <MessageSquarePlus size={24} style={{ color: "var(--muted-foreground)", opacity: 0.4 }} />
            </div>
            <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
              No class notes yet
            </p>
            <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
              Be the first to post to your year group wall.
            </p>
          </div>
        ) : (
          notes.map((note) => (
            <NoteCard
              key={note.id}
              note={note}
              userId={user?.id}
              onLike={() => likeMut.mutate(note.id)}
              onDelete={() => deleteMut.mutate(note.id)}
              isDeleting={deleteMut.isPending && deleteMut.variables === note.id}
              isLiking={likeMut.isPending && likeMut.variables === note.id}
            />
          ))
        )}
      </div>

      {totalPages > 1 && (
        <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
      )}
    </div>
  );
}
