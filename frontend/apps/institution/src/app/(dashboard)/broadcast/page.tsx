"use client";

import { useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { Loader2, Send, MessageSquare, Bell } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { getBroadcastRecipientCount, sendBroadcast, type BroadcastFilter } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";

const STATUS_OPTIONS = [
  { value: "", label: "Any status" },
  { value: "Active", label: "Active" },
  { value: "Pending", label: "Pending" },
  { value: "Suspended", label: "Suspended" },
];

export default function BroadcastPage() {
  const [title, setTitle] = useState("");
  const [message, setMessage] = useState("");
  const [inApp, setInApp] = useState(true);
  const [sms, setSms] = useState(true);
  const [status, setStatus] = useState("");
  const [yearFrom, setYearFrom] = useState("");
  const [yearTo, setYearTo] = useState("");
  const [showConfirm, setShowConfirm] = useState(false);

  const filter: BroadcastFilter = {
    status: status || undefined,
    graduationYearFrom: yearFrom ? Number(yearFrom) : undefined,
    graduationYearTo: yearTo ? Number(yearTo) : undefined,
  };

  const { data: recipientCount, isFetching: countLoading } = useQuery({
    queryKey: ["broadcast-recipient-count", filter],
    queryFn: () => getBroadcastRecipientCount(filter),
    placeholderData: (prev) => prev,
  });

  const sendMut = useMutation({
    mutationFn: () => {
      const channels = [inApp && "InApp", sms && "Sms"].filter(Boolean) as string[];
      return sendBroadcast({ title: title.trim() || undefined, message: message.trim(), channels, filter });
    },
    onSuccess: (result) => {
      setShowConfirm(false);
      setTitle("");
      setMessage("");
      toast.success(`Broadcast sent to ${result.recipientCount.toLocaleString()} member${result.recipientCount === 1 ? "" : "s"}`);
    },
    onError: (e) => {
      setShowConfirm(false);
      toast.error(handleApiError(e));
    },
  });

  const channelsSelected = inApp || sms;
  const canSend = message.trim().length > 0 && channelsSelected;

  return (
    <div className="p-4 sm:p-[26px] max-w-[820px] mx-auto">
      <div>
        <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Broadcast</h1>
        <p className="text-muted-foreground text-[13px] mt-1.5">
          Send an urgent announcement (e.g. a funeral notice or school emergency) to a filtered group of members via SMS and/or in-app notification.
        </p>
      </div>

      <Card className="mt-5">
        <CardContent className="p-5 space-y-5">
          <div className="space-y-1.5">
            <Label>Title (optional)</Label>
            <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. Important Notice" maxLength={100} />
          </div>

          <div className="space-y-1.5">
            <Label>Message</Label>
            <Textarea
              rows={5}
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              placeholder="Write the announcement exactly as it should appear to members..."
            />
            <p className="text-[11px] text-muted-foreground">{message.length} characters</p>
          </div>

          <div className="space-y-2">
            <Label>Channels</Label>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => setInApp((v) => !v)}
                className={cn(
                  "flex items-center gap-2 px-3 py-2 rounded-full border text-[12.5px] font-semibold transition-colors",
                  inApp ? "bg-primary/10 text-primary border-blue-300" : "bg-white text-foreground border-border hover:bg-muted"
                )}
              >
                <Bell size={14} />In-app notification
              </button>
              <button
                type="button"
                onClick={() => setSms((v) => !v)}
                className={cn(
                  "flex items-center gap-2 px-3 py-2 rounded-full border text-[12.5px] font-semibold transition-colors",
                  sms ? "bg-primary/10 text-primary border-blue-300" : "bg-white text-foreground border-border hover:bg-muted"
                )}
              >
                <MessageSquare size={14} />SMS
              </button>
              <button
                type="button"
                disabled
                title="Coming soon"
                className="flex items-center gap-2 px-3 py-2 rounded-full border text-[12.5px] font-semibold opacity-50 cursor-not-allowed bg-white text-muted-foreground border-border"
              >
                <MessageSquare size={14} />WhatsApp (coming soon)
              </button>
            </div>
            <p className="text-[11px] text-muted-foreground">
              SMS is sent to every matching member with a phone number on file, regardless of their individual SMS notification preference — this is intentional for urgent, time-sensitive announcements.
            </p>
          </div>

          <div className="space-y-2">
            <Label>Audience filter</Label>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div className="space-y-1.5">
                <Label className="text-[11px] text-muted-foreground font-normal">Status</Label>
                <FormSelect value={status} onValueChange={setStatus} options={STATUS_OPTIONS} placeholder="Any status" />
              </div>
              <div className="space-y-1.5">
                <Label className="text-[11px] text-muted-foreground font-normal">Graduation year from</Label>
                <Input type="number" value={yearFrom} onChange={(e) => setYearFrom(e.target.value)} placeholder="e.g. 1980" />
              </div>
              <div className="space-y-1.5">
                <Label className="text-[11px] text-muted-foreground font-normal">Graduation year to</Label>
                <Input type="number" value={yearTo} onChange={(e) => setYearTo(e.target.value)} placeholder="e.g. 1995" />
              </div>
            </div>
          </div>

          <div className="flex items-center justify-between pt-2 border-t border-border">
            <p className="text-[13px] text-muted-foreground">
              {countLoading ? (
                <span className="inline-flex items-center gap-1.5"><Loader2 size={13} className="animate-spin" />Estimating recipients…</span>
              ) : (
                <>
                  <b className="text-foreground">{(recipientCount ?? 0).toLocaleString()}</b> member{recipientCount === 1 ? "" : "s"} will receive this broadcast
                </>
              )}
            </p>
            <Button disabled={!canSend} onClick={() => setShowConfirm(true)}>
              <Send size={14} />Send broadcast
            </Button>
          </div>
        </CardContent>
      </Card>

      <ConfirmModal
        open={showConfirm}
        title="Send Broadcast"
        message={`This will send "${message.trim().slice(0, 80)}${message.trim().length > 80 ? "…" : ""}" to ${(recipientCount ?? 0).toLocaleString()} member(s) via ${[inApp && "in-app", sms && "SMS"].filter(Boolean).join(" and ")}. This cannot be undone. Continue?`}
        confirmLabel="Send Broadcast"
        variant="destructive"
        isLoading={sendMut.isPending}
        onConfirm={() => sendMut.mutate()}
        onCancel={() => setShowConfirm(false)}
      />
    </div>
  );
}
