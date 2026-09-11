"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { FileText, Receipt, ArrowRight, GraduationCap, ScrollText, Award, Languages, Stamp } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { getServiceTypes } from "@/lib/member-api";

/** A little visual variety across cards — picked deterministically from the name, not random, so a given service always gets the same icon/tint. */
const CARD_STYLES = [
  { icon: ScrollText, tint: "var(--chart-1, #6366f1)" },
  { icon: GraduationCap, tint: "var(--chart-2, #0ea5e9)" },
  { icon: Award, tint: "var(--chart-3, #f59e0b)" },
  { icon: Languages, tint: "var(--chart-4, #10b981)" },
  { icon: Stamp, tint: "var(--chart-5, #ec4899)" },
];
function cardStyleFor(name: string) {
  const hash = Array.from(name).reduce((a, c) => a + c.charCodeAt(0), 0);
  return CARD_STYLES[hash % CARD_STYLES.length];
}

function ServiceCardSkeleton() {
  return (
    <Card>
      <CardContent className="p-6 space-y-4">
        <Skeleton className="h-11 w-11 rounded-xl" />
        <Skeleton className="h-4 w-2/3" variant="text" />
        <Skeleton className="h-3 w-full" variant="text" />
        <Skeleton className="h-3 w-4/5" variant="text" />
      </CardContent>
    </Card>
  );
}

export default function ServicesPage() {
  const { data, isLoading } = useQuery({
    queryKey: ["service-types"],
    queryFn: () => getServiceTypes(1, 50),
  });
  const services = data?.results ?? [];

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-8">
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <PageHeader
          eyebrow="Services"
          title="Alumni services"
          description="Request transcripts, attestation letters, and other institution services — submit, pay, and track progress right here."
        />
        <Link href="/services/requests">
          <Button variant="outline" className="gap-2">
            <Receipt size={16} />
            My requests
          </Button>
        </Link>
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
          {Array.from({ length: 3 }).map((_, i) => <ServiceCardSkeleton key={i} />)}
        </div>
      ) : services.length === 0 ? (
        <EmptyState icon={<FileText size={28} />} title="No services available yet" description="Your institution hasn't set up any services to request yet." />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
          {services.map((s) => {
            const { icon: Icon, tint } = cardStyleFor(s.name);
            return (
              <Link key={s.id} href={`/services/${s.id}`} className="group">
                <Card className="h-full transition-all duration-200 hover:-translate-y-0.5 hover:shadow-lg hover:border-accent/50">
                  <CardContent className="p-6 space-y-4 flex flex-col h-full">
                    <div className="flex items-start justify-between gap-3">
                      <div
                        className="w-11 h-11 rounded-xl flex items-center justify-center shrink-0"
                        style={{ background: `color-mix(in srgb, ${tint} 14%, transparent)` }}
                      >
                        <Icon size={20} style={{ color: tint }} />
                      </div>
                      <Badge variant="secondary" className="shrink-0 font-semibold">
                        {formatCurrency(s.price)}
                      </Badge>
                    </div>
                    <div className="flex-1 space-y-1.5">
                      <h3 className="font-semibold text-[16px] leading-snug">{s.name}</h3>
                      {s.description && <p className="text-[13px] text-muted-foreground leading-relaxed line-clamp-3">{s.description}</p>}
                    </div>
                    <span className="inline-flex items-center gap-1.5 text-[13px] font-semibold text-accent">
                      Request this
                      <ArrowRight size={14} className="transition-transform group-hover:translate-x-0.5" />
                    </span>
                  </CardContent>
                </Card>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}
