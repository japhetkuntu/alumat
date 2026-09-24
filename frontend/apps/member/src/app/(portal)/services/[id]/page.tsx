import type { Metadata } from "next";
import { buildEntityMetadata } from "@/lib/entity-og";
import ServiceDetailClient from "./service-detail-client";

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const { id } = await params;
  return buildEntityMetadata("service", id);
}

export default function ServiceDetailPage() {
  return <ServiceDetailClient />;
}
