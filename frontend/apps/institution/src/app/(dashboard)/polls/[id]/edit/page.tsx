"use client";

import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { CardSkeleton, LoadError } from "@alumni/ui";
import { getPoll } from "@/lib/institution-api";
import { PollEditor } from "@/components/institution/poll-editor";

export default function EditPollPage() {
  const { id } = useParams<{ id: string }>();
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["poll", id], queryFn: () => getPoll(id) });
  if (isLoading) return <div className="mx-auto max-w-[900px] space-y-4 p-4 sm:p-[26px]"><CardSkeleton /><CardSkeleton /></div>;
  if (isError || !data) return <div className="mx-auto max-w-[900px] p-4 sm:p-[26px]"><LoadError onRetry={() => refetch()} /></div>;
  // Keyed on the poll so the form starts from what was saved, not from an earlier visit.
  return <PollEditor key={data.id + (data.locked ? "l" : "")} poll={data} />;
}
