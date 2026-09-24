import type { Metadata } from "next";
import { buildEntityMetadata } from "@/lib/entity-og";
import AlbumDetailClient from "./album-detail-client";

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const { id } = await params;
  return buildEntityMetadata("album", id);
}

export default function AlbumDetailPage() {
  return <AlbumDetailClient />;
}
