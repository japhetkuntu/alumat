"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Card } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@alumni/ui";
import { Table, TableBody, TableCell, TableEmpty, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { getPlatformMembers } from "@/lib/platform-api";
import { useAuth } from "@/hooks/use-auth";

const STATUS_OPTIONS = ["All", "Active", "Pending", "Suspended", "Blocked", "Banned"] as const;
const ACTIVITY_OPTIONS = [
  { value: "all", label: "All members" },
  { value: "active", label: "Active (logged in, last 7 days)" },
  { value: "inactive", label: "Inactive (7+ days, or never)" },
] as const;

export default function PlatformMembersPage() {
  const { isSuperAdmin, role } = useAuth();
  const isSupport = role === "Support";
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<string>("All");
  const [activity, setActivity] = useState<string>("all");
  const [page, setPage] = useState(1);
  const pageSize = 50;

  const { data, isLoading } = useQuery({
    queryKey: ["platform-members", { page, pageSize, search, status, activity }],
    queryFn: () => getPlatformMembers({
      page, pageSize,
      search: search || undefined,
      status: status === "All" ? undefined : status,
      activeOnly: activity === "all" ? undefined : activity === "active",
    }),
  });

  const members = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  if (!isSuperAdmin && !isSupport) {
    return (
      <div className="p-8 lg:p-12">
        <h1 className="text-2xl font-bold">Unauthorized</h1>
        <p className="text-muted-foreground mt-2">Only SuperAdmin and Support staff can view members across institutions.</p>
      </div>
    );
  }

  return (
    <div className="p-7 max-w-[1500px]">
      <h1 className="text-[24px] font-bold">Members</h1>
      <p className="text-muted-foreground text-[13px] mt-1 mb-6">
        Every alumni member across every institution on the platform — who they are, and whether they&apos;re actually active.
      </p>

      <Card>
        <div className="px-5 py-4 border-b border-border flex flex-wrap items-center gap-3">
          <Input
            placeholder="Search by name or email"
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="max-w-[320px]"
          />
          <Select value={status} onValueChange={(v) => { setStatus(v); setPage(1); }}>
            <SelectTrigger className="w-[160px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {STATUS_OPTIONS.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
            </SelectContent>
          </Select>
          <Select value={activity} onValueChange={(v) => { setActivity(v); setPage(1); }}>
            <SelectTrigger className="w-[260px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {ACTIVITY_OPTIONS.map((o) => <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>)}
            </SelectContent>
          </Select>
          {data && (
            <span className="ml-auto text-[12.5px] text-muted-foreground">{data.totalCount.toLocaleString()} members</span>
          )}
        </div>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Member</TableHead>
              <TableHead>Institution</TableHead>
              <TableHead>Class of</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Last login</TableHead>
              <TableHead>Activity</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {!isLoading && members.length === 0 && <TableEmpty title="No matching members" colSpan={6} />}
            {members.map((m) => (
              <TableRow key={m.id}>
                <TableCell>
                  <p className="font-semibold">{m.firstName} {m.lastName}</p>
                  <p className="text-[12px] text-muted-foreground">{m.email}</p>
                </TableCell>
                <TableCell>{m.institutionName}</TableCell>
                <TableCell className="tabular-nums">{m.graduationYear}</TableCell>
                <TableCell>
                  <Badge variant={m.status === "Active" ? "success" : m.status === "Pending" ? "warning" : "destructive"}>
                    {m.status}
                  </Badge>
                </TableCell>
                <TableCell className="text-[13px]">
                  {m.lastLoginAt ? new Date(m.lastLoginAt).toLocaleString() : "Never"}
                </TableCell>
                <TableCell>
                  <Badge variant={m.isActive ? "success" : "neutral"}>{m.isActive ? "Active" : "Inactive"}</Badge>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <div className="px-5 py-4 border-t border-border">
          <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
        </div>
      </Card>
    </div>
  );
}
