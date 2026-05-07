"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { type ChangeEvent, useState } from "react";
import { ArrowLeft } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { formatDate } from "@/lib/utils";
import { getEventRsvps, reopenRsvp } from "@/lib/admin-api";
import { TableSkeleton } from "@/components/ui/skeleton";
import { toast } from "sonner";

export default function EventRsvpsPage() {
  const { id } = useParams<{ id: string }>();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<"Confirmed" | "Cancelled" | "All">("Confirmed");
  const pageSize = 20;

  const { data, isLoading } = useQuery({
    queryKey: ["admin-event-rsvps", id, page, status],
    queryFn: () => getEventRsvps(id, page, pageSize, status),
    placeholderData: (prev) => prev,
  });

  const rsvps = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-6 lg:p-8 space-y-6 page-enter">
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div className="flex items-center gap-3">
          <Link href="/events">
            <Button size="sm" variant="ghost"><ArrowLeft size={14} />Back to Events</Button>
          </Link>
          <div>
            <h1 className="text-[22px] font-bold tracking-tight">Event RSVPs</h1>
            <p className="text-muted-foreground text-[13px]">{data?.totalCount ?? 0} total registrations</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold text-muted-foreground">Show</span>
          <select
            value={status}
            onChange={(e: ChangeEvent<HTMLSelectElement>) => { setStatus(e.target.value as "Confirmed" | "Cancelled" | "All"); setPage(1); }}
            className="rounded-lg border border-border bg-background px-3 py-2 text-sm"
          >
            <option value="Confirmed">Confirmed</option>
            <option value="Cancelled">Cancelled</option>
            <option value="All">All</option>
          </select>
        </div>
      </div>

      <Card>
        <CardContent className="p-0">
          <div className="px-4 py-3 border-b border-border/50">
            <h2 className="text-base font-semibold">Registrations ({data?.totalCount ?? 0})</h2>
          </div>
          <Table className="min-w-[680px] sm:min-w-[760px]">
            <TableHeader>
              <TableRow>
                <TableHead>Member</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Changed</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableSkeleton rows={5} cols={4} />
              ) : rsvps.length === 0 ? (
                <TableRow><TableCell colSpan={5} className="text-center py-8 text-muted-foreground">No registrations yet</TableCell></TableRow>
              ) : rsvps.map((r) => (
                <TableRow key={r.id}>
                  <TableCell className="font-medium text-sm">
                    {r.memberName ?? "Unknown"}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {r.memberEmail ?? "—"}
                  </TableCell>
                  <TableCell>
                    <Badge variant={r.status === "Confirmed" ? "success" : "secondary"}>{r.status}</Badge>
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {r.status === "Cancelled" ? (r.updatedAt ? formatDate(r.updatedAt) : "—") : formatDate(r.createdAt)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
    </div>
  );
}
