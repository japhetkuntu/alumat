import type { Metadata } from "next";
import { buildEntityMetadata } from "@/lib/entity-og";
import JobDetailClient from "./job-detail-client";

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const { id } = await params;
  return buildEntityMetadata("job", id);
}

export default function JobDetailPage() {
  return <JobDetailClient />;
}
