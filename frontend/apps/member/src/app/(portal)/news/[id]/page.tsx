"use client";

import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Calendar } from "lucide-react";
import Link from "next/link";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { CardSkeleton } from "@/components/ui/skeleton";
import { RichTextViewer } from "@/components/ui/rich-text-editor";
import { MediaGallery } from "@/components/ui/media-gallery";
import { formatDate } from "@/lib/utils";
import { getNewsPost } from "@/lib/member-api";

export default function NewsDetailPage() {
  const { id } = useParams<{ id: string }>();

  const { data: post, isLoading } = useQuery({
    queryKey: ["m-news", id],
    queryFn: () => getNewsPost(id),
  });

  if (isLoading) return <div className="p-6 space-y-4"><CardSkeleton /><CardSkeleton /></div>;
  if (!post) return <div className="p-6 text-muted-foreground">Post not found.</div>;

  return (
    <div className="p-6 lg:p-8 max-w-3xl mx-auto space-y-6 page-enter">
      <Link href="/news">
        <Button size="sm" variant="ghost"><ArrowLeft size={14} />Back to News</Button>
      </Link>

      <Card>
        <CardHeader>
          <div className="flex items-center gap-2 flex-wrap mb-2">
            <Badge variant="secondary">{post.category}</Badge>
            {post.isPinned && <Badge>Pinned</Badge>}
          </div>
          <CardTitle className="text-2xl tracking-tight">{post.title}</CardTitle>
          <div className="flex items-center gap-2 text-sm text-muted-foreground mt-1">
            <Calendar size={14} />
            <span>{formatDate(post.publishedAt ?? post.createdAt)}</span>
            {post.authorName && <span>· {post.authorName}</span>}
          </div>
        </CardHeader>
        <CardContent>
          <MediaGallery imageUrls={post.imageUrls} youtubeUrls={post.youtubeVideoUrls} className="mb-6" />

          <RichTextViewer content={post.content} />
        </CardContent>
      </Card>
    </div>
  );
}
