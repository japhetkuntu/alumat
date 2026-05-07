"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { MessageSquarePlus, Heart, Trash2, Loader2, Send } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Textarea } from "@/components/ui/textarea";
import { formatDate, getInitials } from "@/lib/utils";
import { getClassNotes, createClassNote, toggleClassNoteLike, deleteClassNote } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { useAuth } from "@/hooks/use-auth";

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
      toast.success("Posted!");
      setNewPost("");
      qc.invalidateQueries({ queryKey: ["m-class-notes"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const likeMut = useMutation({
    mutationFn: (noteId: string) => toggleClassNoteLike(noteId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-class-notes"] });
    },
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

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[800px] mx-auto space-y-6 sm:space-y-8 selection:bg-primary/20">
      <header className="space-y-2 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <h1 className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tight text-foreground">
          Class Notes
        </h1>
        <p className="text-muted-foreground text-sm sm:text-base lg:text-lg font-medium leading-relaxed max-w-2xl">
          {user?.graduationYear
            ? `A private wall for the Class of ${user.graduationYear}. Share updates and memories with your year group.`
            : "Connect with your year group through posts and updates."}
        </p>
      </header>

      {/* New post */}
      <Card>
        <CardContent className="p-4">
          <Textarea
            placeholder="Share something with your classmates..."
            value={newPost}
            onChange={(e) => setNewPost(e.target.value)}
            rows={3}
            className="resize-none"
          />
          <div className="flex justify-end mt-3">
            <Button
              size="sm"
              onClick={() => postMut.mutate()}
              disabled={!newPost.trim() || postMut.isPending}
            >
              {postMut.isPending ? (
                <Loader2 className="w-4 h-4 mr-1 animate-spin" />
              ) : (
                <Send className="w-4 h-4 mr-1" />
              )}
              Post
            </Button>
          </div>
        </CardContent>
      </Card>

      {isLoading ? (
        <div className="space-y-4">
          {Array.from({ length: 4 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : notes.length === 0 ? (
        <EmptyState
          icon={<MessageSquarePlus size={48} />}
          title="No class notes yet"
          description="Be the first to post to your year group wall!"
        />
      ) : (
        <div className="space-y-4">
          {notes.map((note) => (
            <Card key={note.id} className="hover:shadow-sm transition-shadow">
              <CardContent className="p-4 sm:p-5">
                <div className="flex items-start gap-3">
                  <Avatar className="w-9 h-9 shrink-0">
                    <AvatarFallback className="text-xs">{getInitials(note.authorName ?? "")}</AvatarFallback>
                  </Avatar>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <span className="font-semibold text-sm">{note.authorName}</span>
                        <span className="text-xs text-muted-foreground">{formatDate(note.createdAt)}</span>
                      </div>
                      {note.authorId === user?.id && (
                        <Button
                          variant="ghost"
                          size="icon"
                          className="w-7 h-7 text-muted-foreground hover:text-destructive"
                          onClick={() => deleteMut.mutate(note.id)}
                          disabled={deleteMut.isPending}
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </Button>
                      )}
                    </div>
                    <p className="mt-1.5 text-sm whitespace-pre-wrap leading-relaxed">{note.content}</p>
                    {note.imageUrl && (
                      <img src={note.imageUrl} alt="" className="mt-3 rounded-lg max-h-64 object-cover" />
                    )}
                    <div className="mt-3 flex items-center gap-1">
                      <Button
                        variant="ghost"
                        size="sm"
                        className={`h-8 px-2 gap-1 ${note.isLikedByMe ? "text-red-500" : "text-muted-foreground"}`}
                        onClick={() => likeMut.mutate(note.id)}
                        disabled={likeMut.isPending}
                      >
                        <Heart className={`w-4 h-4 ${note.isLikedByMe ? "fill-current" : ""}`} />
                        <span className="text-xs">{note.likeCount}</span>
                      </Button>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {totalPages > 1 && <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />}
    </div>
  );
}
