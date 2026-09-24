import type { Metadata } from "next";
import { buildEntityMetadata } from "@/lib/entity-og";
import CampaignDetailClient from "./campaign-detail-client";

export async function generateMetadata({ params }: { params: Promise<{ campaignId: string }> }): Promise<Metadata> {
  const { campaignId } = await params;
  return buildEntityMetadata("campaign", campaignId);
}

export default function PaymentCampaignPage() {
  return <CampaignDetailClient />;
}
