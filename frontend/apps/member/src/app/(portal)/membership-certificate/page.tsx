"use client";

import { useRef, useState, useCallback, useMemo, Fragment } from "react";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Download, Award, CheckCircle2 } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { formatCurrency, formatDate, cn } from "@alumni/ui";
import {
  getMyProfile, getMyContributions, getMyCampaigns, getMyMembershipStatus,
} from "@/lib/member-api";
import { publicMemberClient } from "@/lib/api-client";
import type { Campaign, Contribution } from "@/types";
import { contributionMethodLabel } from "@/types";

interface InstitutionThemeResponse {
  portalName: string;
  displayName: string;
  logoUrl: string | null;
}

interface PaidMembership {
  campaign: Campaign;
  contribution: Contribution;
}

export default function MembershipCertificatePage() {
  const certRef      = useRef<HTMLDivElement>(null);
  const [downloading, setDownloading] = useState(false);
  const [selectedYear, setSelectedYear] = useState<number | null>(null);

  const { data: profile, isLoading: loadingProfile } = useQuery({
    queryKey: ["m-profile"],
    queryFn:  getMyProfile,
  });

  // The certificate must show this institution's own name/logo, not a fixed
  // one — this platform hosts any number of institutions, each with their
  // own branding, so it's fetched per-tenant rather than baked in.
  const { data: theme } = useQuery({
    queryKey: ["m-institution-theme"],
    queryFn: async () => {
      const res = await publicMemberClient.get<{ data: InstitutionThemeResponse }>("/public/institution/theme");
      return res.data.data;
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });

  const { data: membershipStatus } = useQuery({
    queryKey: ["m-membership-status"],
    queryFn:  getMyMembershipStatus,
    retry:    false,
  });

  const { data: campaignsData, isLoading: loadingCampaigns } = useQuery({
    queryKey: ["m-campaigns"],
    queryFn:  () => getMyCampaigns(1, 100),
  });

  const { data: contributionsData, isLoading: loadingContribs } = useQuery({
    queryKey: ["m-contributions-all"],
    queryFn:  () => getMyContributions({ pageSize: 500 }),
  });

  const isLoading = loadingProfile || loadingCampaigns || loadingContribs;

  const paidMemberships: PaidMembership[] = useMemo(() => {
    if (!campaignsData?.results || !contributionsData?.results) return [];
    const membershipCampaigns = campaignsData.results.filter(c => c.isMembershipCampaign);
    const confirmedContribs   = contributionsData.results.filter(c => c.status === "Confirmed");
    return membershipCampaigns
      .map(campaign => {
        const contribution = confirmedContribs.find(c => c.campaignId === campaign.id);
        return contribution ? { campaign, contribution } : null;
      })
      .filter((x): x is PaidMembership => x !== null)
      .sort((a, b) => (b.campaign.membershipYear ?? 0) - (a.campaign.membershipYear ?? 0));
  }, [campaignsData, contributionsData]);

  const selected = useMemo(
    () => (selectedYear
      ? paidMemberships.find(p => p.campaign.membershipYear === selectedYear)
      : paidMemberships[0]),
    [paidMemberships, selectedYear]
  );

  const activeYear = selected?.campaign.membershipYear ?? paidMemberships[0]?.campaign.membershipYear;

  const handleDownload = useCallback(async () => {
    if (!certRef.current || !selected) return;
    setDownloading(true);
    try {
      const html2canvas = (await import("html2canvas-pro")).default;
      const { jsPDF }   = await import("jspdf");
      const el          = certRef.current;

      const originalStyle = el.style.cssText;
      el.style.width    = "840px";
      el.style.height   = "594px";
      el.style.position = "fixed";
      el.style.left     = "-9999px";
      el.style.top      = "0";
      void el.offsetHeight; // force a layout reflow before measuring/capturing

      const canvas = await html2canvas(el, {
        scale:           3,
        useCORS:         true,
        backgroundColor: "#ffffff",
        logging:         false,
      });
      el.style.cssText = originalStyle;

      const pdf  = new jsPDF({ orientation: "landscape", unit: "mm", format: "a4" });
      const pageW = pdf.internal.pageSize.getWidth();
      const pageH = pdf.internal.pageSize.getHeight();
      const ratio = Math.min(pageW / canvas.width, pageH / canvas.height);
      const w = canvas.width  * ratio;
      const h = canvas.height * ratio;
      pdf.addImage(canvas.toDataURL("image/png"), "PNG", (pageW - w) / 2, (pageH - h) / 2, w, h);
      pdf.save(`Certificate-${selected.campaign.membershipYear}-${profile?.firstName ?? "Member"}-${profile?.lastName ?? ""}.pdf`);
    } catch {
      window.print();
    } finally {
      setDownloading(false);
    }
  }, [selected, profile]);

  /* ── Loading ── */
  if (isLoading) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 space-y-6 max-w-5xl mx-auto">
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  /* ── No certificates ── */
  if (paidMemberships.length === 0) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 max-w-5xl mx-auto space-y-6">
        <button
          onClick={() => history.back()}
          className="flex items-center gap-1.5 text-[13.5px] font-semibold transition-colors hover:underline"
          style={{ color: "var(--muted-foreground)" }}
        >
          <ArrowLeft size={15} /> Back
        </button>

        <div
          className="rounded-2xl border p-10 text-center space-y-4"
          style={{ borderColor: "var(--border)", background: "var(--background)" }}
        >
          <Award size={40} style={{ color: "var(--muted-foreground)", opacity: 0.3, margin: "0 auto" }} />
          <p className="text-[16px] font-semibold" style={{ color: "var(--foreground)" }}>
            No certificates yet
          </p>
          <p className="text-[14px] max-w-sm mx-auto" style={{ color: "var(--muted-foreground)" }}>
            Once you pay a membership campaign, your certificate will be available here.
          </p>
          <Link href="/contributions">
            <Button variant="outline" className="font-semibold mt-2">Browse campaigns</Button>
          </Link>
        </div>
      </div>
    );
  }

  const memberName     = `${profile?.firstName ?? ""} ${profile?.lastName ?? ""}`.trim() || "Alumni Member";
  const memberNumber   = profile?.memberNumber ?? profile?.id?.slice(0, 8).toUpperCase() ?? "";
  const graduationYear = profile?.graduationYear;
  const paymentDate    = selected?.contribution.confirmedAt ?? selected?.contribution.createdAt;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-5xl mx-auto space-y-6">

      {/* ── Header ── */}
      <div>
        <button
          onClick={() => history.back()}
          className="flex items-center gap-1.5 text-[13.5px] font-semibold mb-2 transition-colors hover:underline"
          style={{ color: "var(--muted-foreground)" }}
        >
          <ArrowLeft size={15} /> Back
        </button>
        <PageHeader
          eyebrow="Verified membership record"
          title="Membership Certificate"
          description="View and download your certificates"
        >
          <Button
            onClick={handleDownload}
            disabled={downloading}
            className="font-semibold gap-2 text-[14px] shrink-0"
            style={{ height: 44 }}
          >
            <Download size={15} />
            {downloading ? "Generating PDF…" : "Download PDF"}
          </Button>
        </PageHeader>
      </div>

      {/* ── Year selector ── */}
      {paidMemberships.length > 1 && (
        <div className="flex flex-wrap gap-2">
          {paidMemberships.map(p => (
            <button
              key={p.campaign.membershipYear}
              onClick={() => setSelectedYear(p.campaign.membershipYear ?? null)}
              className={cn(
                "px-4 py-2 rounded-full text-[13px] font-semibold border transition-colors",
                p.campaign.membershipYear === activeYear ? "text-white border-transparent" : "border-border hover:border-primary/40",
              )}
              style={p.campaign.membershipYear === activeYear
                ? { background: "var(--primary)", color: "white" }
                : { background: "var(--background)", color: "var(--muted-foreground)" }}
            >
              {p.campaign.membershipYear}
            </button>
          ))}
        </div>
      )}

      {/* ── Certificate ── */}
      {selected && (
        <div
          ref={certRef}
          className="relative bg-white rounded-2xl overflow-hidden shadow-2xl"
          style={{ aspectRatio: "297/210", containerType: "inline-size" }}
        >
          {/* Decorative double border */}
          <div className="absolute inset-0" style={{ padding: "clamp(4px, 2.1cqw, 20px)" }}>
            <div className="absolute border sm:border-2 border-amber-600/30 rounded-lg sm:rounded-xl"
              style={{ inset: "clamp(4px, 2.1cqw, 20px)" }} />
            <div className="absolute border border-amber-600/15 rounded sm:rounded-lg"
              style={{ inset: "clamp(8px, 2.5cqw, 24px)" }} />
          </div>

          {/* Corner ornaments */}
          {(["top", "bottom"] as const).flatMap(v =>
            (["left", "right"] as const).map(h => (
              <div
                key={`${v}-${h}`}
                className={cn(
                  "absolute border-amber-600/40",
                  v === "top" ? "border-t-2 rounded-tl" : "border-b-2 rounded-bl",
                  h === "left" ? "border-l-2" : "border-r-2",
                )}
                style={{
                  [v]: "clamp(8px, 3.4cqw, 32px)",
                  [h]: "clamp(8px, 3.4cqw, 32px)",
                  width:  "clamp(12px, 5cqw, 48px)",
                  height: "clamp(12px, 5cqw, 48px)",
                }}
              />
            ))
          )}

          {/* Dot pattern */}
          <div className="absolute inset-0 opacity-[0.025]"
            style={{ backgroundImage: "radial-gradient(circle at 25px 25px, #000 1px, transparent 1px)", backgroundSize: "50px 50px" }} />

          {/* Content */}
          <div
            className="relative h-full flex flex-col items-center justify-center text-center"
            style={{ padding: "clamp(8px, 4.2cqw, 40px) clamp(12px, 5cqw, 48px)" }}
          >
            {/* Logo + org name */}
            <div className="flex items-center" style={{ gap: "clamp(4px, 1.3cqw, 12px)", marginBottom: "clamp(4px, 1.7cqw, 16px)" }}>
              {theme?.logoUrl ? (
                <Image
                  src={theme.logoUrl}
                  alt={`${theme.displayName} logo`}
                  width={48}
                  height={48}
                  unoptimized
                  className="object-contain"
                  style={{ width: "clamp(16px, 5cqw, 48px)", height: "clamp(16px, 5cqw, 48px)" }}
                />
              ) : (
                <div
                  className="rounded-full flex items-center justify-center font-bold text-white"
                  style={{
                    width: "clamp(16px, 5cqw, 48px)", height: "clamp(16px, 5cqw, 48px)",
                    background: "var(--primary)", fontSize: "clamp(7px, 2.1cqw, 20px)",
                  }}
                >
                  {(theme?.displayName ?? "A").charAt(0).toUpperCase()}
                </div>
              )}
              <h2
                className="font-bold text-amber-800 tracking-wider"
                style={{ fontSize: "clamp(7px, 2.1cqw, 20px)" }}
              >
                {(theme?.displayName ?? "ALUMNI").toUpperCase()} ALUMNI ASSOCIATION
              </h2>
            </div>

            {/* Divider */}
            <div className="h-px bg-gradient-to-r from-transparent via-amber-600/40 to-transparent"
              style={{ width: "clamp(24px, 10cqw, 96px)", marginBottom: "clamp(4px, 2.1cqw, 20px)" }} />

            {/* Title */}
            <h1
              className="font-bold text-gray-900 tracking-tight"
              style={{ fontSize: "clamp(10px, 5cqw, 48px)", fontFamily: "Georgia, 'Times New Roman', serif", marginBottom: "clamp(2px, 0.9cqw, 8px)" }}
            >
              Certificate of Membership
            </h1>
            <p
              className="text-gray-500 font-medium tracking-widest uppercase"
              style={{ fontSize: "clamp(5px, 1.5cqw, 14px)", marginBottom: "clamp(6px, 3.4cqw, 32px)" }}
            >
              {selected.campaign.membershipYear} Membership Year
            </p>

            {/* Member name */}
            <p className="text-gray-500" style={{ fontSize: "clamp(5px, 1.5cqw, 14px)", marginBottom: "clamp(2px, 0.9cqw, 8px)" }}>
              This certifies that
            </p>
            <h2
              className="font-bold text-primary"
              style={{ fontSize: "clamp(9px, 3.8cqw, 36px)", fontFamily: "Georgia, 'Times New Roman', serif", marginBottom: "clamp(2px, 0.9cqw, 8px)" }}
            >
              {memberName}
            </h2>
            <div className="h-px bg-gradient-to-r from-transparent via-gray-300 to-transparent"
              style={{ width: "clamp(60px, 30cqw, 288px)", marginBottom: "clamp(6px, 2.5cqw, 24px)" }} />

            {/* Details row */}
            <div
              className="flex flex-wrap items-center justify-center"
              style={{ gap: "clamp(3px, 2.1cqw, 20px)", marginBottom: "clamp(6px, 3.4cqw, 32px)" }}
            >
              {[
                { label: "Member ID",  value: memberNumber                      },
                ...(graduationYear ? [{ label: "Class of", value: String(graduationYear) }] : []),
                { label: "Amount",    value: formatCurrency(selected.contribution.amount) },
                { label: "Paid on",   value: paymentDate ? formatDate(paymentDate) : "—" },
              ].map((item, i, arr) => (
                <Fragment key={item.label}>
                  <div className="text-center" style={{ padding: "0 clamp(2px, 1.3cqw, 12px)" }}>
                    <p className="font-bold uppercase text-amber-600/60"
                      style={{ fontSize: "clamp(4px, 1cqw, 9px)", letterSpacing: "0.15em" }}>
                      {item.label}
                    </p>
                    <p className="font-bold text-gray-800" style={{ fontSize: "clamp(6px, 1.5cqw, 14px)" }}>
                      {item.value}
                    </p>
                  </div>
                  {i < arr.length - 1 && (
                    <span className="text-amber-300 select-none"
                      style={{ fontSize: "clamp(4px, 1.3cqw, 12px)" }}>•</span>
                  )}
                </Fragment>
              ))}
            </div>

            {/* Verified badge */}
            <div
              className="flex items-center rounded-full bg-success/10 border border-success/30"
              style={{ gap: "clamp(2px, 0.9cqw, 8px)", padding: "clamp(2px, 0.9cqw, 8px) clamp(6px, 1.7cqw, 16px)" }}
            >
              <CheckCircle2 className="text-success"
                style={{ width: "clamp(8px, 1.7cqw, 16px)", height: "clamp(8px, 1.7cqw, 16px)" }} />
              <span className="font-semibold text-success tracking-wide"
                style={{ fontSize: "clamp(5px, 1.3cqw, 12px)" }}>
                Payment Verified &amp; Confirmed
              </span>
            </div>

            {/* Footer ref */}
            <div style={{ marginTop: "clamp(4px, 3.4cqw, 32px)" }}>
              <p className="text-gray-300 font-medium tracking-wider"
                style={{ fontSize: "clamp(4px, 1.1cqw, 10px)" }}>
                Ref: {selected.contribution.transactionRef ?? selected.contribution.id.slice(0, 12).toUpperCase()}
              </p>
            </div>
          </div>
        </div>
      )}

      {/* ── Payment details ── */}
      {selected && (
        <div
          className="rounded-2xl border p-5 sm:p-6"
          style={{ borderColor: "var(--border)", background: "var(--background)" }}
        >
          <div className="flex items-center gap-2 mb-4">
            <Award size={15} style={{ color: "var(--primary)" }} />
            <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
              Payment details
            </p>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            {[
              { label: "Campaign",          value: selected.campaign.title,                          wide: false },
              { label: "Membership year",   value: String(selected.campaign.membershipYear ?? "—"),  wide: false },
              { label: "Amount paid",       value: formatCurrency(selected.contribution.amount),     wide: false, green: true },
              { label: "Payment method",    value: contributionMethodLabel(selected.contribution.paymentMethod), wide: false },
              { label: "Payment date",      value: paymentDate ? formatDate(paymentDate) : "—",      wide: false },
              { label: "Transaction ref",   value: selected.contribution.transactionRef ?? "—",      wide: true,  mono: true },
            ].map(item => (
              <div
                key={item.label}
                className={cn("rounded-xl p-3 sm:p-4", item.wide && "col-span-2")}
                style={{ background: "var(--secondary)" }}
              >
                <p
                  className="text-[11px] font-semibold uppercase tracking-wide mb-1"
                  style={{ color: "var(--muted-foreground)" }}
                >
                  {item.label}
                </p>
                <p
                  className={cn("text-[13.5px] font-semibold truncate", item.mono && "font-mono")}
                  style={{ color: item.green ? "var(--success)" : "var(--foreground)" }}
                >
                  {item.value}
                </p>
              </div>
            ))}
          </div>

          {/* Status row */}
          <div className="flex flex-wrap items-center justify-between gap-3 mt-3 pt-4 border-t"
            style={{ borderColor: "var(--border)" }}>
            <div className="flex items-center gap-2">
              <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>Status</p>
              <Badge variant="success" className="text-[11px] font-semibold">
                {selected.contribution.status}
              </Badge>
            </div>
            {membershipStatus && (
              <div className="flex items-center gap-2">
                <CheckCircle2 size={14} style={{ color: "var(--primary)" }} />
                <p className="text-[13px] font-medium" style={{ color: "var(--muted-foreground)" }}>
                  Membership {membershipStatus.isMembershipActive ? "active" : "expired"}
                  {membershipStatus.membershipExpiry && membershipStatus.isMembershipActive
                    ? ` · valid until ${formatDate(membershipStatus.membershipExpiry)}`
                    : ""}
                </p>
              </div>
            )}
          </div>
        </div>
      )}

      <style jsx global>{`
        @media print {
          body * { visibility: hidden !important; }
          [data-cert-print], [data-cert-print] * { visibility: visible !important; }
          [data-cert-print] { position: fixed; top: 0; left: 0; width: 100vw; height: 100vh; }
        }
      `}</style>
    </div>
  );
}
