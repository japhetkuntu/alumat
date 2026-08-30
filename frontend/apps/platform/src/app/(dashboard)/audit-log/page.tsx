"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Card } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Table, TableBody, TableCell, TableEmpty, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { getAuditLog } from "@/lib/platform-api";
import { useAuth } from "@/hooks/use-auth";

export default function AuditLogPage() {
  const { isSuperAdmin } = useAuth();
  const [search, setSearch] = useState("");
  const { data } = useQuery({
    queryKey: ["audit-log", { page: 1, pageSize: 100, search }],
    queryFn: () => getAuditLog({ page: 1, pageSize: 100, search: search || undefined }),
  });
  const entries = data?.results ?? [];

  if (!isSuperAdmin) {
    return (
      <div className="p-8 lg:p-12">
        <h1 className="text-2xl font-bold">Unauthorized</h1>
        <p className="text-muted-foreground mt-2">Only SuperAdmin users can access the audit log.</p>
      </div>
    );
  }

  return (
    <div className="p-7 max-w-[1500px]">
      <h1 className="text-[24px] font-bold">Audit Log</h1>
      <p className="text-muted-foreground text-[13px] mt-1 mb-6">Platform-wide accountability trail — every sensitive action taken by platform staff.</p>

      <Card>
        <div className="px-5 py-4 border-b border-border">
          <Input placeholder="Search by actor, action, or target" value={search} onChange={(e) => setSearch(e.target.value)} className="max-w-[380px]" />
        </div>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Actor</TableHead>
              <TableHead>Action</TableHead>
              <TableHead>Target</TableHead>
              <TableHead>Timestamp</TableHead>
              <TableHead>Reference</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {entries.length === 0 && <TableEmpty title="No matching events" colSpan={5} />}
            {entries.map((a) => (
              <TableRow key={a.id}>
                <TableCell className="font-semibold">{a.actor}</TableCell>
                <TableCell>{a.action}</TableCell>
                <TableCell>{a.target}</TableCell>
                <TableCell>{new Date(a.timestamp).toLocaleString()}</TableCell>
                <TableCell className="font-mono text-[12px] text-muted-foreground">{a.id}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
    </div>
  );
}
