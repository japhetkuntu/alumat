import type { Metadata } from "next";
import { buildEntityMetadata } from "@/lib/entity-og";
import ContributionCampaignDetailClient from "./contribution-campaign-detail-client";

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const { id } = await params;
  return buildEntityMetadata("campaign", id);
}

export default function ContributionCampaignDetailPage() {
  return <ContributionCampaignDetailClient />;
}
