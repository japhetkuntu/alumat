"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Card, Badge, LoadError, formatDate, formatDateTime } from "@alumni/ui";
import { getPlatformMember } from "@/lib/platform-api";
import { useAuth } from "@/hooks/use-auth";
import { ViewableAvatar } from "@/components/platform/viewable-avatar";

function Field({ label, children }: { label: string; children?: React.ReactNode }) {
  const empty = children === null || children === undefined || children === "";
  return (
    <div className="min-w-0">
      <dt className="text-[11.5px] font-semibold uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-1 break-words text-[14px]">{empty ? <span className="text-muted-foreground">—</span> : children}</dd>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card className="p-5">
      <h2 className="mb-4 text-[15px] font-semibold">{title}</h2>
      <dl className="grid grid-cols-1 gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">{children}</dl>
    </Card>
  );
}

const when = (v?: string | null) => (v ? <time dateTime={v} title={formatDateTime(v)}>{formatDate(v)}</time> : null);

export default function PlatformMemberDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { isSuperAdmin, role } = useAuth();
  const allowed = isSuperAdmin || role === "Support";
  const { data: m, isLoading, isError, refetch } = useQuery({ queryKey: ["platform-member", id], queryFn: () => getPlatformMember(id), enabled: allowed });

  if (!allowed) return <div className="p-8"><h1 className="text-2xl font-bold">Unauthorized</h1></div>;
  const name = m ? `${m.firstName} ${m.lastName}` : "";

  return (
    <div className="max-w-[1100px] space-y-5 p-5 sm:p-7">
      <Link href="/members" className="text-[13px] text-muted-foreground hover:text-foreground">← Member directory</Link>
      {isLoading && <div className="h-40 animate-pulse rounded bg-muted" />}
      {isError && <LoadError className="py-10" onRetry={() => refetch()} />}
      {m && (
        <>
          <Card className="flex flex-col gap-5 p-5 sm:flex-row sm:items-center">
            <ViewableAvatar src={m.profilePictureUrl} name={name} caption={m.email} size="xl" />
            <div className="min-w-0 flex-1">
              <h1 className="break-words text-[22px] font-bold leading-tight sm:text-[26px]">{name}</h1>
              <p className="mt-1 break-all text-[13.5px] text-muted-foreground">{m.email}</p>
              <div className="mt-3 flex flex-wrap gap-2">
                <Badge variant={m.status === "Active" ? "success" : m.status === "Pending" ? "warning" : "destructive"}>{m.status}</Badge>
                <Badge variant={m.isActive ? "success" : "neutral"}>{m.isActive ? "Active this week" : "Inactive"}</Badge>
                {m.isMembershipActive && <Badge variant="success">Paid membership</Badge>}
              </div>
            </div>
          </Card>

          {m.banReason && <Card className="p-5 text-[14px]"><span className="font-semibold">Reason on record: </span>{m.banReason}</Card>}

          <Section title="Account">
            <Field label="Institution"><Link href={`/institutions/${m.institutionId}`} className="hover:underline">{m.institutionName}</Link></Field>
            <Field label="Phone">{m.phone}</Field>
            <Field label="Email verified">{m.isEmailVerified ? "Yes" : "No"}</Field>
            <Field label="Joined">{when(m.createdAt)}</Field>
            <Field label="Last sign-in">{m.lastLoginAt ? when(m.lastLoginAt) : "Not yet"}</Field>
            <Field label="Accepted terms">{when(m.termsAcceptedAt)}</Field>
          </Section>

          <Section title={m.organizationType === "Alumni" ? "School" : "Community"}>
            {m.organizationType === "Alumni" && <Field label="Graduation year">{m.graduationYear || null}</Field>}
            {m.organizationType === "Alumni" && <Field label="Year of entry">{m.yearOfEntry}</Field>}
            <Field label="Programme">{m.program}</Field>
            <Field label="Student ID">{m.studentId}</Field>
            <Field label="Member number">{m.memberNumber}</Field>
            <Field label="House">{m.house}</Field>
          </Section>

          <Section title="Work and profile">
            <Field label="Employment">{m.employmentStatus}</Field>
            <Field label="Company">{m.company}</Field>
            <Field label="Job title">{m.jobTitle}</Field>
            <Field label="Location">{m.location}</Field>
            <Field label="LinkedIn">{m.linkedInUrl && <a href={m.linkedInUrl} target="_blank" rel="noopener noreferrer" className="break-all underline">{m.linkedInUrl}</a>}</Field>
            <Field label="Skills">{m.skills?.length ? m.skills.join(", ") : null}</Field>
            <Field label="Interests">{m.interests?.length ? m.interests.join(", ") : null}</Field>
            <Field label="Bio">{m.bio}</Field>
          </Section>

          <Section title="Membership">
            <Field label="Status">{m.isMembershipActive ? "Active" : "Not active"}</Field>
            <Field label="Expires">{when(m.membershipExpiry)}</Field>
            <Field label="Years paid">{m.membershipYearsPaid || null}</Field>
            <Field label="Last paid">{when(m.lastMembershipPaidAt)}</Field>
          </Section>
        </>
      )}
    </div>
  );
}
