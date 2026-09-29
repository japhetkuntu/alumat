"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Check, Copy, MessageSquare, Share2 } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { getInstitutionProfile } from "@/lib/institution-api";

/**
 * The single highest-leverage fix for "members are reluctant to sign up": admins currently
 * have to write their own pitch from scratch and drop it into the WhatsApp group they already
 * use — most don't bother, and what little gets said is generic ("check out our new
 * platform!"), which gives a member nothing concrete to react to. This hands them a message
 * that's both ready to send AND specific — real, live numbers for their own institution
 * ("47 members, 3 events this month") rather than a vague pitch — one tap from being pasted
 * into that same WhatsApp group. See also the standing "why fight WhatsApp" positioning on
 * the member marketing site (why-not-whatsapp/page.tsx): the fix here is the same instinct
 * applied to distribution, not just messaging — meet admins in the channel they already use
 * instead of asking them to run a "campaign."
 */
export function InviteKitCard({
  totalMembers, upcomingEvents, activeCampaignCount,
}: {
  totalMembers: number;
  upcomingEvents: number;
  activeCampaignCount: number;
}) {
  const { data: profile } = useQuery({
    queryKey: ["institution-profile"],
    queryFn: getInstitutionProfile,
    staleTime: 60 * 1000,
  });

  const brandName = profile?.portalName || profile?.name || "your community";
  const link = profile?.memberPortalUrl || "";

  const [message, setMessage] = useState("");
  const [edited, setEdited] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (edited || !profile) return;
    const bits: string[] = [];
    if (totalMembers > 0) bits.push(`${totalMembers} member${totalMembers === 1 ? "" : "s"} already here`);
    if (activeCampaignCount > 0) bits.push(`${activeCampaignCount} active fundraiser${activeCampaignCount === 1 ? "" : "s"}`);
    if (upcomingEvents > 0) bits.push(`${upcomingEvents} upcoming event${upcomingEvents === 1 ? "" : "s"}`);
    const stats = bits.length > 0 ? ` ${bits.join(", ")} — ` : " ";
    setMessage(
      `${brandName} is now on AlumUnion!${stats}Join us: ${link}`.trim()
    );
    // Only re-derive the default message while the admin hasn't started editing it themselves,
    // and once the real numbers/link have actually loaded (not the empty placeholders).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [profile, totalMembers, upcomingEvents, activeCampaignCount]);

  if (!profile) return null;

  const whatsappUrl = `https://wa.me/?text=${encodeURIComponent(message)}`;
  const qrUrl = link ? `https://api.qrserver.com/v1/create-qr-code/?size=180x180&data=${encodeURIComponent(link)}` : null;

  const copyMessage = async () => {
    try {
      await navigator.clipboard.writeText(message);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard access can fail (permissions, non-secure context) — the message is still
      // visible and selectable in the textarea, so this never blocks the admin from copying
      // it by hand.
    }
  };

  return (
    <div className="card p-[18px] mt-3.5">
      <div className="flex items-start gap-2.5 mb-3">
        <div className="w-8 h-8 rounded-lg bg-primary/10 flex items-center justify-center shrink-0">
          <Share2 size={16} className="text-primary" />
        </div>
        <div>
          <p className="text-[14px] font-semibold">Invite members</p>
          <p className="text-[12px] text-muted-foreground mt-0.5">
            Word of mouth is still how most members find this — here&apos;s a ready-made message with real numbers, for wherever you&apos;d normally mention it.
          </p>
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-[1fr_auto] gap-4">
        <div className="space-y-2">
          <Textarea
            rows={3}
            value={message}
            onChange={(e) => { setMessage(e.target.value); setEdited(true); }}
            className="text-[13px]"
          />
          <div className="flex flex-wrap gap-2">
            <Button
              size="sm"
              className="font-semibold text-[12.5px] gap-1.5"
              onClick={() => window.open(whatsappUrl, "_blank", "noopener,noreferrer")}
            >
              <MessageSquare size={13} />Share to WhatsApp
            </Button>
            <Button variant="outline" size="sm" onClick={copyMessage} className="font-semibold text-[12.5px] gap-1.5">
              {copied ? <Check size={13} /> : <Copy size={13} />}
              {copied ? "Copied" : "Copy message"}
            </Button>
          </div>
        </div>

        {qrUrl && (
          <div className="flex flex-col items-center gap-1.5 shrink-0">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={qrUrl} alt="QR code to the member portal" width={100} height={100} className="border border-border" />
            <p className="text-[10.5px] text-muted-foreground text-center max-w-[100px]">For posters or events</p>
          </div>
        )}
      </div>
    </div>
  );
}
