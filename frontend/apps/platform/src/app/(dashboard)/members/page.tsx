"use client";

import { LoadError } from "@alumni/ui";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Card } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@alumni/ui";
import { Table, TableBody, TableCell, TableEmpty, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { InfoTip } from "@alumni/ui";
import { UserAvatar, formatDate, formatDateTime } from "@alumni/ui";
import { getPlatformMembers } from "@/lib/platform-api";
import { useAuth } from "@/hooks/use-auth";

const STATUS_OPTIONS = ["All", "Active", "Pending", "Suspended", "Blocked", "Banned"] as const;
const ACTIVITY_OPTIONS = [
  { value: "all", label: "All members" },
  { value: "active", label: "Active (last 7 days)" },
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
  const { data, isLoading, isError, refetch } = useQuery({
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
        <p className="text-muted-foreground mt-2">Only platform staff can view the member network across institutions.</p>
      </div>
    );
  }

  return (
    <div className="p-5 sm:p-7 max-w-[1500px]">
      <div className="mb-6 max-w-2xl">
                <h1 className="text-[26px] font-bold tracking-tight">Member directory</h1>
        <p className="text-muted-foreground text-[13px] mt-1.5">
          Every member across all institutions, and whether they have signed in lately.
        </p>
      </div>

      <Card>
        <div className="px-5 py-4 border-b border-border flex flex-col gap-4 lg:flex-row lg:items-center">
          <div className="flex flex-wrap items-center gap-3 flex-1">
          <Input
            aria-label="Search members by name or email"
            placeholder="Search members by name or email"
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="w-full sm:max-w-[320px]"
          />
          <Select value={status} onValueChange={(v) => { setStatus(v); setPage(1); }}>
            <SelectTrigger aria-label="Filter by member status" className="w-[160px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {STATUS_OPTIONS.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
            </SelectContent>
          </Select>
          <Select value={activity} onValueChange={(v) => { setActivity(v); setPage(1); }}>
            <SelectTrigger aria-label="Filter by activity" className="w-full sm:w-[260px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {ACTIVITY_OPTIONS.map((o) => <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>)}
            </SelectContent>
          </Select>
          </div>
          {data && (
            <span className="text-[12.5px] text-muted-foreground lg:ml-auto">
            <span className="font-semibold text-foreground">{data.totalCount.toLocaleString()}</span> members in view
            </span>
          )}
        </div>
        <div className="overflow-x-auto">
        <Table stackOnMobile className="min-w-[980px]">
          <TableHeader>
            <TableRow>
              <TableHead>Member</TableHead>
              <TableHead>Institution</TableHead>
              <TableHead>Graduation year</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Last sign-in</TableHead>
              <TableHead>
                <span className="inline-flex items-center gap-1.5">Activity<InfoTip text="Active means signed in within the last 7 days." /></span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading && Array.from({ length: 5 }).map((_, index) => (
              <TableRow key={`loading-${index}`} aria-hidden="true">
                <TableCell colSpan={6}><div className="h-10 rounded bg-muted animate-pulse" /></TableCell>
              </TableRow>
            ))}
            {isError && <tr><td colSpan={6}><LoadError className="py-10" onRetry={() => refetch()} /></td></tr>}
            {!isLoading && !isError && members.length === 0 && <TableEmpty title="No members match these filters" colSpan={6} />}
            {members.map((m) => (
              <TableRow key={m.id}>
                <TableCell>
                  <div className="flex items-center gap-3">
                    <UserAvatar name={`${m.firstName} ${m.lastName}`} size="sm" />
                    <div className="min-w-0">
                      <p className="font-semibold truncate">{m.firstName} {m.lastName}</p>
                      <p className="text-[12px] text-muted-foreground truncate">{m.email}</p>
                    </div>
                  </div>
                </TableCell>
                <TableCell>{m.institutionName}</TableCell>
                <TableCell className="tabular-nums">{m.organizationType === "Alumni" ? m.graduationYear || "—" : "—"}</TableCell>
                <TableCell>
                  <Badge variant={m.status === "Active" ? "success" : m.status === "Pending" ? "warning" : "destructive"}>
                    {m.status}
                  </Badge>
                </TableCell>
                <TableCell className="text-[13px] whitespace-nowrap">
                  {m.lastLoginAt ? (
                    <time dateTime={m.lastLoginAt} title={formatDateTime(m.lastLoginAt)}>{formatDate(m.lastLoginAt)}</time>
                  ) : "Not yet"}
                </TableCell>
                <TableCell>
                  <Badge variant={m.isActive ? "success" : "neutral"}>{m.isActive ? "Active" : "Inactive"}</Badge>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        </div>
        <div className="px-5 py-4 border-t border-border">
          <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
        </div>
      </Card>
    </div>
  );
}
