"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { FormError } from "@alumni/ui";
import { getSupportTickets, createSupportTicket } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";

const SEVERITY_OPTIONS = [
  { value: "High", label: "High" },
  { value: "Medium", label: "Medium" },
  { value: "Low", label: "Low" },
];

export default function SupportPage() {
  const queryClient = useQueryClient();
  const { data: tickets = [], isLoading } = useQuery({ queryKey: ["support-tickets"], queryFn: getSupportTickets });

  const [subject, setSubject] = useState("");
  const [severity, setSeverity] = useState("Medium");
  const [message, setMessage] = useState("");
  const [error, setError] = useState<string | null>(null);

  const createMutation = useMutation({
    mutationFn: () => createSupportTicket({ subject, severity, message }),
    onSuccess: () => {
      toast.success("Support ticket submitted");
      queryClient.invalidateQueries({ queryKey: ["support-tickets"] });
      setSubject("");
      setSeverity("Medium");
      setMessage("");
      setError(null);
    },
    onError: (e) => {
      const msg = handleApiError(e);
      setError(msg);
      toast.error(msg);
    },
  });

  return (
    <div className="p-7 max-w-[1200px]">
      <div className="mb-6">
        <h1 className="text-[24px] font-bold">Support</h1>
        <p className="text-muted-foreground text-[13px] mt-1">Report an issue or ask the platform team a question — they'll respond here.</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[1fr_1.3fr] gap-4">
        <Card>
          <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">New ticket</p></div>
          <CardContent className="p-5 space-y-4">
            <div className="space-y-1.5">
              <Label>Subject</Label>
              <Input value={subject} onChange={(e) => setSubject(e.target.value)} placeholder="Briefly describe the issue" />
            </div>
            <div className="space-y-1.5">
              <Label>Severity</Label>
              <FormSelect value={severity} onValueChange={setSeverity} options={SEVERITY_OPTIONS} />
            </div>
            <div className="space-y-1.5">
              <Label>Message</Label>
              <Textarea rows={5} value={message} onChange={(e) => setMessage(e.target.value)} placeholder="What's going on?" />
            </div>
            <FormError message={error} />
            <Button
              className="w-full"
              onClick={() => createMutation.mutate()}
              disabled={createMutation.isPending || !subject.trim() || !message.trim()}
            >
              {createMutation.isPending ? "Submitting…" : "Submit ticket"}
            </Button>
          </CardContent>
        </Card>

        <Card>
          <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Your tickets</p></div>
          <CardContent className="p-0">
            {isLoading && <p className="px-5 py-6 text-[13px] text-muted-foreground">Loading…</p>}
            {!isLoading && tickets.length === 0 && <p className="px-5 py-6 text-[13px] text-muted-foreground">No support tickets yet.</p>}
            {tickets.map((t) => (
              <div key={t.id} className="px-5 py-4 border-b border-border last:border-0">
                <div className="flex justify-between items-start gap-2">
                  <p className="font-semibold text-[13.5px]">{t.subject}</p>
                  <Badge variant={t.severity === "High" ? "destructive" : t.severity === "Medium" ? "warning" : "neutral"}>{t.severity}</Badge>
                </div>
                <p className="text-[12.5px] text-muted-foreground mt-1">{t.status} &middot; {new Date(t.createdAt).toLocaleDateString()}</p>
                <p className="text-[13px] mt-2 leading-relaxed">{t.message}</p>
                {t.internalNote && (
                  <div className="border-t border-border mt-3 pt-3 text-[13px] leading-relaxed">
                    <b>Response from platform team</b><br />{t.internalNote}
                  </div>
                )}
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
