"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { getAnnouncements, sendAnnouncement } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

export default function AnnouncementsPage() {
  const queryClient = useQueryClient();
  const { data: items = [] } = useQuery({ queryKey: ["announcements"], queryFn: getAnnouncements });
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");

  const sendMutation = useMutation({
    mutationFn: () => sendAnnouncement({ title, body, audience: "All institutions" }),
    onSuccess: () => {
      toast.success("Announcement sent to all institutions.");
      queryClient.invalidateQueries({ queryKey: ["announcements"] });
      setTitle("");
      setBody("");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function send() {
    if (!title.trim() || !body.trim()) {
      toast.error("Add a title and message before sending.");
      return;
    }
    sendMutation.mutate();
  }

  return (
    <div className="p-7 max-w-[1500px]">
      <h1 className="text-[24px] font-bold">Announcements</h1>
      <p className="text-muted-foreground text-[13px] mt-1 mb-6">Broadcast a message to some or all institutions.</p>

      <div className="grid grid-cols-1 lg:grid-cols-[1fr_1.2fr] gap-4">
        <Card>
          <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Compose</p></div>
          <CardContent className="p-5 space-y-4">
            <div className="space-y-1.5">
              <Label>Title</Label>
              <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Scheduled maintenance" />
            </div>
            <div className="space-y-1.5">
              <Label>Message</Label>
              <Textarea value={body} onChange={(e) => setBody(e.target.value)} rows={5} placeholder="What should institutions know?" />
            </div>
            <div className="space-y-1.5">
              <Label>Audience</Label>
              <Input value="All institutions" disabled />
            </div>
            <Button onClick={send} className="w-full" disabled={sendMutation.isPending}>
              {sendMutation.isPending ? "Sending…" : "Send announcement"}
            </Button>
          </CardContent>
        </Card>

        <Card>
          <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">History</p></div>
          <CardContent className="p-0">
            {items.length === 0 && <p className="px-5 py-6 text-[13px] text-muted-foreground">No announcements sent yet.</p>}
            {items.map((a) => (
              <div key={a.id} className="px-5 py-4 border-b border-border last:border-0">
                <p className="font-semibold text-[13.5px]">{a.title}</p>
                <p className="text-[13px] text-muted-foreground mt-1">{a.body}</p>
                <p className="text-[12px] text-muted-foreground mt-2">
                  {a.audience} &middot; {new Date(a.sentAt).toLocaleString()} &middot; seen by {a.seenByAdmins}/{a.totalAdmins} admins
                </p>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
