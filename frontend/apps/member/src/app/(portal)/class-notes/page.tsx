"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { MessageSquarePlus, Heart, Trash2, Loader2, Send, BookOpen, GraduationCap } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { formatDistanceToNow } from "date-fns";
import { getInitials, cn } from "@/lib/utils";
import { getClassNotes, createClassNote, toggleClassNoteLike, deleteClassNote } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@/components/ui/skeleton";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { useAuth } from "@/hooks/use-auth";
import type { ClassNote } from "@/types";

const AVATAR_GRADIENTS = [
  "from-violet-500 to-purple-500",
  "from-blue-500 to-cyan-500",
  "from-emerald-500 to-teal-500",
  "from-orange-500 to-rose-500",
  "from-pink-500 to-fuchsia-500",
  "from-amber-500 to-yellow-500",
];

function avatarGrad(name: string) {
  let s = 0;
  for (const c of name) s += c.charCodeAt(0);
  return AVATAR_GRADIENTS[s % AVATAR_GRADIENTS.length];
}

const MAX_CHARS = 1000;

function NoteCard({
  note,
  userId,
  onLike,
  onDelete,
  isDeleting,
  isLiking,
}: {
  note: ClassNote;
  userId?: string;
  onLike: () => void;
  onDelete: () => void;
  isDeleting: boolean;
  isLiking: boolean;
}) {
  const [expanded, setExpanded] = useState(false);
  const isOwn = note.authorId === userId;
  const grad = avatarGrad(note.authorName ?? "A");
  const long = note.content.length > 280;

  return (
    <div className="group relative flex gap-3 sm:gap-4 py-5 border-b border-border/30 last:border-0 px-4 sm:px-6 hover:bg-muted/20 transition-colors duration-150">
      <Avatar className="w-10 h-10 shrink-0 mt-0.5">
        {note.authorProfilePictureUrl && (
          <img src={note.authorProfilePictureUrl} alt={note.authorName ?? ""} className="object-cover" />
        )}
        <AvatarFallback className={cn("text-white font-black text-sm bg-gradient-to-br", grad)}>
          {getInitials(note.authorName ?? "A")}
        </AvatarFallback>
      </Avatar>

      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="font-black text-sm">{note.authorName ?? "Member"}</span>
          {note.yearGroup && (
            <span className="flex items-center gap-0.5 text-[10px] font-bold text-muted-foreground/60 uppercase tracking-wide">
              <GraduationCap size={10} />
              {note.yearGroup}
            </span>
          )}
          <span className="text-[11px] text-muted-foreground/50">
            {formatDistanceToNow(new Date(note.createdAt), { addSuffix: true })}
          </span>
        </div>

        <p className={cn("mt-1.5 text-[14px] leading-relaxed whitespace-pre-wrap text-foreground/90", !expanded && long && "line-clamp-4")}>
          {note.content}
        </p>
        {long && (
          <button onClick={() => setExpanded((v) => !v)} className="text-[12px] font-bold text-primary mt-1 hover:underline">
            {expanded ? "Show less" : "Read more"}
          </button>
        )}

        {note.imageUrl && (
          <img src={note.imageUrl} alt="" className="mt-3 rounded-xl w-full max-h-72 object-cover border border-border/30" />
        )}

        <div className="mt-3 flex items-center gap-3">
          <button
            onClick={onLike}
            disabled={isLiking}
            className={cn(
              "flex items-center gap-1.5 text-[12px] font-semibold rounded-full px-2.5 py-1 transition-all",
              note.isLikedByMe
                ? "text-rose-500 bg-rose-50 dark:bg-rose-950/30"
                : "text-muted-foreground hover:text-rose-500 hover:bg-rose-50 dark:hover:bg-rose-950/30",
            )}
          >
            <Heart size={14} className={cn("transition-transform", note.isLikedByMe && "fill-current scale-110")} />
            <span>{note.likeCount > 0 ? note.likeCount : ""}</span>
            <span>{note.isLikedByMe ? "Liked" : "Like"}</span>
          </button>

          {isOwn && (
            <button
              onClick={onDelete}
              disabled={isDeleting}
              className="flex items-center gap-1 text-[12px] font-semibold text-muted-foreground/50 hover:text-destructive rounded-full px-2 py-1 transition-colors opacity-0 group-hover:opacity-100"
            >
              {isDeleting ? <Loader2 size={12} className="animate-spin" /> : <Trash2 size={12} />}
              Delete
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

export default function ClassNotesPage() {
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [newPost, setNewPost] = useState("");
  const pageSize = 20;
  const qc = useQueryClient();

  const { data: notesData, isLoading } = useQuery({
    queryKey: ["m-class-notes", page],
    queryFn: () => getClassNotes(page, pageSize),
    placeholderData: (prev) => prev,
  });

  const postMut = useMutation({
    mutationFn: () => createClassNote({ content: newPost }),
    onSuccess: () => {
      toast.success("Posted to your class wall!");
      setNewPost("");
      qc.invalidateQueries({ queryKey: ["m-class-notes"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const likeMut = useMutation({
    mutationFn: (noteId: string) => toggleClassNoteLike(noteId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["m-class-notes"] }),
    onError: (e) => toast.error(handleApiError(e)),
  });

  const deleteMut = useMutation({
    mutationFn: (noteId: string) => deleteClassNote(noteId),
    onSuccess: () => {
      toast.success("Note deleted");
      qc.invalidateQueries({ queryKey: ["m-class-notes"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const notes = notesData?.results ?? [];
  const totalPages = notesData?.totalPages ?? 1;
  const charsLeft = MAX_CHARS - newPost.length;

  return (
    <div className="w-full max-w-[680px] mx-auto selection:bg-primary/20">
      {/* Page header banner */}
      <div className="relative overflow-hidden bg-gradient-to-br from-teal-500/10 via-cyan-500/5 to-transparent border-b border-border/40 px-4 sm:px-6 py-8 mb-6">
        <div className="absolute top-0 right-0 w-48 h-48 bg-teal-500/5 rounded-full -translate-y-1/3 translate-x-1/3" />
        <div className="relative flex items-start gap-4">
          <div className="w-11 h-11 rounded-2xl bg-teal-500/15 flex items-center justify-center shrink-0">
            <BookOpen size={20} className="text-teal-600 dark:text-teal-400" />
          </div>
          <div>
            <h1 className="text-2xl sm:text-3xl font-black tracking-tight">Class Notes</h1>
            <p className="text-muted-foreground text-sm font-medium mt-0.5 leading-relaxed">
              {user?.graduationYear
                ? `The wall for Class of ${user.graduationYear} — share updates, memories & milestones.`
                : "Connect with your year group through posts and updates."}
            </p>
          </div>
        </div>
      </div>

      {/* Composer */}
      <div className="px-4 sm:px-6 mb-2">
        <div className="rounded-2xl border border-border/50 bg-background shadow-sm overflow-hidden focus-within:border-primary/40 focus-within:shadow-md focus-within:shadow-primary/5 transition-all duration-200">
          <div className="flex gap-3 p-4">
            <Avatar className="w-9 h-9 shrink-0 mt-0.5">
              <AvatarFallback className={cn("text-white font-black text-sm bg-gradient-to-br", avatarGrad(user?.name ?? "M"))}>
                {getInitials(user?.name ?? "M")}
              </AvatarFallback>
            </Avatar>
            <Textarea
              placeholder={`What's on your mind${user?.graduationYear ? `, Class of ${user.graduationYear}` : ""}?`}
              value={newPost}
              onChange={(e) => setNewPost(e.target.value.slice(0, MAX_CHARS))}
              rows={3}
              className="resize-none border-0 shadow-none focus-visible:ring-0 p-0 text-[14px] leading-relaxed placeholder:text-muted-foreground/50"
            />
          </div>
          <div className="flex items-center justify-between px-4 py-2.5 border-t border-border/30 bg-muted/20">
            <span className={cn("text-[11px] font-medium", charsLeft < 50 ? "text-orange-500" : "text-muted-foreground/50")}>
              {charsLeft} characters remaining
            </span>
            <Button
              size="sm"
              onClick={() => postMut.mutate()}
              disabled={!newPost.trim() || postMut.isPending}
              className="h-8 font-bold text-[12px] px-4"
            >
              {postMut.isPending ? (
                <Loader2 className="w-3.5 h-3.5 mr-1.5 animate-spin" />
              ) : (
                <Send className="w-3.5 h-3.5 mr-1.5" />
              )}
              Post
            </Button>
          </div>
        </div>
      </div>

      {/* Feed */}
      <div className="mt-4 rounded-2xl border border-border/40 bg-background shadow-sm overflow-hidden mx-4 sm:mx-6">
        {isLoading ? (
          <div className="divide-y divide-border/20">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="flex gap-3 p-5 animate-pulse">
                <div className="w-10 h-10 rounded-full bg-muted shrink-0" />
                <div className="flex-1 space-y-2">
                  <div className="h-3 bg-muted rounded w-1/3" />
                  <div className="h-4 bg-muted rounded w-full" />
                  <div className="h-4 bg-muted rounded w-4/5" />
                </div>
              </div>
            ))}
          </div>
        ) : notes.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 px-4 text-center">
            <div className="w-16 h-16 rounded-full bg-muted/50 flex items-center justify-center mb-4">
              <MessageSquarePlus size={28} className="text-muted-foreground/40" />
            </div>
            <p className="font-bold text-base">No class notes yet</p>
            <p className="text-sm text-muted-foreground mt-1">Be the first to post to your year group wall!</p>
          </div>
        ) : (
          <div>
            {notes.map((note) => (
              <NoteCard
                key={note.id}
                note={note}
                userId={user?.id}
                onLike={() => likeMut.mutate(note.id)}
                onDelete={() => deleteMut.mutate(note.id)}
                isDeleting={deleteMut.isPending}
                isLiking={likeMut.isPending}
              />
            ))}
          </div>
        )}
      </div>

      {totalPages > 1 && (
        <div className="px-4 sm:px-6 pb-8 mt-4">
          <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
        </div>
      )}
    </div>
  );
}
