import type { Metadata } from "next";
import { buildEntityMetadata } from "@/lib/entity-og";
import BusinessDetailClient from "./business-detail-client";

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const { id } = await params;
  return buildEntityMetadata("business", id);
}

export default function BusinessDetailPage() {
  return <BusinessDetailClient />;
}
