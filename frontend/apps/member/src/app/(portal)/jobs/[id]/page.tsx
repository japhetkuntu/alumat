"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import {
  Briefcase, MapPin, Clock, ExternalLink,
  ArrowLeft, Globe, Building2, Calendar,
} from "lucide-react";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { getJobById } from "@/lib/member-api";
import type { Job } from "@/types";

/* ─────────────────────────────────────────────────────────────────────────
   DAYS LEFT — plain-language deadline indicator
   ───────────────────────────────────────────────────────────────────────── */
function DaysLeft({ deadline }: { deadline: string }) {
  const [now] = useState(() => Date.now());
  const days = Math.ceil((new Date(deadline).getTime() - now) / 86_400_000);
  if (days < 0)  return <span className="text-[13px] font-semibold text-destructive">Deadline passed</span>;
  if (days === 0) return <span className="text-[13px] font-semibold text-destructive">Closes today</span>;
  if (days <= 7)  return <span className="text-[13px] font-semibold" style={{ color: "#d97706" }}>{days} day{days !== 1 ? "s" : ""} left</span>;
  return <span className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>{formatDate(deadline)}</span>;
}

const typeVariant: Record<string, "info" | "secondary" | "warning" | "success"> = {
  "Full-time": "info",
  "Part-time": "secondary",
  Contract:    "warning",
  Internship:  "success",
};

export default function MemberJobDetailPage() {
  const { id }   = useParams() as { id: string };
  const router   = useRouter();

  const { data: job, isLoading } = useQuery({
    queryKey: ["m-job", id],
    queryFn:  () => getJobById(id),
  });

  /* ── Loading ── */
  if (isLoading) {
    return (
      <div className="p-6 lg:p-10 max-w-5xl mx-auto space-y-6 animate-pulse">
        <div className="h-6 w-24 rounded-lg" style={{ background: "var(--secondary)" }} />
        <div className="h-56 w-full rounded-2xl" style={{ background: "var(--secondary)" }} />
        <div className="space-y-3">
          <div className="h-8 w-2/3 rounded-lg" style={{ background: "var(--secondary)" }} />
          <div className="h-4 w-full rounded-lg"  style={{ background: "var(--secondary)" }} />
          <div className="h-4 w-4/5 rounded-lg"  style={{ background: "var(--secondary)" }} />
        </div>
      </div>
    );
  }

  if (!job) return (
    <div className="p-6 lg:p-10 max-w-5xl mx-auto">
      <Button variant="ghost" size="sm" className="mb-6 font-semibold gap-2" onClick={() => router.push("/jobs")}>
        <ArrowLeft size={15} /> Back to jobs
      </Button>
      <EmptyState
        icon={<Briefcase size={40} />}
        title="Job not found"
        description="This listing may have been removed or the link is incorrect."
      />
    </div>
  );

  const deadlinePassed = job.deadline && new Date(job.deadline) < new Date();

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto pb-20">

      {/* ── Nav row ── */}
      <div className="flex items-center justify-between mb-6 sm:mb-8">
        <button
          onClick={() => router.push("/jobs")}
          className="flex items-center gap-1.5 text-[13.5px] font-semibold transition-colors hover:underline"
          style={{ color: "var(--muted-foreground)" }}
        >
          <ArrowLeft size={15} /> Back to jobs
        </button>
        <Badge variant={typeVariant[job.type] ?? "secondary"} className="text-[11px] font-semibold uppercase tracking-wide">
          {job.type}
        </Badge>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 lg:gap-12">

        {/* ════════════════════════════════════
            LEFT — main content
        ════════════════════════════════════ */}
        <div className="lg:col-span-7 space-y-8">

          {/* Banner */}
          <div className="rounded-2xl overflow-hidden border" style={{ borderColor: "var(--border)" }}>
            {job.bannerImageUrl ? (
              <img
                src={job.bannerImageUrl}
                alt={job.title}
                className="w-full object-cover"
                style={{ maxHeight: 380 }}
              />
            ) : (
              <div
                className="flex flex-col items-center justify-center gap-3 py-14"
                style={{ background: "var(--secondary)" }}
              >
                <div
                  className="w-16 h-16 rounded-2xl flex items-center justify-center"
                  style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
                >
                  <Briefcase size={28} style={{ color: "var(--primary)" }} />
                </div>
                <p className="text-[15px] font-semibold" style={{ color: "var(--muted-foreground)" }}>
                  {job.company}
                </p>
              </div>
            )}
          </div>

          {/* Title + meta */}
          <div>
            <h1
              className="font-[family-name:var(--font-display)] leading-tight mb-4"
              style={{ fontSize: "clamp(1.5rem, 3.5vw, 2.25rem)", fontWeight: 700, letterSpacing: "-0.02em", color: "var(--foreground)" }}
            >
              {job.title}
            </h1>
            <div className="flex flex-wrap gap-x-5 gap-y-2">
              {[
                { icon: Building2, text: job.company },
                { icon: MapPin,    text: job.location },
                { icon: Calendar,  text: `Posted ${formatDate(job.createdAt)}` },
                ...(job.deadline ? [{ icon: Clock, text: `Deadline: ${formatDate(job.deadline)}` }] : []),
              ].map(({ icon: Icon, text }) => (
                <div key={text} className="flex items-center gap-1.5 text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
                  <Icon size={14} style={{ color: "var(--primary)" }} />
                  {text}
                </div>
              ))}
            </div>
          </div>

          {/* Apply button — shown in content on mobile, sidebar on desktop */}
          <div className="lg:hidden">
            <ApplyBlock job={job} deadlinePassed={!!deadlinePassed} />
          </div>

          {/* Description */}
          <div>
            <h2
              className="font-[family-name:var(--font-display)] mb-4 pb-3 border-b"
              style={{ fontSize: "1.1rem", fontWeight: 700, color: "var(--foreground)", borderColor: "var(--border)" }}
            >
              About the role
            </h2>
            <p
              className="whitespace-pre-wrap leading-[1.85]"
              style={{ fontSize: "0.9625rem", color: "var(--muted-foreground)" }}
            >
              {job.description || "No description has been provided for this listing. Please visit the application link or contact the alumni office for more information."}
            </p>
          </div>

        </div>

        {/* ════════════════════════════════════
            RIGHT — sticky sidebar
        ════════════════════════════════════ */}
        <aside className="lg:col-span-5">
          <div
            className="rounded-2xl border overflow-hidden lg:sticky lg:top-24"
            style={{ borderColor: "var(--border)", background: "var(--background)" }}
          >
            <div className="h-1 w-full" style={{ background: "var(--primary)" }} />
            <div className="p-5 sm:p-6 space-y-5">

              {/* Apply — desktop */}
              <div className="hidden lg:block">
                <ApplyBlock job={job} deadlinePassed={!!deadlinePassed} />
              </div>

              {/* Details list */}
              <div
                className="rounded-xl border divide-y overflow-hidden"
                style={{ borderColor: "var(--border)" }}
              >
                {[
                  { icon: Building2, label: "Company",  value: job.company },
                  { icon: MapPin,    label: "Location",  value: job.location },
                  { icon: Briefcase, label: "Job type",  value: job.type },
                  { icon: Calendar,  label: "Posted",    value: formatDate(job.createdAt) },
                  ...(job.deadline ? [{
                    icon: Clock,
                    label: "Deadline",
                    value: null,
                    custom: <DaysLeft deadline={job.deadline} />,
                  }] : []),
                ].map(({ icon: Icon, label, value, custom }) => (
                  <div key={label} className="flex items-center gap-3 px-4 py-3.5"
                    style={{ background: "var(--background)" }}>
                    <div
                      className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
                      style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
                    >
                      <Icon size={14} style={{ color: "var(--primary)" }} />
                    </div>
                    <div className="flex-1 flex items-center justify-between gap-3 min-w-0">
                      <span className="text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>{label}</span>
                      {custom ?? (
                        <span className="text-[13.5px] font-semibold text-right" style={{ color: "var(--foreground)" }}>
                          {value}
                        </span>
                      )}
                    </div>
                  </div>
                ))}
              </div>

            </div>
          </div>
        </aside>

      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   APPLY BLOCK — shared between mobile (inline) and desktop (sidebar)
   ───────────────────────────────────────────────────────────────────────── */
function ApplyBlock({ job, deadlinePassed }: { job: Job; deadlinePassed: boolean }) {
  if (deadlinePassed) {
    return (
      <div
        className="rounded-xl p-4 text-center"
        style={{ background: "var(--secondary)", border: "1px solid var(--border)" }}
      >
        <p className="text-[14px] font-semibold mb-1" style={{ color: "var(--foreground)" }}>
          Applications closed
        </p>
        <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
          The deadline for this position has passed.
        </p>
      </div>
    );
  }

  if (job.applyUrl) {
    return (
      <div className="space-y-3">
        <a href={job.applyUrl} target="_blank" rel="noopener noreferrer" className="block">
          <Button className="w-full font-bold text-[15px] gap-2" style={{ height: 48 }}>
            <Globe size={16} />
            Apply now
            <ExternalLink size={13} className="ml-auto opacity-60" />
          </Button>
        </a>
        {job.deadline && (
          <p className="text-center text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>
            <DaysLeft deadline={job.deadline} />
          </p>
        )}
      </div>
    );
  }

  return (
    <div
      className="rounded-xl p-4 text-center"
      style={{ background: "var(--secondary)", border: "1px solid var(--border)" }}
    >
      <p className="text-[14px] font-semibold mb-1" style={{ color: "var(--foreground)" }}>
        No application link
      </p>
      <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
        Contact the alumni office for more information.
      </p>
    </div>
  );
}
