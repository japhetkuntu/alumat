import type { Metadata } from "next";
import { buildEntityMetadata } from "@/lib/entity-og";
import CommunityDetailClient from "./community-detail-client";

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const { id } = await params;
  return buildEntityMetadata("community", id);
}

export default function CommunityDetailPage() {
  return <CommunityDetailClient />;
}
