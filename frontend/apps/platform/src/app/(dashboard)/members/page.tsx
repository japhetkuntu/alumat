"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Card } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@alumni/ui";
import { Table, TableBody, TableCell, TableEmpty, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { Button, Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle, Label } from "@alumni/ui";
import { UserAvatar, formatDate, formatDateTime } from "@alumni/ui";
import { getPlatformMembers, updatePlatformMemberProfile, type PlatformMemberItem } from "@/lib/platform-api";
import { useAuth } from "@/hooks/use-auth";
import { toast } from "sonner";
import { handleApiError } from "@/lib/api-client";

const STATUS_OPTIONS = ["All", "Active", "Pending", "Suspended", "Blocked", "Banned"] as const;
const ACTIVITY_OPTIONS = [
  { value: "all", label: "All members" },
  { value: "active", label: "Engaged recently (last 7 days)" },
  { value: "inactive", label: "Needs re-engagement (7+ days, or never)" },
] as const;

export default function PlatformMembersPage() {
  const { isSuperAdmin, role } = useAuth();
  const isSupport = role === "Support";
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<string>("All");
  const [activity, setActivity] = useState<string>("all");
  const [page, setPage] = useState(1);
  const pageSize = 50;
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<PlatformMemberItem | null>(null);
  const [connectionType, setConnectionType] = useState("");
  const [skills, setSkills] = useState("");
  const [interests, setInterests] = useState("");
  const [visibility, setVisibility] = useState({ email: false, phone: false, company: true, bio: true });
  const editMutation = useMutation({
    mutationFn: () => updatePlatformMemberProfile(editing!.id, {
      connectionType: connectionType || undefined,
      skills: skills.split(",").map(v => v.trim()).filter(Boolean),
      interests: interests.split(",").map(v => v.trim()).filter(Boolean),
      showEmailOnDirectory: visibility.email,
      showPhoneOnDirectory: visibility.phone,
      showCompanyOnDirectory: visibility.company,
      showBioOnDirectory: visibility.bio,
    }),
    onSuccess: () => {
      toast.success("Member profile updated");
      setEditing(null);
      queryClient.invalidateQueries({ queryKey: ["platform-members"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

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
        <p className="text-muted-foreground mt-2">Only platform staff can view the member network across institutions.</p>
      </div>
    );
  }

  return (
    <div className="p-5 sm:p-7 max-w-[1500px]">
      <div className="mb-6 max-w-2xl">
        <p className="text-[11px] font-bold uppercase tracking-[0.18em] text-primary mb-2">Member network</p>
        <h1 className="text-[26px] font-bold tracking-tight">Member directory</h1>
        <p className="text-muted-foreground text-[13px] mt-1.5">
          A people-first view of the members who keep every institution connected, from sign-up through ongoing engagement.
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
            <SelectTrigger aria-label="Filter by engagement" className="w-full sm:w-[260px]">
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
        <Table className="min-w-[980px]">
          <TableHeader>
            <TableRow>
              <TableHead>Member</TableHead>
              <TableHead>Institution</TableHead>
              <TableHead>Cohort / year</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Last seen</TableHead>
              <TableHead>Engagement</TableHead>
              <TableHead />
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading && Array.from({ length: 5 }).map((_, index) => (
              <TableRow key={`loading-${index}`} aria-hidden="true">
                <TableCell colSpan={7}><div className="h-10 rounded bg-muted animate-pulse" /></TableCell>
              </TableRow>
            ))}
            {!isLoading && members.length === 0 && <TableEmpty title="No members match these filters" colSpan={7} />}
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
                  <Badge variant={m.isActive ? "success" : "neutral"}>{m.isActive ? "Engaged" : "Needs a nudge"}</Badge>
                </TableCell>
                <TableCell>
                  <Button size="sm" variant="outline" onClick={() => {
                    setEditing(m);
                    setConnectionType(m.connectionType ?? "");
                    setSkills((m.skills ?? []).join(", "));
                    setInterests((m.interests ?? []).join(", "));
                    setVisibility({
                      email: m.showEmailOnDirectory,
                      phone: m.showPhoneOnDirectory,
                      company: m.showCompanyOnDirectory,
                      bio: m.showBioOnDirectory,
                    });
                  }}>Edit community profile</Button>
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
      <Dialog open={!!editing} onOpenChange={(open) => !open && setEditing(null)}>
        <DialogContent>
          <DialogHeader><DialogTitle>Edit member community profile</DialogTitle></DialogHeader>
          <div className="space-y-4">
            <p className="text-[13px] text-muted-foreground">{editing?.firstName} {editing?.lastName} · {editing?.institutionName}</p>
            <div className="space-y-1.5">
              <Label>Connection type</Label>
              <Input value={connectionType} onChange={e => setConnectionType(e.target.value)} placeholder="Member, volunteer, leader…" />
            </div>
            <div className="space-y-1.5">
              <Label>Skills</Label>
              <Input value={skills} onChange={e => setSkills(e.target.value)} placeholder="Comma-separated skills" />
            </div>
            <div className="space-y-1.5">
              <Label>Interests and causes</Label>
              <Input value={interests} onChange={e => setInterests(e.target.value)} placeholder="Comma-separated interests" />
            </div>
            <div className="grid grid-cols-2 gap-3 text-[13px]">
              {[
                ["email", "Show email"],
                ["phone", "Show phone"],
                ["company", "Show company and role"],
                ["bio", "Show bio"],
              ].map(([key, label]) => (
                <label key={key} className="flex items-center gap-2">
                  <input type="checkbox" checked={visibility[key as keyof typeof visibility]} onChange={e => setVisibility(v => ({ ...v, [key]: e.target.checked }))} />
                  {label}
                </label>
              ))}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)}>Cancel</Button>
            <Button onClick={() => editMutation.mutate()} disabled={editMutation.isPending}>{editMutation.isPending ? "Saving…" : "Save profile"}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
