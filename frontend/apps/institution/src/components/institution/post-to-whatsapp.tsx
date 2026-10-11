"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button, Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle, FormError, Label, Textarea } from "@alumni/ui";
import { getWhatsAppOverview, previewWhatsAppPost, publishWhatsAppPost, type WhatsAppContentType } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";

/**
 * "Post to a WhatsApp group" for an event, job, news article or fundraiser. The administrator picks a connected group, sees the
 * exact message and link, may edit the wording, and confirms. Nothing is sent before that, and only full administrators see the
 * button at all. Renders nothing when no group is connected: there is nothing to post to.
 */
export function PostToWhatsAppButton({ type, contentId, size = "sm" }: { type: WhatsAppContentType; contentId: string; size?: "sm" | "default" }) {
  const { user } = useAuth();
  const isAdmin = user?.role === "SuperAdmin";
  const [open, setOpen] = useState(false);
  const [groupId, setGroupId] = useState("");
  const [text, setText] = useState("");
  const [link, setLink] = useState("");
  const [error, setError] = useState<string | null>(null);

  const overview = useQuery({ queryKey: ["whatsapp-overview"], queryFn: getWhatsAppOverview, enabled: isAdmin, staleTime: 60_000, retry: false });
  const groups = (overview.data?.groups ?? []).filter((g) => g.status === "Active" && g.publishingEnabled);

  const preview = useMutation({
    mutationFn: (gid: string) => previewWhatsAppPost({ type, contentId, groupId: gid }),
    onSuccess: (p) => { setText(p.text); setLink(p.link); setError(null); },
    onError: (e) => { setText(""); setError(handleApiError(e)); },
  });
  const publish = useMutation({
    mutationFn: () => publishWhatsAppPost({ type, contentId, groupId, text }),
    onSuccess: () => { toast.success("Queued. It will appear in the group shortly."); setOpen(false); },
    onError: (e) => setError(handleApiError(e)),
  });

  // Open on the only group when there is just one.
  useEffect(() => {
    if (open && !groupId && groups.length === 1) { setGroupId(groups[0].id); preview.mutate(groups[0].id); }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, groups.length]);

  if (!isAdmin || !overview.data?.platformEnabled || groups.length === 0) return null;

  return (
    <>
      <Button variant="outline" size={size} onClick={() => { setOpen(true); setError(null); }}>Post to WhatsApp group</Button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>Post to a WhatsApp group</DialogTitle></DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="wa-group">Group</Label>
              <select
                id="wa-group" value={groupId} className="h-11 w-full border border-border bg-background px-3 text-[14px]"
                onChange={(e) => { setGroupId(e.target.value); setText(""); if (e.target.value) preview.mutate(e.target.value); }}
              >
                <option value="">Choose a group</option>
                {groups.map((g) => <option key={g.id} value={g.id}>{g.name}{g.communityName ? ` · ${g.communityName}` : ""}</option>)}
              </select>
            </div>

            {preview.isPending && <div className="skeleton h-28 w-full" aria-busy="true" />}

            {text && (
              <div className="space-y-2">
                <Label htmlFor="wa-text">Message (you can edit it)</Label>
                <Textarea id="wa-text" rows={7} value={text} onChange={(e) => setText(e.target.value)} maxLength={1000} />
                <p className="break-all text-xs text-muted-foreground">Link: {link}. It is always included.</p>
              </div>
            )}
            <FormError message={error} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button disabled={!groupId || !text.trim()} isLoading={publish.isPending} loadingText="Posting" onClick={() => publish.mutate()}>Post now</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
