import type { Metadata } from "next";
import { buildEntityMetadata } from "@/lib/entity-og";
import NewsDetailClient from "./news-detail-client";

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const { id } = await params;
  return buildEntityMetadata("news", id);
}

export default function NewsDetailPage() {
  return <NewsDetailClient />;
}
