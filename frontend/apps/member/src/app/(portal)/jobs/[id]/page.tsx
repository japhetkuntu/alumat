"use client";

import { useQuery } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import {
  Briefcase, MapPin, Clock, ExternalLink, ArrowLeft, Globe,
  Building2, Calendar, CheckCircle2,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { formatDate } from "@/lib/utils";
import { getJobById } from "@/lib/member-api";

const typeColors: Record<string, "info" | "secondary" | "warning" | "success"> = {
  "Full-time": "info",
  "Part-time": "secondary",
  Contract: "warning",
  Internship: "success",
};

function DeadlineStatus({ deadline }: { deadline: string }) {
  const deadlineDate = new Date(deadline);
  const now = new Date();
  const diffDays = Math.ceil((deadlineDate.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));

  if (diffDays < 0) {
    return (
      <span className="text-[10px] font-bold text-destructive uppercase tracking-widest animate-in fade-in">
        Deadline passed
      </span>
    );
  }
  if (diffDays <= 7) {
    return (
      <span className="text-[10px] font-bold text-orange-500 uppercase tracking-widest animate-in fade-in">
        {diffDays === 0 ? "Closes today" : `${diffDays} day${diffDays === 1 ? "" : "s"} left`}
      </span>
    );
  }
  return (
    <span className="text-[10px] font-bold text-muted-foreground uppercase tracking-widest">
      {formatDate(deadline)}
    </span>
  );
}

export default function MemberJobDetailPage() {
  const { id } = useParams() as { id: string };
  const router = useRouter();

  const { data: job, isLoading } = useQuery({
    queryKey: ["m-job", id],
    queryFn: () => getJobById(id),
  });

  if (isLoading) {
    return (
      <div className="p-8 lg:p-12 max-w-5xl mx-auto space-y-8 animate-pulse">
        <div className="h-8 w-32 bg-muted rounded-lg" />
        <div className="h-56 w-full bg-muted rounded-2xl" />
        <div className="space-y-4">
          <div className="h-10 w-2/3 bg-muted rounded-lg" />
          <div className="h-4 w-full bg-muted rounded-lg" />
          <div className="h-4 w-5/6 bg-muted rounded-lg" />
        </div>
      </div>
    );
  }

  if (!job) return null;

  const deadlinePassed = job.deadline && new Date(job.deadline) < new Date();

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-6 sm:space-y-8 lg:space-y-12 pb-24 selection:bg-primary/20">
      {/* Navigation */}
      <nav className="flex items-center justify-between animate-in fade-in slide-in-from-top-4 duration-500">
        <Button
          variant="ghost"
          size="sm"
          className="h-10 rounded-xl hover:bg-black/5 dark:hover:bg-white/5 font-bold group"
          onClick={() => router.back()}
        >
          <ArrowLeft size={18} className="mr-2 group-hover:-translate-x-1 transition-transform" />
          Back to Jobs
        </Button>
        <Badge variant={typeColors[job.type] ?? "secondary"} className="h-7 px-3 font-black uppercase tracking-widest text-[10px]">
          {job.type}
        </Badge>
      </nav>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 sm:gap-8 lg:gap-12">
        {/* Main Content */}
        <div className="lg:col-span-8 space-y-6 sm:space-y-8 lg:space-y-10">
          {/* Banner / Hero */}
          <section className="animate-in fade-in zoom-in-95 duration-1000 delay-200">
            {job.bannerImageUrl ? (
              <div className="rounded-[2rem] overflow-hidden shadow-2xl border-4 border-white dark:border-white/5 ring-1 ring-black/5 ring-offset-4 ring-offset-background aspect-video relative group">
                <img
                  src={job.bannerImageUrl}
                  alt={job.title}
                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-1000"
                />
                <div className="absolute inset-0 bg-gradient-to-t from-black/30 to-transparent" />
                <div className="absolute bottom-6 left-6">
                  <div className="flex items-center gap-2 p-3 rounded-2xl bg-black/40 backdrop-blur-sm border border-white/10">
                    <Building2 size={18} className="text-white/80" />
                    <span className="text-white font-black text-lg">{job.company}</span>
                  </div>
                </div>
              </div>
            ) : (
              <div className="rounded-[2rem] bg-gradient-to-br from-primary/10 via-primary/5 to-muted/20 aspect-video flex flex-col items-center justify-center border-4 border-white dark:border-white/5 ring-1 ring-black/5 gap-4">
                <div className="w-24 h-24 rounded-[1.5rem] bg-primary/10 flex items-center justify-center">
                  <Briefcase size={48} className="text-primary/30" />
                </div>
                <p className="text-lg font-black text-foreground/30">{job.company}</p>
              </div>
            )}
          </section>

          {/* Title & Metadata */}
          <header className="space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
            <h1 className="text-2xl sm:text-3xl lg:text-4xl xl:text-5xl font-black tracking-tight text-foreground leading-[1.1]">
              {job.title}
            </h1>
            <div className="flex flex-wrap items-center gap-3 sm:gap-6 text-sm font-bold text-muted-foreground">
              <div className="flex items-center gap-2">
                <Building2 size={18} className="text-primary" />
                {job.company}
              </div>
              <div className="flex items-center gap-2">
                <MapPin size={18} className="text-primary" />
                {job.location}
              </div>
              {job.deadline && (
                <div className="flex items-center gap-2">
                  <Clock size={18} className="text-primary" />
                  Deadline: {formatDate(job.deadline)}
                </div>
              )}
              <div className="flex items-center gap-2">
                <Calendar size={18} className="text-primary" />
                Posted {formatDate(job.createdAt)}
              </div>
            </div>
          </header>

          {/* Description */}
          <section className="space-y-6 prose prose-lg dark:prose-invert max-w-none animate-in fade-in slide-in-from-bottom-6 duration-700 delay-300">
            <h2 className="text-2xl font-black tracking-tight border-b border-border/40 pb-4">
              About the Role
            </h2>
            <div className="whitespace-pre-wrap font-medium text-muted-foreground leading-relaxed text-lg">
              {job.description || "No detailed description has been provided for this job posting. Please visit the application link for more information."}
            </div>
          </section>
        </div>

        {/* Sidebar */}
        <aside className="lg:col-span-4 space-y-8 animate-in fade-in slide-in-from-right-8 duration-1000 delay-300">
          <Card className="sticky top-24 overflow-hidden border-border/40 shadow-2xl shadow-primary/5 bg-card/80 backdrop-blur-xl">
            <div className="absolute top-0 left-0 right-0 h-2 bg-primary" />
            <CardContent className="p-5 sm:p-8 lg:p-10 space-y-6 sm:space-y-8 pt-8 sm:pt-12">
              {/* Apply CTA */}
              <div className="space-y-4">
                <p className="text-[11px] font-bold text-muted-foreground uppercase tracking-[0.2em] mb-2">Application</p>
                {deadlinePassed ? (
                  <div className="p-6 rounded-2xl bg-muted/50 border border-border/40 text-center space-y-2">
                    <p className="text-sm font-bold text-muted-foreground">Application Closed</p>
                    <p className="text-[11px] font-medium text-muted-foreground/60 uppercase tracking-widest">
                      The deadline for this position has passed.
                    </p>
                  </div>
                ) : job.applyUrl ? (
                  <a href={job.applyUrl} target="_blank" rel="noopener noreferrer">
                    <Button className="w-full h-16 rounded-2xl font-black text-lg shadow-2xl shadow-primary/20 hover:shadow-primary/40 transition-all hover:scale-[1.02] active:scale-[0.98] group/btn">
                      <Globe size={22} className="mr-3 group-hover/btn:rotate-12 transition-transform" />
                      Apply Now
                      <ExternalLink size={14} className="ml-auto opacity-60" />
                    </Button>
                  </a>
                ) : (
                  <div className="p-6 rounded-2xl bg-muted/50 border border-border/40 text-center space-y-2">
                    <p className="text-sm font-bold text-muted-foreground">No Application Link</p>
                    <p className="text-[11px] font-medium text-muted-foreground/60 uppercase tracking-widest">
                      Contact the alumni office for more information.
                    </p>
                  </div>
                )}
              </div>

              {/* Deadline indicator */}
              {job.deadline && !deadlinePassed && (
                <div className="flex items-center gap-3 p-4 rounded-2xl bg-primary/5 border border-primary/20 animate-in zoom-in-95 duration-500">
                  <div className="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center text-primary shrink-0">
                    <CheckCircle2 size={20} />
                  </div>
                  <div>
                    <p className="text-[10px] font-bold text-primary uppercase tracking-widest leading-none mb-1">
                      Applications Open
                    </p>
                    <DeadlineStatus deadline={job.deadline} />
                  </div>
                </div>
              )}

              {/* Details */}
              <div className="space-y-3 pt-4 border-t border-border/40">
                <div className="flex items-center gap-3 p-4 rounded-2xl bg-muted/20 border border-border/40 relative group/loc overflow-hidden">
                  <div className="absolute inset-0 bg-primary/5 translate-y-full group-hover/loc:translate-y-0 transition-transform duration-500" />
                  <div className="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center text-primary shrink-0 z-10">
                    <Building2 size={20} />
                  </div>
                  <div className="z-10">
                    <p className="text-[10px] font-bold text-muted-foreground uppercase tracking-widest leading-none mb-1">Company</p>
                    <p className="text-sm font-black">{job.company}</p>
                  </div>
                </div>

                <div className="flex items-center gap-3 p-4 rounded-2xl bg-muted/20 border border-border/40">
                  <div className="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center text-primary shrink-0">
                    <MapPin size={20} />
                  </div>
                  <div>
                    <p className="text-[10px] font-bold text-muted-foreground uppercase tracking-widest leading-none mb-1">Location</p>
                    <p className="text-sm font-black">{job.location}</p>
                  </div>
                </div>

                {job.deadline && (
                  <div className="flex items-center gap-3 p-4 rounded-2xl bg-muted/20 border border-border/40">
                    <div className="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center text-primary shrink-0">
                      <Clock size={20} />
                    </div>
                    <div>
                      <p className="text-[10px] font-bold text-muted-foreground uppercase tracking-widest leading-none mb-1">Deadline</p>
                      <p className="text-sm font-black">{formatDate(job.deadline)}</p>
                    </div>
                  </div>
                )}

                <div className="flex items-center gap-3 p-4 rounded-2xl bg-muted/20 border border-border/40">
                  <div className="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center text-primary shrink-0">
                    <Briefcase size={20} />
                  </div>
                  <div>
                    <p className="text-[10px] font-bold text-muted-foreground uppercase tracking-widest leading-none mb-1">Job Type</p>
                    <p className="text-sm font-black">{job.type}</p>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </aside>
      </div>
    </div>
  );
}
