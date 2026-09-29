"use client";

import { useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { Loader2, Send, MessageSquare, Bell, Mail, ImageUpload } from "@alumni/ui";
import { toast } from "sonner";
import { Button } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { cn } from "@alumni/ui";
import {
  getPlatformBroadcastRecipientCount, sendPlatformBroadcast, uploadPlatformImage,
  type PlatformBroadcastFilter, type PlatformEngagementSegment,
} from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

const STATUS_OPTIONS = [
  { value: "", label: "Any status" },
  { value: "Active", label: "Active" },
  { value: "Pending", label: "Pending" },
  { value: "Suspended", label: "Suspended" },
];

const ENGAGEMENT_OPTIONS: { value: PlatformEngagementSegment; label: string; hint: string }[] = [
  { value: "", label: "No engagement filter", hint: "Everyone matching the filters above." },
  { value: "Dormant", label: "Haven't logged in for 60+ days", hint: "Never logged in, or their last login was over 60 days ago — a re-engagement nudge." },
  { value: "NoContributionsEver", label: "Never made a contribution", hint: "No successful payment on record, ever — dues, fundraisers or otherwise." },
  { value: "NoContributionToActiveFundraiser", label: "Haven't given to the open fundraiser(s)", hint: "There's a live fundraiser and this member hasn't contributed to it yet. Empty if nothing is open right now." },
];

/** The platform-side equivalent of the institution admin's own Broadcast page —
 * for Support/SuperAdmin outreach to one institution's members, e.g. nudging
 * inactive members or those who haven't given to a live fundraiser. */
export function InstitutionNotifyTab({ institutionId, institutionSlug, isCommunity }: { institutionId: string; institutionSlug?: string; isCommunity: boolean }) {
  const [title, setTitle] = useState("");
  const [message, setMessage] = useState("");
  const [inApp, setInApp] = useState(true);
  const [sms, setSms] = useState(false);
  const [email, setEmail] = useState(false);
  const [status, setStatus] = useState("");
  const [yearFrom, setYearFrom] = useState("");
  const [yearTo, setYearTo] = useState("");
  const [engagementSegment, setEngagementSegment] = useState<PlatformEngagementSegment>("");
  const [image, setImage] = useState<File | null>(null);
  const [showConfirm, setShowConfirm] = useState(false);

  const filter: PlatformBroadcastFilter = {
    institutionId,
    status: status || undefined,
    graduationYearFrom: yearFrom ? Number(yearFrom) : undefined,
    graduationYearTo: yearTo ? Number(yearTo) : undefined,
    engagementSegment: engagementSegment || undefined,
  };

  const { data: recipientCount, isFetching: countLoading } = useQuery({
    queryKey: ["platform-broadcast-recipient-count", filter],
    queryFn: () => getPlatformBroadcastRecipientCount(filter),
    placeholderData: (prev) => prev,
  });

  const sendMut = useMutation({
    mutationFn: async () => {
      const imageUrl = image ? await uploadPlatformImage(image, institutionSlug) : undefined;
      const channels = [inApp && "InApp", sms && "Sms", email && "Email"].filter(Boolean) as string[];
      return sendPlatformBroadcast({ title: title.trim() || undefined, message: message.trim(), channels, imageUrl, ...filter });
    },
    onSuccess: (result) => {
      setShowConfirm(false);
      setTitle("");
      setMessage("");
      setImage(null);
      toast.success(`Sent to ${result.recipientCount.toLocaleString()} member${result.recipientCount === 1 ? "" : "s"}`);
    },
    onError: (e) => {
      setShowConfirm(false);
      toast.error(handleApiError(e));
    },
  });

  const channelsSelected = inApp || sms || email;
  const canSend = message.trim().length > 0 && channelsSelected;

  return (
    <div className="max-w-[720px] space-y-5">
      <p className="text-muted-foreground text-[13px]">
        Send a targeted notification to this institution&apos;s own members — a support nudge, a re-engagement push
        to members who&apos;ve gone quiet, or a heads-up about a fundraiser, via SMS, email and/or in-app notification.
      </p>

      <div className="space-y-1.5">
        <Label>Title (optional)</Label>
        <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. We miss you!" maxLength={100} />
      </div>

      <div className="space-y-1.5">
        <Label>Message</Label>
        <Textarea
          rows={5}
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          placeholder="Write the message exactly as it should appear to members..."
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
              "flex items-center gap-2 px-3 py-2 border text-[12.5px] font-semibold transition-colors",
              inApp ? "bg-primary/10 text-primary border-blue-300" : "bg-white text-foreground border-border hover:bg-muted"
            )}
          >
            <Bell size={14} />In-app notification
          </button>
          <button
            type="button"
            onClick={() => setSms((v) => !v)}
            className={cn(
              "flex items-center gap-2 px-3 py-2 border text-[12.5px] font-semibold transition-colors",
              sms ? "bg-primary/10 text-primary border-blue-300" : "bg-white text-foreground border-border hover:bg-muted"
            )}
          >
            <MessageSquare size={14} />SMS
          </button>
          <button
            type="button"
            onClick={() => setEmail((v) => !v)}
            className={cn(
              "flex items-center gap-2 px-3 py-2 border text-[12.5px] font-semibold transition-colors",
              email ? "bg-primary/10 text-primary border-blue-300" : "bg-white text-foreground border-border hover:bg-muted"
            )}
          >
            <Mail size={14} />Email
          </button>
        </div>
        <p className="text-[11px] text-muted-foreground">
          If the institution has SMS or email notifications turned off, that channel is silently skipped server-side.
        </p>
      </div>

      <div className="space-y-2">
        <Label>Image (optional)</Label>
        <ImageUpload file={image} onChange={setImage} label="Upload an image for this notification" />
        <p className="text-[11px] text-muted-foreground">
          Shown in the in-app notification panel, and as a banner in the email if Email is selected.
        </p>
      </div>

      <div className="space-y-2">
        <Label>Audience filter</Label>
        <div className={cn("grid grid-cols-1 gap-3", isCommunity ? "sm:grid-cols-1" : "sm:grid-cols-3")}>
          <div className="space-y-1.5">
            <Label className="text-[11px] text-muted-foreground font-normal">Status</Label>
            <FormSelect value={status} onValueChange={setStatus} options={STATUS_OPTIONS} placeholder="Any status" />
          </div>
          {!isCommunity && (
            <>
              <div className="space-y-1.5">
                <Label className="text-[11px] text-muted-foreground font-normal">Graduation year from</Label>
                <Input type="number" value={yearFrom} onChange={(e) => setYearFrom(e.target.value)} placeholder="e.g. 1980" />
              </div>
              <div className="space-y-1.5">
                <Label className="text-[11px] text-muted-foreground font-normal">Graduation year to</Label>
                <Input type="number" value={yearTo} onChange={(e) => setYearTo(e.target.value)} placeholder="e.g. 1995" />
              </div>
            </>
          )}
        </div>
      </div>

      <div className="space-y-2">
        <Label>Engagement segment</Label>
        <div className="space-y-1.5">
          {ENGAGEMENT_OPTIONS.map((opt) => (
            <label
              key={opt.value}
              className={cn(
                "flex items-start gap-2.5 px-3 py-2.5 border cursor-pointer transition-colors",
                engagementSegment === opt.value ? "bg-primary/10 border-blue-300" : "border-border hover:bg-muted"
              )}
            >
              <input
                type="radio"
                name="platform-engagement-segment"
                className="mt-0.5"
                checked={engagementSegment === opt.value}
                onChange={() => setEngagementSegment(opt.value)}
              />
              <span>
                <span className="block text-[13px] font-semibold">{opt.label}</span>
                <span className="block text-[11.5px] text-muted-foreground mt-0.5">{opt.hint}</span>
              </span>
            </label>
          ))}
        </div>
      </div>

      <div className="flex items-center justify-between pt-2 border-t border-border">
        <p className="text-[13px] text-muted-foreground">
          {countLoading ? (
            <span className="inline-flex items-center gap-1.5"><Loader2 size={13} className="animate-spin" />Estimating recipients…</span>
          ) : (
            <>
              <b className="text-foreground">{(recipientCount ?? 0).toLocaleString()}</b> member{recipientCount === 1 ? "" : "s"} will receive this
            </>
          )}
        </p>
        <Button disabled={!canSend} onClick={() => setShowConfirm(true)}>
          <Send size={14} />Send
        </Button>
      </div>

      <ConfirmModal
        open={showConfirm}
        title="Send Notification"
        message={`This will send "${message.trim().slice(0, 80)}${message.trim().length > 80 ? "…" : ""}" to ${(recipientCount ?? 0).toLocaleString()} member(s) via ${[inApp && "in-app", sms && "SMS", email && "email"].filter(Boolean).join(" and ")}. Continue?`}
        confirmLabel="Send"
        variant="destructive"
        isLoading={sendMut.isPending}
        onConfirm={() => sendMut.mutate()}
        onCancel={() => setShowConfirm(false)}
      />
    </div>
  );
}
