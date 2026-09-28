"use client";

import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { ShareLinkButton, type ShareLinkButtonProps } from "@alumni/ui";
import { getInstitutionProfile } from "@/lib/institution-api";
import { buildMemberPortalShareUrl } from "@/lib/member-portal-share";

interface MemberShareButtonProps extends Omit<ShareLinkButtonProps, "url" | "onSuccess" | "onError"> {
  /** The item's page in the MEMBER portal, e.g. "/events/abc123". Never an admin-portal path. */
  memberPath: string;
}

/**
 * "Share" for an administrator. The link always opens the item's page in the member portal (where members can RSVP,
 * give, read and so on), never the admin console, so it can be pasted into WhatsApp, social media or an email. Those
 * member pages also carry a link preview (title, description and picture) for chat apps.
 */
export function MemberShareButton({ memberPath, title, variant = "outline", size = "sm", ...props }: MemberShareButtonProps) {
  const { data: institution } = useQuery({
    queryKey: ["institution-profile"],
    queryFn: getInstitutionProfile,
    staleTime: 60 * 1000,
  });
  const configured = !!institution?.memberPortalUrl;

  return (
    <ShareLinkButton
      {...props}
      url={buildMemberPortalShareUrl(memberPath, institution?.memberPortalUrl)}
      title={title}
      variant={variant}
      size={size}
      onSuccess={(result) => {
        toast.success(result === "shared" ? "Share sheet opened" : "Member portal link copied");
        if (!configured) {
          toast.warning("Member portal URL is not configured, so this shared the admin address instead, which members can't open. Set it up in institution settings.");
        }
      }}
      onError={(message) => {
        if (!configured) toast.warning("Member portal URL is not configured, so this uses the current address as a fallback.");
        toast.error(message);
      }}
    />
  );
}
