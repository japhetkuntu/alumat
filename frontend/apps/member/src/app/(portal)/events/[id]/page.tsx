import type { Metadata } from "next";
import { buildEntityMetadata } from "@/lib/entity-og";
import EventDetailClient from "./event-detail-client";

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const { id } = await params;
  return buildEntityMetadata("event", id);
}

export default function EventDetailPage() {
  return <EventDetailClient />;
}
