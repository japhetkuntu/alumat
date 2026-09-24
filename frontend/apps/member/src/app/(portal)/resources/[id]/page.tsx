import type { Metadata } from "next";
import { buildEntityMetadata } from "@/lib/entity-og";
import ResourceDetailClient from "./resource-detail-client";

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const { id } = await params;
  return buildEntityMetadata("resource", id);
}

export default function ResourceDetailPage() {
  return <ResourceDetailClient />;
}
