"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Building2, ChevronRight, MapPin, Phone, Mail, Globe, ExternalLink } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { getBusinessListing } from "@/lib/member-api";

export default function BusinessListingDetailPage() {
  const { id } = useParams<{ id: string }>();

  const { data: biz, isLoading } = useQuery({
    queryKey: ["m-business-listing", id],
    queryFn: () => getBusinessListing(id),
  });

  if (isLoading) {
    return (
      <div className="p-8 lg:p-12 space-y-6 max-w-4xl mx-auto">
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  if (!biz) {
    return (
      <div className="p-8 lg:p-12 max-w-4xl mx-auto">
        <Link href="/business-directory">
          <Button variant="ghost" size="sm" className="mb-6"><ArrowLeft size={14} />Back to Directory</Button>
        </Link>
        <EmptyState icon={<Building2 size={48} />} title="Business not found" description="This listing may have been removed, unpublished, or the link is incorrect." />
      </div>
    );
  }

  const contactMethods = [
    biz.phoneNumber && { icon: Phone, label: biz.phoneNumber, href: `tel:${biz.phoneNumber}` },
    biz.email && { icon: Mail, label: biz.email, href: `mailto:${biz.email}` },
    biz.websiteUrl && { icon: Globe, label: biz.websiteUrl.replace(/^https?:\/\//, ""), href: biz.websiteUrl, external: true },
    biz.externalLinkUrl && { icon: ExternalLink, label: "More info", href: biz.externalLinkUrl, external: true },
  ].filter(Boolean) as { icon: typeof Phone; label: string; href: string; external?: boolean }[];

  return (
    <div className="p-4 sm:p-8 lg:p-12 max-w-4xl mx-auto space-y-8">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-1.5 text-sm animate-in fade-in slide-in-from-top-4 duration-500">
        <Link href="/business-directory">
          <Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg font-semibold group -ml-2">
            <ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />
            Business Directory
          </Button>
        </Link>
        <ChevronRight size={14} className="text-muted-foreground/50" />
        <span className="text-[13px] font-semibold text-foreground/70 truncate max-w-[200px] sm:max-w-xs">{biz.businessName}</span>
      </nav>

      {/* Banner + Logo */}
      <div className="relative animate-in fade-in duration-700">
        <div className="rounded-2xl overflow-hidden h-44 sm:h-56 bg-gradient-to-br from-primary/10 to-muted/20">
          {biz.bannerUrl && (
            <img src={biz.bannerUrl} alt="" className="w-full h-full object-cover" loading="eager" />
          )}
        </div>
        <div className="absolute -bottom-8 left-6 h-20 w-20 rounded-2xl border-4 border-background bg-background shadow-lg overflow-hidden flex items-center justify-center">
          {biz.logoUrl ? (
            <img src={biz.logoUrl} alt={biz.businessName} className="w-full h-full object-cover" loading="lazy" />
          ) : (
            <Building2 size={28} className="text-primary/50" />
          )}
        </div>
      </div>

      {/* Title + Meta */}
      <div className="space-y-3 pt-6 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-150">
        <h1
          className="font-[family-name:var(--font-display)] leading-tight"
          style={{ fontSize: "clamp(1.5rem, 3vw, 2rem)", fontWeight: 700, letterSpacing: "-0.02em", color: "var(--foreground)" }}
        >
          {biz.businessName}
        </h1>
        <p className="flex items-center gap-1.5 text-[13.5px] text-muted-foreground font-medium">
          <MapPin size={14} />
          {biz.location}
        </p>
      </div>

      <Card>
        <CardContent className="p-6 lg:p-8 space-y-6">
          {biz.description && (
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground/50 mb-3">About</p>
              <p className="text-[15px] leading-relaxed text-foreground/90 whitespace-pre-line">{biz.description}</p>
            </div>
          )}

          {contactMethods.length > 0 && (
            <div className="space-y-3 pt-2 border-t border-border/40">
              <p className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground/50 pt-4">Contact</p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {contactMethods.map((method, i) => (
                  <a
                    key={i}
                    href={method.href}
                    target={method.external ? "_blank" : undefined}
                    rel={method.external ? "noopener noreferrer" : undefined}
                    className="flex items-center gap-2.5 rounded-xl border border-border/40 px-3.5 py-3 hover:border-primary/40 hover:bg-muted/30 transition-all duration-200 group"
                  >
                    <div className="h-8 w-8 rounded-lg bg-primary/10 text-primary flex items-center justify-center shrink-0">
                      <method.icon size={14} />
                    </div>
                    <span className="text-[13px] font-medium truncate group-hover:text-primary transition-colors">{method.label}</span>
                  </a>
                ))}
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
